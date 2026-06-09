using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Tts;
using ArkPlot.Tts.Alignment;
using ArkPlot.Tts.Engines;
using NAudio.Wave;
using SqlSugar;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// verify-tts 命令：端到端验证整套 TTS 工作流。
/// 用法: verify-tts <output_dir> [--segments N]
/// </summary>
public static class VerifyTtsRunner
{
    public static async Task RunAsync(string outputDir, int segmentLimit = 3)
    {
        var report = new VerificationReport();

        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║     TTS 工作流端到端验证 (CLI)           ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine($"输出目录: {outputDir}");
        Console.WriteLine($"片段限制: {segmentLimit}");
        Console.WriteLine();

        // ── Step 1: 扫描小说文件 ──
        Console.WriteLine("── Step 1: 扫描小说文件 ──");
        var novelFiles = Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "*_novel_*.md")
            : [];
        Console.WriteLine($"  找到 {novelFiles.Length} 个小说文件");
        foreach (var f in novelFiles)
            Console.WriteLine($"    {Path.GetFileName(f)} ({new FileInfo(f).Length / 1024}KB)");

        if (novelFiles.Length == 0)
        {
            Console.WriteLine("  ❌ 无小说文件，终止");
            report.Fail("扫描小说文件");
            report.Print();
            return;
        }

        // 选最小的
        var selectedFile = novelFiles.OrderBy(f => new FileInfo(f).Length).First();
        Console.WriteLine($"  ✅ 选中: {Path.GetFileName(selectedFile)}");
        report.Pass("扫描小说文件");

        // ── Step 2: 对齐 ──
        Console.WriteLine("\n── Step 2: 对齐 (NovelAligner) ──");
        List<AlignmentEntry> entries;
        AlignmentStats stats;
        try
        {
            var aligner = new NovelAligner();
            (entries, stats) = await aligner.AlignByFileNameAsync(selectedFile);
            Console.WriteLine($"  章节: {stats.TotalNovelChapters}");
            Console.WriteLine($"  对话: {stats.AlignedDialogs}/{stats.TotalDialogs} 已对齐");
            Console.WriteLine($"  未对齐: {stats.UnalignedDialogs}");
            Console.WriteLine($"  总片段: {entries.Count}");

            if (stats.AlignedDialogs == 0)
            {
                Console.WriteLine("  ❌ 零对齐，可能活动名不匹配或 DB 无数据");
                report.Fail("对齐");
                report.Print();
                return;
            }
            Console.WriteLine("  ✅ 对齐成功");
            report.Pass("对齐", $"{stats.AlignedDialogs}/{stats.TotalDialogs}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 对齐失败: {ex.Message}");
            report.Fail("对齐", ex.Message);
            report.Print();
            return;
        }

        // ── Step 3: 章节/片段提取 + 音色分配 ──
        Console.WriteLine("\n── Step 3: 章节提取 + 音色分配 ──");
        var chapters = entries
            .GroupBy(e => e.ChapterTitle)
            .OrderBy(g => entries.IndexOf(g.First()))
            .ToList();
        Console.WriteLine($"  章节数: {chapters.Count}");
        foreach (var ch in chapters.Take(5))
            Console.WriteLine($"    {ch.Key} ({ch.Count()} 段)");

        var voiceManager = new VoiceManager();
        var voiceAssignments = entries
            .Where(e => e.IsDialog && !string.IsNullOrEmpty(e.CharacterName))
            .GroupBy(e => e.CharacterName!)
            .Select(g => new
            {
                Name = g.Key,
                Gender = g.First().Gender ?? "?",
                Voice = voiceManager.GetVoiceForCharacter(g.Key, g.First().Gender),
                Count = g.Count()
            })
            .OrderByDescending(v => v.Count)
            .ToList();

