using ArkPlotWpf.Model;
using System;
using System.Linq;
using ArkPlotWpf.Utilities.WorkFlow;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public class PlotManager
{
    public Plot CurrentPlot { get; private set; }
    private AkpParser Parser { get; init; }
    public PlotManager(string title, StringBuilder content, AkpParser akpParser)
    {
        CurrentPlot = new Plot(title, content);
        Parser = akpParser;
    }


    public void InitializePlot()
    {
        // 假设这里填充了TextVariants的初始值
        List<FormattedTextEntry> textVariants = new List<FormattedTextEntry>();
        // 示例：假设每个文本段落是原始内容按行分割的结果
        var lines = CurrentPlot.Content.ToString().Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
        Parser.InitializeParser(textVariants);
        int index = 0;
        foreach (var line in lines)
        {
            textVariants.Add(new FormattedTextEntry
            {
                Index = index++,
                OriginalText = line,
                MdText = ConvertToMarkdown(line),
                TypText = ConvertToTypewriterText(line)
            });
        }

        CurrentPlot.TextVariants = textVariants;
    }

    private string ConvertToMarkdown(string line)
    {
        // 实现转换为Markdown格式的逻辑
        return Parser.ProcessSingleLine(line);
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

