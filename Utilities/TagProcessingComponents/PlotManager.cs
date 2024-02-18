using ArkPlotWpf.Model;
using System;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public class PlotManager
{
    public Plot CurrentPlot { get; private set; }

    public PlotManager(string title, StringBuilder content)
    {
        CurrentPlot = new Plot(title, content);
    }

    public void InitializePlot()
    {
        // 假设这里填充了TextVariants的初始值
        List<FormattedTextEntry> textVariants = new List<FormattedTextEntry>();
        // 示例：假设每个文本段落是原始内容按行分割的结果
        var lines = CurrentPlot.Content.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        int index = 0;
        foreach (var line in lines)
        {
            textVariants.Add(new FormattedTextEntry { 
                Index = index++, 
                OriginalText = line, 
                MdText = ConvertToMarkdown(line), 
                TypText = ConvertToTypewriterText(line) 
            });
        }

        CurrentPlot.TextVariants = textVariants ;
    }

    private string ConvertToMarkdown(string line)
    {
        // 实现转换为Markdown格式的逻辑
        return line; // 仅为示例，实际逻辑可能更复杂
    }

    private string ConvertToTypewriterText(string line)
    {
        // 实现转换为打字机风格文本的逻辑
        return line; // 仅为示例，实际逻辑可能更复杂
    }
}

