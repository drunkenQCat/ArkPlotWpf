using ArkPlot.Core.Utilities.ArknightsDbComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// Step 3: 下载章节内容。
/// </summary>
public static class ChapterDownloader
{
    public static async Task<PlotManager?> DownloadAsync(AkpStoryLoader storyLoader, string chapterName)
    {
        Console.WriteLine("[3/6] 正在下载章节内容...");
        await storyLoader.GetAllChapters(new[] { chapterName });

        if (storyLoader.ContentTable.Count == 0)
        {
            Console.WriteLine("❌ 未成功下载任何内容。请检查网络连接（需要访问 GitHub）。");
            return null;
        }

        var plotManager = storyLoader.ContentTable[0];
        var rawContentLength = plotManager.CurrentPlot.Content.Length;
        var variantCount = plotManager.CurrentPlot.TextVariants.Count;
        Console.WriteLine($"    原始内容长度：{rawContentLength} 字符");
        Console.WriteLine($"    TextVariants：{variantCount} 条");

        if (rawContentLength == 0 && variantCount == 0)
        {
            Console.WriteLine("⚠️ 章节内容为空，可能是网络问题导致下载失败。");
            return null;
        }

        if (rawContentLength == 0 && variantCount > 0)
            Console.WriteLine("    ℹ️ 使用 DB 缓存（跳过下载和解析）");

        return plotManager;
    }
}
