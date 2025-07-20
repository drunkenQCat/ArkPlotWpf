using System.Text.RegularExpressions;

namespace ArkPlotWpf.Utilities;

public class HtmlTagParser
{
    private string OriginalHtml { get; set; }
    private string TagName { get; set; } = "";
    public Dictionary<string, string> Attributes { get; set; }

    public HtmlTagParser(string html)
    {
        OriginalHtml = html;
        Attributes = new Dictionary<string, string>();
        ParseHtml();
    }

    private void ParseHtml()
    {
        // 使用正则表达式匹配HTML标签及其属性
        var tagPattern = new Regex(@"<(\w+)([^>]*)>");
        var attrPattern = new Regex(@"(\w+)=""([^""]*)""");

        var tagMatch = tagPattern.Match(OriginalHtml);
        if (tagMatch.Success)
        {
            TagName = tagMatch.Groups[1].Value;
            var attrMatches = attrPattern.Matches(tagMatch.Groups[2].Value);
            foreach (Match match in attrMatches)
            {
                Attributes.Add(match.Groups[1].Value, match.Groups[2].Value);
            }
        }
    }

    public string ReconstructHtml()
    {
        // 重构HTML标签
        var reconstructed = $"<{TagName}";
        foreach (var attr in Attributes)
        {
            reconstructed += $" {attr.Key}=\"{attr.Value}\"";
        }
        reconstructed += ">";
        return reconstructed;
    }
}