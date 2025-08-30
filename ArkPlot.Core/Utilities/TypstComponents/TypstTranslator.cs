namespace ArkPlot.Core.Utilities.TypstComponents;

// 这个类是用来将对话转换为Typst代码的。这�?types 的代码是用来模拟明日方舟 avg 界面�?
public class TypstTranslator
{
    public readonly string ChapterName;

    private string background = "";

    // �?avg 画面分为4个部分。分别是对话的名字、对话的内容、对话的背景图、对话的人物图�?
    private string name = "";
    private string portrait = "";
    private string portrait2 = "";
    private string script = "";

    private TypstTranslator(string name)
    {
        ChapterName = name;
    }

    // 这个类的输出端口，用来获取Typst代码�?
    public string TypCode { get; private set; } = "#import \"typst-template/template.typ\": arknights_sim\r\n";

    // 用来生成单个立绘�?typ 的代码�?
    private string TypDialogLine()
    {
        return $@"
#arknights_sim(
  {name},
  {script},
  image(
    {portrait},
    height: 150%
  ),
  image(
    {background},
    width: 120%
  )
)
";
    }

    // 用来生成两个立绘�?typ 的代码�?
    private string TypDialogLineWithTwoPortraits()
    {
        return @$"
#arknights_sim_2p(
  {name},
  {script},
  image(
    {portrait},
    height: 150%
  ),
  image(
    {portrait2},
    height: 150%
  ),
  image(
    {background},
    width: 120%
  )
)";
    }

    // 这些方法用来设置对话的名字、对话的内容、对话的背景图等等�?
    public void SetName(string inputName)
    {
        name = inputName;
    }

    public void SetScript(string inputScript)
    {
        script = inputScript;
    }

    public void SetPortrait(string inputPortrait)
    {
        portrait = inputPortrait;
    }

    public void SetPortrait2(string inputPortrait)
    {
        portrait2 = inputPortrait;
    }

    public void SetBackground(string inputBackground)
    {
        background = inputBackground;
    }

    // 这个类还有一个方法，用来更新Typst代码�?
    public void UpdateCode()
    {
        TypCode += string.IsNullOrEmpty(portrait2) ? TypDialogLine() : TypDialogLineWithTwoPortraits();
        name = "";
        script = "";
    }
}
