using ArkPlotWpf.Data.Entities;
using ArkPlotWpf.Model;
using System.Text.Json;
using System.Linq;

namespace ArkPlotWpf.Data.Mappers;

public static class PlotMapper
{
    public static PlotEntity ToEntity(this Plot model, long actId)
    {
        return new PlotEntity
        {
            Title = model.Title,
            Content = model.Content.ToString(),
            ActId = actId
        };
    }

    public static Plot ToModel(this PlotEntity entity, List<FormattedTextEntryEntity> textEntries)
    {
        var plot = new Plot(entity.Title, new StringBuilder(entity.Content));
        plot.TextVariants = textEntries.Select(x => x.ToModel()).ToList();
        return plot;

    }

    public static FormattedTextEntryEntity ToEntity(this FormattedTextEntry entry, long plotId)
    {
        return new FormattedTextEntryEntity
        {
            PlotId = plotId,
            IndexNo = entry.Index,
            OriginalText = entry.OriginalText,
            MdText = entry.MdText,
            MdDuplicateCounter = entry.MdDuplicateCounter,
            TypText = entry.TypText,
            Type = entry.Type,
            IsTagOnly = entry.IsTagOnly,
            CharacterName = entry.CharacterName,
            Dialog = entry.Dialog,
            PngIndex = entry.PngIndex,
            Bg = entry.Bg,
            MetadataJson = JsonSerializer.Serialize(new
            {
                entry.CommandSet,
                entry.ResourceUrls,
                entry.PortraitsInfo
            })
        };
    }

    public static FormattedTextEntry ToModel(this FormattedTextEntryEntity entity)
    {
        var meta = JsonSerializer.Deserialize<FormattedTextEntryMetadata>(entity.MetadataJson) ?? new();

        return new FormattedTextEntry
        {
            Index = entity.IndexNo,
            OriginalText = entity.OriginalText,
            MdText = entity.MdText,
            MdDuplicateCounter = entity.MdDuplicateCounter,
            TypText = entity.TypText,
            Type = entity.Type,
            IsTagOnly = entity.IsTagOnly,
            CharacterName = entity.CharacterName,
            Dialog = entity.Dialog,
            PngIndex = entity.PngIndex,
            Bg = entity.Bg,
            CommandSet = meta.CommandSet,
            ResourceUrls = meta.ResourceUrls,
            PortraitsInfo = meta.PortraitsInfo
        };
    }
}

public class FormattedTextEntryMetadata
{
    public StringDict CommandSet { get; set; } = new();
    public List<string> ResourceUrls { get; set; } = new();
    public PortraitInfo PortraitsInfo { get; set; } = new([], 0);
}
