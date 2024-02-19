using ArkPlotWpf.Model;
using System;
using System.Linq;
using ArkPlotWpf.Services;
using ArkPlotWpf.Utilities.WorkFlow;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public class PlotManager
{
    public Plot CurrentPlot { get; private set; }
    private AkpParser? Parser { get; set; }

    private readonly NotificationBlock noticeBlock = NotificationBlock.Instance;

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
        // 假设这里填充了TextVariants的初始值
        List<FormattedTextEntry> textVariants = new List<FormattedTextEntry>();
        // 示例：假设每个文本段落是原始内容按行分割的结果
        var lines = CurrentPlot.Content.ToString().Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
        switch (Parser)
        {
            case { IsInitialized: false }:
                Parser.InitializeParser(textVariants);
                break;
            default:
                noticeBlock.RaiseCommonEvent("【警告!解析器未配置!】\r\n");
                return;
        }
        foreach (var entry in CurrentPlot.TextVariants)
        {
            entry.MdText = ConvertToMarkdown(entry);
            entry.TypText = ConvertToTypewriterText(entry.OriginalText);
        }

        Parser.IsInitialized = false;
    }
    private string ConvertToMarkdown(FormattedTextEntry line)
    {
        // 实现转换为Markdown格式的逻辑
        return Parser!.ProcessSingleLine(line);
    }

    private string ConvertToTypewriterText(string line)
    {
        // 实现转换为打字机风格文本的逻辑
        return ""; // 仅为示例，实际逻辑可能更复杂
    }

    public string ExportMd()
    {
        var mdList = CurrentPlot.TextVariants.Select(p => p.MdText);
        return string.Join("\r\n", mdList);
    }
}

