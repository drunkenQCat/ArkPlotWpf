using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.ArknightsDbComponents;
using ArkPlot.Core.Utilities.PrtsComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// Step 4-5: 加载 Prts 资源索引 + 预加载资源。
/// </summary>
public static class ResourceLoader
{
    public static async Task LoadAsync(
        AkpStoryLoader storyLoader, PlotManager plotManager, List<FormattedTextEntry> processedEntries)
    {
        Console.WriteLine("[4/8] 正在加载 Prts 资源索引...");
        var prts = new PrtsDataProcessor();
        var prtsLoaded = false;
        try
        {
            await prts.EnsureSyncedAsync();
            prtsLoaded = true;
            Console.WriteLine("    Prts 资源索引加载完成");
        }
        catch (Exception prtsEx)
        {
            Console.WriteLine($"    ⚠️ Prts 索引加载失败（{prtsEx.Message}），跳过 ResourceUrls 填充");
        }

        Console.WriteLine("[5/8] 正在预加载资源（填充 ResourceUrls 等字段）...");
        int entriesWithUrls = 0;
        int preloadCount = 0;

        if (prtsLoaded)
        {
            var preloadInfo = storyLoader.GetPreloadInfo();
            preloadCount = preloadInfo.Count;
            entriesWithUrls = processedEntries.Count(e => e.ResourceUrls.Count > 0);
        }
        else
        {
            try
            {
                var preloader = new PrtsPreloader(plotManager);
                preloader.ParseAndCollectAssets();
                entriesWithUrls = processedEntries.Count(e => e.ResourceUrls.Count > 0);
                preloadCount = preloader.Assets.Count;
            }
            catch
            {
                // 优雅降级
            }
        }

        Console.WriteLine($"    资源条目：{preloadCount}");
        Console.WriteLine($"    有 ResourceUrls 的条目：{entriesWithUrls}");
    }
}
