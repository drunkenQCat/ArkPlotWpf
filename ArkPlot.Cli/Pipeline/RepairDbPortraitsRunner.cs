using System.Text.Json;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;

namespace ArkPlot.Cli.Pipeline;

public static class RepairDbPortraitsRunner
{
    private const string TransparentPortrait = "https://pics/transparent.png";

    public static async Task<int> RunAsync(
        string? dbPath,
        long? plotId = null,
        bool dryRun = false,
        string? outPath = null,
        int show = 20)
    {
        if (!string.IsNullOrEmpty(dbPath))
        {
            DbFactory.ConfigureForTesting($"Data Source={dbPath}");
        }

        var db = DbFactory.GetClient();

        var query = db.Queryable<FormattedTextEntry>();
        if (plotId.HasValue)
            query = query.Where(e => e.PlotId == plotId.Value);

        var entries = await query
            .OrderBy("PlotId, [Index], Id")
            .ToListAsync();

        var toUpdate = new List<FormattedTextEntry>();
        var preview = new List<PreviewItem>(Math.Min(show, 256));
        StreamWriter? outWriter = null;
        if (!string.IsNullOrEmpty(outPath))
        {
            var directory = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            outWriter = new StreamWriter(outPath);
        }

        long currentPlot = -1;
        bool blocked = false;

        foreach (var e in entries)
        {
            if (e.PlotId != currentPlot)
            {
                currentPlot = e.PlotId;
                blocked = false;
            }

            var tagOnly = GetTagOnlyDirective(e.OriginalText);
            if (tagOnly != null)
            {
                var beforeType = e.Type;
                var beforePortrait0 = e.Portraits.Count > 0 ? e.Portraits[0] : "";
                blocked = true;
                e.IsTagOnly = true;
                e.Type = tagOnly;
                e.CommandSet = new() { ["type"] = tagOnly };
                e.CharacterCode = null;
                e.Portraits = [TransparentPortrait];
                e.PortraitFocus = 0;
                toUpdate.Add(e);
                Record(new PreviewItem(
                    e.PlotId,
                    e.Index,
                    e.Id,
                    $"tag-only:{tagOnly}",
                    e.OriginalText,
                    beforeType,
                    e.Type,
                    beforePortrait0,
                    TransparentPortrait));
                continue;
            }

            var isPortraitCommand = IsEffectivePortraitCommand(e);
            if (isPortraitCommand)
            {
                blocked = false;
                continue;
            }

            if (blocked)
            {
                if (e.Portraits.Count != 1 || !string.Equals(e.Portraits[0], TransparentPortrait, StringComparison.Ordinal) || e.PortraitFocus != 0)
                {
                    var beforeType = e.Type;
                    var beforePortrait0 = e.Portraits.Count > 0 ? e.Portraits[0] : "";
                    e.Portraits = [TransparentPortrait];
                    e.PortraitFocus = 0;
                    toUpdate.Add(e);
                    Record(new PreviewItem(
                        e.PlotId,
                        e.Index,
                        e.Id,
                        "blocked-after-tag-only",
                        e.OriginalText,
                        beforeType,
                        e.Type,
                        beforePortrait0,
                        TransparentPortrait));
                }
            }
        }

        outWriter?.Dispose();

        Console.WriteLine($"RepairDbPortraits: scanned={entries.Count}, update={toUpdate.Count}, plotFilter={(plotId?.ToString() ?? "(all)")}, dryRun={dryRun}");
        if (!string.IsNullOrEmpty(outPath))
            Console.WriteLine($"RepairDbPortraits: previewFile={outPath}");
        if (preview.Count > 0)
        {
            Console.WriteLine($"RepairDbPortraits: preview(show={preview.Count})");
            foreach (var item in preview)
            {
                Console.WriteLine(
                    $"PlotId={item.PlotId} Index={item.Index} Id={item.Id} Reason={item.Reason} Type={item.TypeBefore}->{item.TypeAfter} Portrait0={item.Portrait0Before}->{item.Portrait0After} Text={item.OriginalText}");
            }
        }

        if (dryRun || toUpdate.Count == 0)
            return 0;

        await db.Updateable(toUpdate)
            .UpdateColumns(e => new { e.Type, e.CommandSet, e.IsTagOnly, e.CharacterCode, e.Portraits, e.PortraitFocus })
            .ExecuteCommandAsync();

        return 0;

        void Record(PreviewItem item)
        {
            if (preview.Count < show)
                preview.Add(item);
            if (outWriter != null)
                outWriter.WriteLine(JsonSerializer.Serialize(item));
        }
    }

    private static bool IsEffectivePortraitCommand(FormattedTextEntry e)
    {
        if (string.IsNullOrEmpty(e.Type))
            return false;
        if (e.IsTagOnly)
            return false;
        return e.Type is "character" or "charslot" or "charactercutin";
    }

    private record PreviewItem(
        long PlotId,
        int Index,
        long Id,
        string Reason,
        string OriginalText,
        string TypeBefore,
        string TypeAfter,
        string Portrait0Before,
        string Portrait0After);

    private static string? GetTagOnlyDirective(string originalText)
    {
        var t = (originalText ?? "").Trim();
        if (t.Length < 2 || t[0] != '[' || t[^1] != ']')
            return null;

        t = t[1..^1].Trim();
        if (t.Length == 0)
            return null;

        if (t.Equals("charslot", StringComparison.OrdinalIgnoreCase))
            return "charslot";
        if (t.Equals("character", StringComparison.OrdinalIgnoreCase))
            return "character";

        return null;
    }
}

