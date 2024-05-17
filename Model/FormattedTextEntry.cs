namespace ArkPlotWpf.Model;

public class FormattedTextEntry
{
    public int Index;
    public string OriginalText = "";
    public string MdText = "";
    public int MdDuplicateCounter;
    public string TypText = "";
    public string Type = "";
    public StringDict CommandSet = new();
    public bool IsTagOnly { get; set; }
    public string CharacterName = "";
    public string Dialog { get; set; } = "";

    public List<string> ResourceUrls = new();
    public PortraitInfo PortraitsInfo = new(new List<string>(), 0);
    public string Bg = "";

    public FormattedTextEntry(FormattedTextEntry entry)
    {
        Index = entry.Index;
        OriginalText = entry.OriginalText;
        MdText = entry.MdText;
        MdDuplicateCounter = entry.MdDuplicateCounter;
        TypText = entry.TypText;
        Type = entry.Type;
        CommandSet = new(entry.CommandSet);
        IsTagOnly = entry.IsTagOnly;
        ResourceUrls = new(entry.ResourceUrls);
        CharacterName = entry.CharacterName;
        Dialog = entry.CharacterName;
        Bg = entry.Bg;
        PortraitsInfo = entry.PortraitsInfo;
    }

    public FormattedTextEntry()
    {
    }
}
