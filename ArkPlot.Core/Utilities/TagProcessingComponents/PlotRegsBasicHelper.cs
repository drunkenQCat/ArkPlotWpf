using System.Text.RegularExpressions;
using ArkPlot.Core.Model;

namespace ArkPlot.Core.Utilities.TagProcessingComponents;

internal static class PlotRegsBasicHelper
{
    private static string GetMultiLineName(FormattedTextEntry entry)
    {
        if (!entry.CommandSet.TryGetValue("name", out var name)) name = "";
        return name;
    }


    public static string ProcessDialog(FormattedTextEntry entry)
    {
        var name = entry.CharacterName;
        if (name == "？？？" || string.IsNullOrWhiteSpace(name)) name = "神秘人士";
        if (entry.Type == "multiline")
            name = GetMultiLineName(entry);
        var dialog = entry.Dialog;
        var dialogWithName = $"**{name}**`讲道：`{dialog}";
        if (dialog == "......") dialogWithName = $"**{name}**`陷入了沉默`";
        return dialogWithName;
    }

    public static string RipDollar(string text)
    {
        text = Regex.Replace(text, @"\$", "");
        return text;
    }

    public static string MakeLine(FormattedTextEntry line)
    {
        return "---";
    }

    public static string MakeComment(FormattedTextEntry line)
    {
        return $"> {line.OriginalText}";
    }
}
