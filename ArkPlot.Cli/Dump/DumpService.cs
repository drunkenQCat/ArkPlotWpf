using ArkPlot.Arknights;

using ArkPlot.Core.Model;
using ArkPlot.Cli.Infrastructure;
using Newtonsoft.Json;

namespace ArkPlot.Cli.Dump;

/// <summary>
/// 按 Model 定义 dump Plot 对象为 JSON，用于导出前验证。
/// </summary>
public static class DumpService
{
    public static DumpResult DumpPlotToJson(Plot plot, string actName, string chapterName)
    {
        var picDescCount = plot.TextVariants.Count(e => !string.IsNullOrWhiteSpace(e.PicDesc));

        var entries = plot.TextVariants.Cast<ArkPlot.Arknights.FormattedTextEntry>().ToList();
        var dump = new PlotDump
        {
            Meta = new DumpMeta
            {
                Activity = actName,
                Chapter = chapterName,
                Title = plot.Title,
                DumpTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TotalEntries = entries.Count,
                ValidMdEntries = entries.Count(e => !string.IsNullOrWhiteSpace(e.MdText)),
                ValidTypEntries = entries.Count(e => !string.IsNullOrWhiteSpace(e.TypText))
            },
            TextVariants = entries.Select(entry => new FormattedTextEntryDump
            {
                Index = entry.Index,
                OriginalText = entry.OriginalText,
                MdText = entry.MdText,
                MdDuplicateCounter = entry.MdDuplicateCounter,
                TypText = entry.TypText,
                Type = entry.Type,
                IsTagOnly = entry.IsTagOnly,
                CharacterName = entry.CharacterName,
                CharacterCode = entry.CharacterCode,
                Dialog = entry.Dialog,
                PngIndex = entry.PngIndex,
                Bg = entry.Bg,
                ResourceUrls = entry.ResourceUrls,
                Portraits = entry.Portraits,
                PortraitFocus = entry.PortraitFocus,
                CommandSet = entry.CommandSet,
                PicDesc = entry.PicDesc
            }).ToList()
        };

        var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cli_output");
        Directory.CreateDirectory(outputDir);

        var fileName = FileHelper.SanitizeFileName($"{actName}_{chapterName}");
        var dumpPath = Path.Combine(outputDir, $"{fileName}_dump.json");

        var jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        var json = JsonConvert.SerializeObject(dump, jsonSettings);
        File.WriteAllText(dumpPath, json, System.Text.Encoding.UTF8);

        var stats = $"Total={dump.Meta.TotalEntries}, Md={dump.Meta.ValidMdEntries}, Typ={dump.Meta.ValidTypEntries}";
        return new DumpResult(dumpPath, stats, picDescCount);
    }
}
