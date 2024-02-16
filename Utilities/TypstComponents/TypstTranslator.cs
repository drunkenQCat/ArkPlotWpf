namespace ArkPlotWpf.Utilities.TypstComponents;

public class TypstTranslator
{
    private string typCode = "#import \"typst-template/template.typ\": arknights_sim\r\n";
    private string chapterName = "";
    private string name = "";
    private string script = "";
    private string portrait = "";
    private string background = "";

    private string typDialogLine() =>
        $"#arknights_sim(\"{name}\", \"{script}\", image(\"{portrait}\"),image( \"{background}\"))\r\n";

    public void SetName(string inputName) => name = inputName;
    public void SetScript(string inputScript) => script = inputScript;
    public void SetPortrait(string inputPortrait) => portrait = inputPortrait;
    public void SetBackground(string inputBackground) => background = inputBackground;
    public void UpdateCode() => typCode += typDialogLine();
    public string GetTypCode => typCode;
}
