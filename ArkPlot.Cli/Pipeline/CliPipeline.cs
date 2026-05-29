using ArkPlot.Cli.Dump;
using ArkPlot.Cli.Infrastructure;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// CLI 纯编排器：按顺序调用各 Step，传递中间结果。
/// </summary>
public class CliPipeline
{
    private readonly string _tagsJsonPath;

    public CliPipeline(string tagsJsonPath)
    {
        _tagsJsonPath = tagsJsonPath;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("=== ArkPlot CLI - 完整流程验证 ===");
        Console.WriteLine($"目标：简中 ACTIVITY_STORY 第一个活动 → 第一章 → 完整解析流程 → Dump JSON → 导出 Markdown");
        Console.WriteLine($"Debug 模式：{(CliOptions.DebugMode ? "开启（强制重写 PicDesc）" : "关闭")}");
        Console.WriteLine($"Vision 模式：{(CliOptions.EnableVision ? "开启（Ollama 生成真实图片描述）" : "关闭（使用占位符）")}");
        Console.WriteLine($"TTS 模式：{(CliOptions.EnableTts ? "开启（EdgeTTS 生成章节音频）" : "关闭")}");
        Console.WriteLine();

        // 1-2. 加载活动 & 获取第一章
        var (_, _, actName, storyLoader) = await ActivityLoader.LoadFirstActivityAsync();
        var firstChapter = await ActivityLoader.GetFirstChapterAsync(storyLoader);
        if (firstChapter == null) return;

        // 3. 下载章节内容
        var plotManager = await ChapterDownloader.DownloadAsync(storyLoader, firstChapter);
        if (plotManager == null) return;

        var processedEntries = plotManager.CurrentPlot.TextVariants;

        // 4-5. 加载 Prts 资源索引
        await ResourceLoader.LoadAsync(storyLoader, plotManager, processedEntries);

        // 6. 解析文档
        DocumentParser.Parse(_tagsJsonPath, plotManager, processedEntries);

        // 6.5 Debug 模式 mock 注入
        if (CliOptions.DebugMode)
            DocumentParser.InjectMockResourceUrls(processedEntries);

        // 7. 运行 PicDesc
        using var picDescService = PicDescRunner.Run(storyLoader, processedEntries);
        if (picDescService == null) return;
        var picDescStats = picDescService.GetStats();

        // 8. Dump JSON
        Console.WriteLine("[7/8] 正在 Dump JSON（导出前验证）...");
        var dumpResult = DumpService.DumpPlotToJson(plotManager.CurrentPlot, actName, firstChapter);
        Console.WriteLine($"    ✅ 已保存：{dumpResult.DumpPath}");
        Console.WriteLine($"    📊 统计：{dumpResult.Stats}");
        Console.WriteLine($"    🖼️  PicDesc 条目：{dumpResult.PicDescCount}");

        // 8. 导出 Markdown
        var (outputDir, markdown) = MarkdownExporter.Export(storyLoader, picDescService, actName);

        Console.WriteLine($"    ✅ Markdown 已保存：{outputDir}");
        Console.WriteLine($"    📄 文件大小：{markdown.Content.Length} 字符");
        Console.WriteLine($"    🗄️  PicDesc 数据库：{picDescStats.DbCount} 条记录");
        Console.WriteLine($"    📁 图片缓存目录：{picDescStats.CacheFileCount} 个文件，{picDescStats.CacheSizeBytes / 1024} KB");

        // 9. TTS
        if (CliOptions.EnableTts)
            await TtsRunner.RunAsync(processedEntries, outputDir, actName, firstChapter);

        // 汇总
        Console.WriteLine("\n=== 流程完成 ===");
        Console.WriteLine($"活动：{actName}");
        Console.WriteLine($"章节：{firstChapter}");
        Console.WriteLine($"Dump JSON：{dumpResult.DumpPath}");
        Console.WriteLine($"Markdown：{Path.Combine(outputDir, $"{markdown.Title}.md")}");
        Console.WriteLine($"PicDesc 数据库：{picDescStats.DbCount} 条记录");
    }
}
