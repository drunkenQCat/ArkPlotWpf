using ArkPlot.Tts;
using ArkPlot.Tts.Engines;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// 小说化文本 TTS 命令：读取对齐后的 JSON，生成单个 MP3。
/// </summary>
public static class NovelTtsRunner
{
    public static async Task RunAsync(string alignedJsonPath, int? segmentLimit = null)
    {
        if (!File.Exists(alignedJsonPath))
        {
            Console.Error.WriteLine($"❌ 文件不存在: {alignedJsonPath}");
            return;
        }

        Console.WriteLine("=== Novel TTS Runner ===");
        Console.WriteLine($"输入: {Path.GetFileName(alignedJsonPath)}");

        var outputDir = Path.GetDirectoryName(alignedJsonPath) ?? ".";
        var cacheDir = Path.Combine(outputDir, "_tts_cache");

        var request = new TtsRequest(
            TtsInputMode.AlignedJson,
            alignedJsonPath,
            outputDir,
            SegmentLimit: segmentLimit);

        var engine = new EdgeTtsEngine();
        var voices = new VoiceManager();
        var cache = new TtsCacheService(cacheDir);

        using var pipeline = new TtsPipeline(engine, voices, cache);
        var progress = new Progress<string>(msg => Console.WriteLine(msg));

        var result = await pipeline.GenerateAsync(request, progress: progress);

        Console.WriteLine($"\n📊 生成统计:");
        Console.WriteLine($"   总片段:   {result.TotalSegments}");
        Console.WriteLine($"   输出文件: {result.OutputFiles.Count}");

        foreach (var f in result.OutputFiles)
            Console.WriteLine($"   {Path.GetFileName(f)}");
    }
}
