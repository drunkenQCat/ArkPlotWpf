using System.Text.Json;
using ArkPlot.Core.Services;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// 小说化文本 TTS 生成器：读取对齐后的 JSON，根据性别选择音色，生成 MP3。
/// </summary>
public static class NovelTtsRunner
{
    private class AlignmentEntryJson
    {
        public string NovelText { get; set; } = "";
        public bool IsDialog { get; set; }
        public string? CharacterName { get; set; }
        public string? CharacterCode { get; set; }
        public int EntryIndex { get; set; }
        public string ChapterTitle { get; set; } = "";
        public string? Gender { get; set; }
    }

    public static async Task RunAsync(string alignedJsonPath, int? segmentLimit = null)
    {
        if (!File.Exists(alignedJsonPath))
        {
            Console.Error.WriteLine($"❌ 文件不存在: {alignedJsonPath}");
            return;
        }

        Console.WriteLine("=== Novel TTS Runner ===");
        Console.WriteLine($"输入: {Path.GetFileName(alignedJsonPath)}");

        // 读取对齐 JSON
        var json = await File.ReadAllTextAsync(alignedJsonPath);
        var entries = JsonSerializer.Deserialize<List<AlignmentEntryJson>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (entries == null || entries.Count == 0)
        {
            Console.Error.WriteLine("❌ 对齐 JSON 为空或解析失败");
            return;
        }

        Console.WriteLine($"总片段数: {entries.Count}");

        // 只取对话片段（旁白跳过，或者用固定音色）
        var segments = entries
            .Where(e => e.IsDialog || !string.IsNullOrWhiteSpace(e.NovelText))
            .Take(segmentLimit ?? int.MaxValue)
            .ToList();

        Console.WriteLine($"将合成 {segments.Count} 个片段");

        // TTS 生成
        using var tts = new TtsService(rate: "+0%", volume: "+0%");
        var outputDir = Path.GetDirectoryName(alignedJsonPath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(alignedJsonPath);
        var outputPath = Path.Combine(outputDir, $"{baseName}_tts.mp3");

        var audioFiles = new List<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"arkplot_novel_tts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            bool isFirst = true;
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var text = segment.NovelText.Trim();

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // 请求间隔
                if (!isFirst)
                {
                    await Task.Delay(800);
                }
                isFirst = false;

                // 选择音色
                string voice;
                string label;
                if (segment.IsDialog)
                {
                    voice = tts.GetVoiceForGender(segment.Gender);
                    label = $"{segment.CharacterName ?? "未知"}({segment.Gender ?? "?"})";
                }
                else
                {
                    // 旁白用默认女声
                    voice = tts.GetVoiceForGender(null);
                    label = "旁白";
                }

                var tempFile = Path.Combine(tempDir, $"segment_{i:D4}.mp3");
                var textPreview = text.Length > 40 ? text[..40] + "..." : text;
                Console.WriteLine($"  [{i}] {label}: {textPreview}");
                Console.WriteLine($"      音色: {voice}");

                await tts.SynthesizeAsync(text, voice, tempFile);
                audioFiles.Add(tempFile);
            }

            if (audioFiles.Count == 0)
            {
                Console.WriteLine("  ⚠️ 没有可合成的片段");
                return;
            }

            // 合并音频
            Console.WriteLine($"\n  正在合并 {audioFiles.Count} 个音频文件...");
            MergeAudioFiles(audioFiles, outputPath);

            Console.WriteLine($"  ✅ 音频已生成: {outputPath}");
        }
        finally
        {
            // 清理临时文件
            foreach (var file in audioFiles)
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { }
            }
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// 合并多个 MP3 文件为单个 MP3 文件（复用 TtsService 的逻辑）
    /// </summary>
    private static void MergeAudioFiles(List<string> inputFiles, string outputFile)
    {
        using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);

        foreach (var inputPath in inputFiles)
        {
            using var reader = new NAudio.Wave.Mp3FileReader(inputPath);

            NAudio.Wave.Mp3Frame? frame;
            while ((frame = reader.ReadNextFrame()) != null)
            {
                outputStream.Write(frame.RawData, 0, frame.RawData.Length);
            }
        }
    }
}