        Console.WriteLine($"  角色音色分配: {voiceAssignments.Count} 个角色");
        foreach (var v in voiceAssignments.Take(10))
            Console.WriteLine($"    {v.Name} ({v.Gender}) → {v.Voice.Replace("Neural", "")} [{v.Count}段]");
        Console.WriteLine($"  旁白 → {voiceManager.GetNarratorVoice().Replace("Neural", "")}");
        Console.WriteLine("  ✅ 音色分配完成");
        report.Pass("音色分配", $"{voiceAssignments.Count}角色");

        // ── Step 4: 文本清洗 ──
        Console.WriteLine("\n── Step 4: 文本清洗 (TextSanitizer) ──");
        var testCases = new (string input, string label)[]
        {
            ("<p>你好<b>世界</b></p>", "HTML标签"),
            ("![图片](http://img.png)", "Markdown图片"),
            ("[链接](http://url.com)", "Markdown链接"),
            ("`code`文本", "内联代码"),
            ("**加粗**和*斜体*", "加粗斜体"),
            (new string('A', 3000), "超长截断"),
            ("", "空输入"),
        };
        foreach (var (input, label) in testCases)
        {
            var result = TextSanitizer.Sanitize(input);
            Console.WriteLine($"  {label}: \"{input[..Math.Min(30, input.Length)]}...\" → \"{result[..Math.Min(30, result.Length)]}\"");
        }
        Console.WriteLine("  ✅ 文本清洗正常");
        report.Pass("文本清洗");

        // ── Step 5: 单段 TTS 合成 (真实 EdgeTTS 调用) ──
        Console.WriteLine("\n── Step 5: 单段 TTS 合成 (EdgeTTS 真实调用) ──");
        var ttsDir = Path.Combine(outputDir, "tts");
        var cacheDir = Path.Combine(ttsDir, "_tts_cache");
        Directory.CreateDirectory(ttsDir);
        Directory.CreateDirectory(cacheDir);

        var cache = new TtsCacheService(cacheDir);
        var engine = new EdgeTtsEngine();

        // 取第一个章节的前 N 段
        var firstChapter = chapters.First();
        var testSegments = firstChapter
            .Where(e => !string.IsNullOrWhiteSpace(e.NovelText))
            .Take(segmentLimit)
            .ToList();

        Console.WriteLine($"  测试章节: {firstChapter.Key}");
        Console.WriteLine($"  测试片段: {testSegments.Count} 段");

        var tempFiles = new List<string>();
        var ct = CancellationToken.None;
        int synthOk = 0, cacheHit = 0, synthFail = 0;

        for (int i = 0; i < testSegments.Count; i++)
        {
            var seg = testSegments[i];
            var text = TextSanitizer.Sanitize(seg.NovelText);
            var voice = seg.IsDialog
                ? voiceManager.GetVoiceForCharacter(seg.CharacterName ?? "", seg.Gender)
                : voiceManager.GetNarratorVoice();

            var cacheKey = TtsCacheService.GetCacheKey(text, voice);
            var (hit, cachedPath) = cache.TryGetCachedAudio(cacheKey);

            var tempFile = Path.Combine(ttsDir, $"verify_seg_{i:D3}.mp3");

            if (hit)
            {
                Console.WriteLine($"  [{i + 1}] 📦 缓存命中 | {voice.Replace("Neural", "")} | {text[..Math.Min(20, text.Length)]}");
                File.Copy(cachedPath!, tempFile, true);
                cacheHit++;
            }
            else
            {
                Console.Write($"  [{i + 1}] 🎤 {voice.Replace("Neural", "")} | {text[..Math.Min(30, text.Length)]}... ");
                try
                {
                    await engine.SynthesizeAsync(text, voice, tempFile, cancellationToken: ct);
                    cache.SaveToCache(cacheKey, tempFile);
                    Console.WriteLine("✅");
                    synthOk++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ {ex.Message[..Math.Min(50, ex.Message.Length)]}");
                    synthFail++;
                    continue;
                }
            }

            tempFiles.Add(tempFile);
        }

