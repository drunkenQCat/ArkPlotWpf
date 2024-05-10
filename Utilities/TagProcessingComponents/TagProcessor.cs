using System.Text.RegularExpressions;
using ArkPlotWpf.Data;
using ArkPlotWpf.Model;
using ArkPlotWpf.Services;
using ArkPlotWpf.Utilities.PrtsComponents;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public partial class TagProcessor
{
    private readonly PrtsDataProcessor prts = new();
    private readonly Dictionary<string, Regex> regexCache = new();
    public readonly PlotRules Rules = PlotRules.Instance;

    public TagProcessor()
    {
        Rules.RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.SpecialTagRegex(), ProcessTag));
    }

    private string ProcessTag(string line)
    {
        //
        // Extract the tag from the line
        var tag = ExtractTag(line);
        // Check if the tag exists; if not, log a notice and return the original line
        if (!IsValidTag(tag)) return NotifyInvalidTag(line, tag);

        // Attempt to replace the tag with a new one
        var newTag = SubStituteTag(tag);
        if (string.IsNullOrEmpty(newTag)) return string.Empty; // Early exit if no new tag

        // Extract and process the value associated with the tag
        var newValue = ExtractValue(line, tag);
        if (string.IsNullOrEmpty(newValue)) return HandleEmptyValue(newTag); // Handle empty values separately

        return ConstructResult(newTag, newValue);
    }

    private static string ExtractTag(string line)
    {
        var tag = ArkPlotRegs.SpecialTagRegex().Match(line).Value;
        tag = tag.ToLower();
        return tag;
    }

    private bool IsValidTag(string tag)
    {
        return Rules.TagList[tag] != null;
    }

    private string NotifyInvalidTag(string line, string tag)
    {
        NotificationBlock.Instance.OnNoMatchTag(new LineNoMatchEventArgs(line, tag));
        return line;
    }

    private string SubStituteTag(string tag)
    {
        return (string)Rules.TagList[tag]!;
    }

    private string ExtractValue(string line, string tag)
    {
        if (!regexCache.TryGetValue(tag, out var regex))
        {
            var newValueReg = (string)Rules.TagList[tag + "_reg"]!;
            regex = new Regex(newValueReg);
            regexCache[tag] = regex;
        }

        var match = regex.Match(line);
        return match.Success ? match.Value : string.Empty;
    }

    private string HandleEmptyValue(string newTag)
    {
        switch (newTag)
        {
            case "`音乐停止`":
                return "<musicstop></musicstop>\r\n";
            case "`立绘`":
                return "";
            case "`图像`":
                return "";
        }

        return newTag;
    }

    private string ConstructResult(string newTag, string newValue)
    {
        // Additional processing on the new value
        var mediaUrl = ConvertValueToHtmlTag(newTag, newValue); // Get any associated media URL
        newValue = FindTheLongestWord(newValue); // Simplify the value to its longest word
        newValue = PlotRegsBasicHelper.RipDollar(newValue); // Remove any dollar signs

        // Construct the result line from the processed tag and value
        var resultLine = newTag + newValue;
        resultLine = ProcessFlashBack(resultLine, newTag, newValue); // Process any flashbacks
        resultLine = AttachToMediaUrl(resultLine, mediaUrl); // Attach media URL if present

        return resultLine; // Return the processed line
    }
}