using ArkPlotWpf.Model;
using System;
using System.Linq;
using ArkPlotWpf.Services;
using ArkPlotWpf.Utilities.WorkFlow;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public class PlotManager
{
    public Plot CurrentPlot { get; }
    private AkpParser? Parser { get; set; }

    private readonly NotificationBlock _noticeBlock = NotificationBlock.Instance;

    public PlotManager(string title, StringBuilder content)
    {
        CurrentPlot = new Plot(title, content);
    }


    public void InitializePlot()
    {
        // 假设这里填充了TextVariants的初始值
        List<FormattedTextEntry> textVariants = new List<FormattedTextEntry>();
        // 示例：假设每个文本段落是原始内容按行分割的结果
        var lines = CurrentPlot.Content.ToString().Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
        int index = 0;
        foreach (var line in lines)
        {
            var entry = new FormattedTextEntry
            {
                Index = index,
                OriginalText = line,
            };
            textVariants.Add(entry);
            index++;
        }
        CurrentPlot.TextVariants = textVariants;
    }

    public void StartParseLines(AkpParser akpParser)
    {
        Parser = akpParser;
        // 示例：假设每个文本段落是原始内容按行分割的结果
        switch (Parser)
        {
            case { IsInitialized: false }:
                Parser.InitializeParser();
                break;
            default:
                _noticeBlock.RaiseCommonEvent("【警告!解析器未配置!】\r\n");
                return;
        }

        int pngIndex = 1;
        foreach (var entry in CurrentPlot.TextVariants)
        {
            entry.MdText = ConvertToMarkdown(entry);
            entry.TypText = ConvertToTypstCode(entry);
            if (string.IsNullOrEmpty(entry.TypText)) continue;
            entry.PngIndex = pngIndex;
            pngIndex++;
        }

        Parser.IsInitialized = false;
    }
    private string ConvertToMarkdown(FormattedTextEntry line)
    {
        // 实现转换为Markdown格式的逻辑
        return Parser!.ProcessSingleLine(line);
    }

    private string ConvertToTypstCode(FormattedTextEntry line)
    {
        if (string.IsNullOrEmpty(line.Dialog)) return "";

        string characterName = line.CharacterName;
        string dialog = line.Dialog;
        string bgImage = $"image(\"{line.Bg.Replace("https://", "")}\", width: 1440pt)";
        List<string> portraits = line.PortraitsInfo.Portraits;
        int focus = line.PortraitsInfo.FocusOn;

        string FormatSinglePortrait(string portrait) =>
            $"#arknights_sim(\"{characterName}\", \"{dialog}\", image(\"{portrait.Replace("https://", "")}\", height: 135%), {bgImage}, focus: {focus})";

        string FormatTwoPortraits(string portrait1, string portrait2) =>
            $"#arknights_sim_2p(\"{characterName}\", \"{dialog}\", image(\"{portrait1.Replace("https://", "")}\", height: 135%), image(\"{portrait2.Replace("https://", "")}\", height: 135%), {bgImage}, focus: {focus})";

        return portraits.Count switch
        {
            0 => $"#arknights_sim(\"{characterName}\", \"{dialog}\", image(\"pics/transparent.png\"), {bgImage})",
            1 => FormatSinglePortrait(portraits[0]),
            2 => FormatTwoPortraits(portraits[0], portraits[1]),
            _ => $"#arknights_sim(\"{characterName}\", \"{dialog}\", image(\"pics/transparent.png\"), {bgImage})"
        };
    }


    public string ExportMd()
    {
        var mdList = CurrentPlot.TextVariants.Select(p => p.MdText);
        return string.Join("\r\n", mdList);
    }
}

