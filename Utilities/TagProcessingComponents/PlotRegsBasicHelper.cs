using System;
using System.Text.RegularExpressions;
using ArkPlotWpf.Data;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

internal static class PlotRegsBasicHelper
{
    private static string ProcessMultiLine(string? newValue)
    {
        newValue = newValue!.Replace("\\n", "\n");
        newValue = newValue.Replace("\\t", "\t");
        return newValue;
    }


    public static string ProcessName(string line)
    {
        var name = ArkPlotRegs.NameRegex().Match(line).Value;
        if (name == "？？？") name = "神秘人士";
        var nameLine = ArkPlotRegs.RegexToSubName().Replace(line, $"**{name}**`讲道：`");
        if (line.Contains("multiline"))
            return ProcessMultiLine(nameLine) + Environment.NewLine;
        return nameLine;
    }

    public static string RipDollar(string text)
    {
        text = Regex.Replace(text, @"\$", "");
        return text;
    }

    public static string MakeLine(string line)
    {
        return "---";
    }

    public static string MakeComment(string line)
    {
        return $"> {line}";
    }
}