        Console.WriteLine($"  合成: {synthOk} 缓存: {cacheHit} 失败: {synthFail}");
        if (tempFiles.Count == 0)
        {
            Console.WriteLine("  ❌ 无有效片段");
            report.Fail("TTS合成", "无有效片段");
            report.Print();
            return;
        }
        report.Pass("TTS合成", $"{synthOk}合成+{cacheHit}缓存, {synthFail}失败");

        // ── Step 6: MP3 合并 ──
        Console.WriteLine("\n── Step 6: MP3 合并 (AudioMerger) ──");
        var mergedFile = Path.Combine(ttsDir, "verify_merged.mp3");
        try
        {
            AudioMerger.MergeFiles(tempFiles, mergedFile);
            var mergedSize = new FileInfo(mergedFile).Length;
            var mergedFrames = AudioMerger.CountFrames(mergedFile);
            Console.WriteLine($"  合并文件: {Path.GetFileName(mergedFile)} ({mergedSize / 1024}KB, {mergedFrames}帧)");
            Console.WriteLine("  ✅ MP3 合并成功");
            report.Pass("MP3合并", $"{mergedFrames}帧, {mergedSize / 1024}KB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ MP3 合并失败: {ex.Message}");
            report.Fail("MP3合并", ex.Message);
            report.Print();
            return;
        }

