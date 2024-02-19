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
    public string Dialog { get; set; } = "";

    public List<string> Urls = new List<string>();

    public string CharacterName = "";

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
        Urls = new(entry.Urls);
        CharacterName = entry.CharacterName;
        Dialog = entry.CharacterName;
    }

    public FormattedTextEntry()
    {
    }
}
