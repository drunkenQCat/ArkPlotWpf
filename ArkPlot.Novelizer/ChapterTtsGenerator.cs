using System.Text.RegularExpressions;
using ArkPlot.Core.Services;
using NAudio.Wave;

namespace ArkPlot.Novelizer;

/// <summary>
/// 按章节生成 TTS 音频的独立模块。
/// 输入小说化 md 文件路径，输出每章一个 MP3 文件。
/// 可供 CLI / Avalonia UI 等不同入口复用。
/// </summary>
public class ChapterTtsGenerator : IDisposable
{
    private readonly TtsService _tts;
    private readonly Action<string>? _onLog;
    private readonly string _tempDir;

    public ChapterTtsGenerator(Action<string>? onLog = null)
    {
        _onLog = onLog;
        _tts = new TtsService(rate: "+0%", volume: "+0%");
        _tempDir = Path.Combine(Path.GetTempPath(), $"arkplot_chtts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// 生成结果。
    /// </summary>
    public record ChapterTtsResult(
        string ChapterTitle,
        string OutputPath,
        int SegmentCount,
        int DialogCount,
        int NarrationCount
    );

    /// <summary>
    /// 对小说文件执行按章节 TTS 生成。
    /// </summary>
    /// <param name="novelFilePath">小说化 md 文件路径（如 水晶箭行动_novel_flash.md）。</param>
    /// <param name="outputDir">输出目录，MP3 文件将写入此目录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>每章的生成结果列表。</returns>
    public async Task<List<ChapterTtsResult>> GenerateAsync(
        string novelFilePath,
        string outputDir,
        int? segmentLimit = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);

        // 1. 对齐：小说文本 → AlignmentEntry（带角色名、性别、章节标题）
        Log($"正在对齐: {Path.GetFileName(novelFilePath)}");

        var aligner = new NovelAligner();
        var (entries, stats) = await aligner.AlignByFileNameAsync(novelFilePath);
        Log($"对齐完成: {stats.TotalNovelChapters} 章节, {stats.AlignedDialogs}/{stats.TotalDialogs} 对话已对齐");

        // 2. 按章节分组
        var chapters = entries
            .GroupBy(e => e.ChapterTitle)
            .OrderBy(g =>
            {
                // 保持原章节顺序：按该章节第一个 entry 在列表中的出现位置排序
                return entries.IndexOf(g.First());
            })
            .ToList();

        Log($"将生成 {chapters.Count} 个章节 MP3\n");

        // 3. 逐章生成
        var results = new List<ChapterTtsResult>();
        var baseName = Path.GetFileNameWithoutExtension(novelFilePath);

        for (int chIdx = 0; chIdx < chapters.Count; chIdx++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chapter = chapters[chIdx];
            var title = chapter.Key;
            var chapterEntries = chapter.ToList();

            var safeName = SanitizeFileName(title);
            var outputPath = Path.Combine(outputDir, $"{baseName}_{chIdx + 1:D2}_{safeName}.mp3");

            Log($"📖 [{chIdx + 1}/{chapters.Count}] {title}");

            var segments = chapterEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.NovelText))
                .ToList();

            // 应用片段数量限制
            if (segmentLimit.HasValue && segments.Count > segmentLimit.Value)
            {
                Log($"   ⚠️ 限制为前 {segmentLimit.Value} 个片段");
                segments = segments.Take(segmentLimit.Value).ToList();
            }

            var dialogCount = segments.Count(s => s.IsDialog);
            var narrationCount = segments.Count - dialogCount;

            Log($"   片段: {segments.Count} (对话 {dialogCount}, 旁白 {narrationCount})");

            if (segments.Count == 0)
            {
                Log("   ⚠️ 无内容，跳过\n");
                continue;
            }

            // 合成每个片段
            var tempFiles = new List<string>();
            try
            {
                bool isFirst = true;
                for (int i = 0; i < segments.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var seg = segments[i];
                    var text = SanitizeForTts(seg.NovelText);

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        Log($"   [{i + 1}/{segments.Count}] 跳过（无有效文本）");
                        continue;
                    }

                    // 请求间隔，避免 EdgeTTS 限流
                    if (!isFirst) await Task.Delay(2000, cancellationToken);
                    isFirst = false;

                    // 选择音色
                    string voice;
                    if (!seg.IsDialog)
                        voice = _tts.GetNarratorVoice();  // 旁白
                    else if (!string.IsNullOrEmpty(seg.CharacterName))
                        voice = _tts.GetVoiceForCharacter(seg.CharacterName, seg.Gender);  // 有角色名 → 用实际性别选音色
                    else if (!string.IsNullOrEmpty(seg.Gender))
                        voice = _tts.GetVoiceForGender(seg.Gender);  // 有性别无角色名
                    else
                        voice = _tts.GetFallbackVoice();  // 无法识别 → 固定 fallback（Yunxi）

                    var tempFile = Path.Combine(_tempDir, $"ch{chIdx:D2}_seg{i:D4}.mp3");
                    var preview = text.Length > 30 ? text[..30] + "..." : text;
                    Log($"   [{i + 1}/{segments.Count}] {voice.Replace("Neural", "")} | {preview}");

                    try
                    {
                        await _tts.SynthesizeAsync(text, voice, tempFile);
                    }
                    catch (Exception ex)
                    {
                        var msg = ex.Message;
                        Log($"   ⚠️ 跳过（{ex.GetType().Name}: {msg[..Math.Min(80, msg.Length)]}）");
                        continue;
                    }

                    if (!File.Exists(tempFile) || new FileInfo(tempFile).Length == 0)
                    {
                        Log($"   ⚠️ 跳过（空文件）");
                        continue;
                    }

                    tempFiles.Add(tempFile);
                }

                // 合并为该章节的 MP3
                MergeAudioFiles(tempFiles, outputPath);
                Log($"   ✅ → {Path.GetFileName(outputPath)} ({tempFiles.Count} 段)\n");

                results.Add(new ChapterTtsResult(title, outputPath, segments.Count, dialogCount, narrationCount));
            }
            finally
            {
                foreach (var f in tempFiles)
                {
                    try { if (File.Exists(f)) File.Delete(f); } catch { }
                }
            }
        }

        Log($"🎵 全部完成: {results.Count} 个章节 MP3");
        return results;
    }

    /// <summary>
    /// 合并多个 MP3 文件为单个文件。
    /// </summary>
    private static void MergeAudioFiles(List<string> inputFiles, string outputFile)
    {
        using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);

        foreach (var inputPath in inputFiles)
        {
            using var reader = new Mp3FileReader(inputPath);
            Mp3Frame? frame;
            while ((frame = reader.ReadNextFrame()) != null)
            {
                outputStream.Write(frame.RawData, 0, frame.RawData.Length);
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalid.Contains(c)));
    }

    /// <summary>
    /// 清洗文本用于 TTS：去除 HTML 标签、markdown 图片/链接语法、多余空白。
    /// 截断超长文本避免 EdgeTTS WebSocket 超时。
    /// </summary>
    private static string SanitizeForTts(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var text = raw;

        // 去除 HTML 标签（包括多行）
        text = Regex.Replace(text, @"<[^>]+>", " ");

        // 去除 markdown 图片: ![alt](url)
        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]*\)", "");

        // 去除 markdown 链接但保留文本: [text](url) → text
        text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");

        // 去除反引号包裹的代码
        text = Regex.Replace(text, @"`[^`]*`", "");

        // 去除 markdown 加粗/斜体标记
        text = Regex.Replace(text, @"\*{1,3}([^*]+)\*{1,3}", "$1");

        // 去除 HTML 实体
        text = Regex.Replace(text, @"&\w+;", " ");

        // 压缩空白
        text = Regex.Replace(text, @"\s+", " ").Trim();

        // 截断（EdgeTTS 单段上限约 3000 字符）
        if (text.Length > 2000)
            text = text[..2000];

        return text;
    }

    private void Log(string msg)
    {
        Console.WriteLine(msg);
        _onLog?.Invoke(msg);
    }

    public void Dispose()
    {
        _tts.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
        GC.SuppressFinalize(this);
    }
}
