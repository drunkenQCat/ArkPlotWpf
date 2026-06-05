using System.Text.Json;
using ArkPlot.Core.Model;
using ArkPlot.Cli.Infrastructure;
using ArkPlot.Tts;
using ArkPlot.Tts.Engines;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// Step 9: TTS 音频生成（主管线，处理原始 FormattedTextEntry）。
/// </summary>
public static class TtsRunner
{
    public static async Task RunAsync(
        List<FormattedTextEntry> processedEntries, string outputDir, string actName, string chapterName)
    {
        Console.WriteLine("[9/9] 正在生成 TTS 音频...");
        try
        {
            // 序列化 entries 为临时 JSON 供 TtsPipeline 消费
            var tempJson = Path.Combine(outputDir, $".tts_raw_{Guid.NewGuid():N}.json");
            var json = JsonSerializer.Serialize(
                processedEntries.Select(e => new { e.CharacterName, e.Dialog }),
                new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(tempJson, json);

            try
            {
                var cacheDir = Path.Combine(outputDir, "_tts_cache");
                var request = new TtsRequest(
                    TtsInputMode.RawEntries,
                    tempJson,
                    outputDir);

                var engine = new EdgeTtsEngine();
                var voices = new VoiceManager();
                var cache = new TtsCacheService(cacheDir);

                using var pipeline = new TtsPipeline(engine, voices, cache);
                var progress = new Progress<string>(msg => Console.WriteLine($"    {msg}"));

                var result = await pipeline.GenerateAsync(request, progress: progress);

                Console.WriteLine($"    🎵 输出文件: {result.OutputFiles.Count}");
            }
            finally
            {
                try { File.Delete(tempJson); } catch { }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("    ⚠️ TTS 生成已取消。");
        }
        catch (Exception ttsEx)
        {
            Console.WriteLine($"    ⚠️ TTS 生成失败：{ttsEx.Message}");
            Console.WriteLine("    提示：这不影响 Markdown 导出结果。");
        }
    }
}
