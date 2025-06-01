namespace ArkPlotWpf.Data.Entities;

public class FormattedTextEntryEntity
{
    public long PlotId { get; set; }
    public int IndexNo { get; set; }
    public string OriginalText { get; set; } = "";
    public string MdText { get; set; } = "";
    public int MdDuplicateCounter { get; set; }
    public string TypText { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsTagOnly { get; set; }
    public string CharacterName { get; set; } = "";
    public string Dialog { get; set; } = "";
    public int PngIndex { get; set; }
    public string Bg { get; set; } = "";
    public string MetadataJson { get; set; } = "";
}
