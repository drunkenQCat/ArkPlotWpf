using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities;
using ArkPlot.Core.Utilities.ArknightsDbComponents;
using ArkPlot.Core.Utilities.WorkFlow;
using ArkPlot.Cli.Infrastructure;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// Step 7: 创建视觉客户端 + 运行 PicDesc。
/// </summary>
public static class PicDescRunner
{
    public static PicDescService? Run(
        AkpStoryLoader storyLoader, List<FormattedTextEntry> processedEntries)
    {
        Console.WriteLine("[7/8] 正在运行 MdReconstructor（填充 PicDesc）...");

        Func<string, Task<string>>? describeByUrl = null;
        VisionClientResult? visionClient = null;

        if (CliOptions.EnableVision)
        {
            try
            {
                visionClient = VisionClientFactory.Create();
                describeByUrl = visionClient?.DescribeByUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ 视觉客户端初始化失败：{ex.Message}");
            }
        }

        using var picDescService = new PicDescService(describeByUrl, debugMode: CliOptions.DebugMode);
        picDescService.InitializeCleanup();

        try
        {
            foreach (var plotManagerEntry in storyLoader.ContentTable)
            {
                var textList = plotManagerEntry.CurrentPlot.TextVariants;
                _ = new MdReconstructor(textList, picDescService);
            }
        }
        finally
        {
            visionClient?.Disposable?.Dispose();
        }

        var picDescEntries = processedEntries.Count(e => !string.IsNullOrWhiteSpace(e.PicDesc));
        Console.WriteLine($"    ✅ MdReconstructor 完成，PicDesc 条目：{picDescEntries}");
        return picDescService;
    }
}
