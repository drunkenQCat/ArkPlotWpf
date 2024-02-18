namespace ArkPlotWpf.Model;

public class FormattedTextEntry
{
    public int Index;
    public string OriginalText = "";
    public string MdText = "";
    public string TypText = "";
    public string Type = "";
    public StringDict CommandSet = new();
    public bool IsTagOnly { get; set; }
    public List<string> Urls = new List<string>();

    public string Dialog = "";
}