        // ── Step 7: NAudio 播放验证 ──
        Console.WriteLine("\n── Step 7: NAudio 播放验证 ──");
        try
        {
            using var reader = new AudioFileReader(mergedFile);
            Console.WriteLine($"  时长: {reader.TotalTime.TotalSeconds:F1}s");
            Console.WriteLine($"  采样率: {reader.WaveFormat.SampleRate}Hz");
            Console.WriteLine($"  通道: {reader.WaveFormat.Channels}");
            Console.WriteLine($"  位深: {reader.WaveFormat.BitsPerSample}bit");

            // 读取前 1 秒验证数据有效
            var buffer = new float[reader.WaveFormat.SampleRate]; // 1秒
            var samplesRead = reader.Read(buffer, 0, buffer.Length);
            var maxAmplitude = buffer.Take(samplesRead).Max(Math.Abs);
            Console.WriteLine($"  前1秒最大振幅: {maxAmplitude:F4}");

            if (maxAmplitude < 0.001f)
            {
                Console.WriteLine("  ❌ 音频几乎静音");
                report.Fail("NAudio播放", "音频几乎静音");
            }
            else
            {
                Console.WriteLine("  ✅ 音频数据有效");
                report.Pass("NAudio播放", $"{reader.TotalTime.TotalSeconds:F1}s, 振幅{maxAmplitude:F4}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ NAudio 播放失败: {ex.Message}");
            report.Fail("NAudio播放", ex.Message);
        }

        // ── Step 8: 背景图提取 ──
        Console.WriteLine("\n── Step 8: 背景图提取 (charslot ResourceUrls) ──");
        try
        {
            var db = DbFactory.GetClient();
            // SqlSugar 不支持 JSON 列的 .Count > 0 在 WHERE 中，先查再过滤
            var allCharSlots = await db.Queryable<FormattedTextEntry>()
                .Where(e => e.Type == "charslot")
                .OrderBy(e => e.Index)
                .ToListAsync();

            var charSlots = allCharSlots
                .Where(e => e.ResourceUrls != null && e.ResourceUrls.Count > 0)
                .Take(10)
                .ToList();

            Console.WriteLine($"  charslot 带背景图: {charSlots.Count} 条 (前10)");
            foreach (var cs in charSlots.Take(5))
            {
                var url = cs.ResourceUrls.FirstOrDefault() ?? "";
                var shortUrl = url.Length > 60 ? url[..57] + "..." : url;
                Console.WriteLine($"    [{cs.Index}] {shortUrl}");
            }

            if (charSlots.Count > 0)
            {
                Console.WriteLine("  ✅ 背景图提取正常");
                report.Pass("背景图提取", $"{charSlots.Count}条");
            }
            else
            {
                Console.WriteLine("  ⚠️ 无背景图数据（可能 DB 未填充）");
                report.Warn("背景图提取", "无数据");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 背景图提取失败: {ex.Message}");
            report.Fail("背景图提取", ex.Message);
        }

        // ── Step 9: 立绘加载 ──
        Console.WriteLine("\n── Step 9: 立绘加载 (FormattedTextEntry.Portraits) ──");
        try
        {
            var db = DbFactory.GetClient();
            // SqlSugar 不支持 JSON 列的 .Count > 0 在 WHERE 中，先查再过滤
            var allEntries = await db.Queryable<FormattedTextEntry>()
                .Take(500)
                .ToListAsync();

            var withPortraits = allEntries
                .Where(e => e.Portraits != null && e.Portraits.Count > 0)
                .Take(10)
                .ToList();

            Console.WriteLine($"  带立绘的条目: {withPortraits.Count} 条 (前10)");
            foreach (var p in withPortraits.Take(5))
            {
                var portrait = p.Portraits.FirstOrDefault() ?? "";
                var shortUrl = portrait.Length > 60 ? portrait[..57] + "..." : portrait;
                Console.WriteLine($"    [{p.Index}] {p.CharacterName ?? "?"} → {shortUrl}");
            }

            if (withPortraits.Count > 0)
            {
                Console.WriteLine("  ✅ 立绘加载正常");
                report.Pass("立绘加载", $"{withPortraits.Count}条");
            }
            else
            {
                Console.WriteLine("  ⚠️ 无立绘数据（可能 DB 未填充）");
                report.Warn("立绘加载", "无数据");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 立绘加载失败: {ex.Message}");
            report.Fail("立绘加载", ex.Message);
        }

        // ── Step 10: 缓存验证 ──
        Console.WriteLine("\n── Step 10: 缓存验证 ──");
        var cachedCount = cache.CachedFileCount;
        Console.WriteLine($"  缓存文件数: {cachedCount}");

        // 验证缓存命中
        var recheckKey = TtsCacheService.GetCacheKey(
            TextSanitizer.Sanitize(testSegments.First().NovelText),
            testSegments.First().IsDialog
                ? voiceManager.GetVoiceForCharacter(testSegments.First().CharacterName ?? "", testSegments.First().Gender)
                : voiceManager.GetNarratorVoice());
        var (recheck, _) = cache.TryGetCachedAudio(recheckKey);
        Console.WriteLine($"  二次查询缓存: {(recheck ? "✅ 命中" : "❌ 未命中")}");
        report.Pass("缓存验证", $"{cachedCount}文件, 二次查询{(recheck ? "命中" : "未命中")}");

        // ── 清理临时文件 ──
        foreach (var f in tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
        try { File.Delete(mergedFile); } catch { }

        // ── 汇总 ──
        report.Print();
    }
}

/// <summary>验证报告。</summary>
internal class VerificationReport
{
    private readonly List<(string Step, string Status, string Detail)> _items = [];

    public void Pass(string step, string detail = "") => _items.Add((step, "✅ PASS", detail));
    public void Fail(string step, string detail = "") => _items.Add((step, "❌ FAIL", detail));
    public void Warn(string step, string detail = "") => _items.Add((step, "⚠️ WARN", detail));

    public void Print()
    {
        Console.WriteLine("\n╔══════════════════════════════════════════╗");
        Console.WriteLine("║          验证报告汇总                     ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");

        int pass = 0, fail = 0, warn = 0;
        foreach (var (step, status, detail) in _items)
        {
            var detailStr = string.IsNullOrEmpty(detail) ? "" : $" ({detail})";
            Console.WriteLine($"  {status}  {step}{detailStr}");
            if (status.Contains("PASS")) pass++;
            else if (status.Contains("FAIL")) fail++;
            else warn++;
        }

        Console.WriteLine($"\n  总计: {pass} 通过 / {fail} 失败 / {warn} 警告");
        Console.WriteLine(fail == 0 ? "  🎉 全部通过！" : "  💥 有失败项，请检查");
    }
}
