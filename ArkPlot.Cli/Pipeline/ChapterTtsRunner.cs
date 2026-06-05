using ArkPlot.Tts;
using ArkPlot.Tts.Alignment;
using ArkPlot.Tts.Engines;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// 按章节 TTS 命令：对小说化 md 文件按章节生成 MP3 音频。
/// </summary>
public static class ChapterTtsRunner
{
    public static async Task RunAsync(string novelFilePath, int? segmentLimit = null, bool debugVoice = false)
    {
        if (!File.Exists(novelFilePath))
        {
            Console.Error.WriteLine($"❌ 文件不存在: {novelFilePath}");
            return;
        }

        Console.WriteLine("=== Chapter TTS Generator ===");
        Console.WriteLine($"输入: {Path.GetFileName(novelFilePath)}");

        var outputDir = Path.GetDirectoryName(novelFilePath) ?? ".";
        var cacheDir = Path.Combine(outputDir, "_tts_cache");

        var request = new TtsRequest(
            TtsInputMode.NovelChapter,
            novelFilePath,
            outputDir,
            SegmentLimit: segmentLimit,
            DebugVoiceOnly: debugVoice);

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
