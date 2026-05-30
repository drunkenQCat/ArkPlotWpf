using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// Step 6: 文档解析 + Debug 模式 mock 注入。
/// </summary>
public static class DocumentParser
{
    public static async Task Parse(string tagsJsonPath, PlotManager plotManager, List<FormattedTextEntry> processedEntries)
    {
        Console.WriteLine("[6/8] 正在解析文档（AkpParser → PlotManager.StartParseLines）...");
        var parser = new AkpParser(tagsJsonPath);
        await plotManager.StartParseLines(parser);

        Console.WriteLine($"    解析完成，共 {processedEntries.Count} 个条目");
        Console.WriteLine($"    有效 MdText 条目：{processedEntries.Count(e => !string.IsNullOrWhiteSpace(e.MdText))}");
        Console.WriteLine($"    有效 TypText 条目：{processedEntries.Count(e => !string.IsNullOrWhiteSpace(e.TypText))}");
        Console.WriteLine($"    有 ResourceUrls 的条目：{processedEntries.Count(e => e.ResourceUrls.Count > 0)}");
    }

    public static void InjectMockResourceUrls(List<FormattedTextEntry> processedEntries)
    {
        var currentEntriesWithUrls = processedEntries.Count(e => e.ResourceUrls.Count > 0);
        if (currentEntriesWithUrls > 0) return;

        Console.WriteLine("    ⚠️ 无 ResourceUrls，注入 mock 数据用于验证 PicDesc 流程...");
        var mockUrls = new List<string>
        {
            "https://media.prts.wiki/a/ab/Avg_char_293_thorns_1.png",
            "https://media.prts.wiki/b/bc/Avg_bg_bg_med.png",
            "https://media.prts.wiki/c/cd/Avg_npc_009.png"
        };

        var mockCounter = 0;
        foreach (var entry in processedEntries)
        {
            if (entry.Type.Contains("char", StringComparison.OrdinalIgnoreCase) && mockCounter < mockUrls.Count)
            {
                entry.ResourceUrls = [mockUrls[mockCounter]];
                mockCounter++;
            }
            else if (entry.Type.Contains("background", StringComparison.OrdinalIgnoreCase) && mockCounter < mockUrls.Count)
            {
                entry.ResourceUrls = [mockUrls[mockCounter]];
                mockCounter++;
            }
        }

        var newCount = processedEntries.Count(e => e.ResourceUrls.Count > 0);
        Console.WriteLine($"    ✅ 已注入 {newCount} 条 mock ResourceUrls");
    }
}
