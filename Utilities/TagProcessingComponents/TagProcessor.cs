using System.Text.RegularExpressions;
using ArkPlotWpf.Data;
using ArkPlotWpf.Model;
using ArkPlotWpf.Utilities.PrtsComponents;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public partial class TagProcessor
{
    public readonly PlotRegs Regs = PlotRegs.Instance;
    private readonly PrtsDataProcessor prts = new();

    public TagProcessor()
    {
        Regs.RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.SpecialTagRegex(), ProcessTag));
    }

    private string ProcessTag(string line)
    {
        //
        // Extract the tag from the line
        string tag = ExtractTag(line);
        // Check if the tag exists; if not, log a notice and return the original line
        if (!IsValidTag(tag)) return NotifyInvalidTag(line, tag);

        // Attempt to replace the tag with a new one
        string newTag = SubStituteTag(tag);
        if (string.IsNullOrEmpty(newTag)) return string.Empty; // Early exit if no new tag

        // Extract and process the value associated with the tag
        string newValue = ExtractValue(line, tag);
        if (string.IsNullOrEmpty(newValue)) return HandleEmptyValue(newTag); // Handle empty values separately

        return ConstructResult(newTag, newValue);
    }

    private static string ExtractTag(string line)
    {
        var tag = ArkPlotRegs.SpecialTagRegex().Match(line).Value;
        tag = tag.ToLower();
        return tag;
    }

    private bool IsValidTag(string tag) => Regs.TagList[tag] != null;

    private string NotifyInvalidTag(string line, string tag)
    {
        NotificationBlock.Instance.OnNoMatchTag(new LineNoMatchEventArgs(line, tag));
        return line;
    }

    private string SubStituteTag(string tag) => (string)Regs.TagList[tag]!;

    private string ExtractValue(string line, string tag)
    {
        var newValueReg = (string)Regs.TagList[tag + "_reg"]!;
        var newValue = Regex.Match(line, newValueReg).Value;
        return newValue;
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
        string? mediaUrl = ConvertValueToHtmlTag(newTag, newValue); // Get any associated media URL
        newValue = TagProcessor.FindTheLongestWord(newValue); // Simplify the value to its longest word
        newValue = PlotRegsBasicHelper.RipDollar(newValue); // Remove any dollar signs

        // Construct the result line from the processed tag and value
        var resultLine = newTag + newValue;
        resultLine = TagProcessor.ProcessFlashBack(resultLine, newTag, newValue); // Process any flashbacks
        resultLine = TagProcessor.AttachToMediaUrl(resultLine, mediaUrl); // Attach media URL if present

        return resultLine; // Return the processed line
    }
}
