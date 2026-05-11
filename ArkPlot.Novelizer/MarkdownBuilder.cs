using System.Text.RegularExpressions;
using ArkPlot.Core.Model;

namespace ArkPlot.Novelizer;

/// <summary>
/// 从 FormattedTextEntry 数组或原始 .md 文本构建适合 LLM 消费的纯文本
/// </summary>
public static partial class MarkdownBuilder
{
    /// <summary>
    /// 预处理原始 .md 文件内容：去除 HTML 标签、表格分隔线，保留 Markdown 和纯文本
    /// </summary>
    public static string PreprocessMdContent(string rawMd)
    {
        var text = HtmlTagRegex().Replace(rawMd, "");

        // 去掉表格分隔线（|---|---|之类）
        text = TableSeparatorRegex().Replace(text, "");

        // 清理多余空行
        text = MultiNewlineRegex().Replace(text, "\n");

        return text.Trim();
    }

    /// <summary>
    /// 拼接所有条目的 MdText，去除 HTML 标签和纯标记行，保留对话和舞台指示
    /// </summary>
    public static string BuildNovelInput(IEnumerable<FormattedTextEntry> entries)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var entry in entries)
        {
            var text = entry.MdText;
            if (string.IsNullOrWhiteSpace(text)) continue;

            // 跳过分隔线
            if (text.Trim() == "---") continue;

            // 去除 HTML 标签（img、audio、source 等）
            text = HtmlTagRegex().Replace(text, "");

            // 清理多余空行
            text = MultiNewlineRegex().Replace(text, "\n");

            if (string.IsNullOrWhiteSpace(text)) continue;

            sb.AppendLine(text.Trim());
        }

        var result = sb.ToString().Trim();

        // 如果没有任何有效内容，使用 dialog 字段兜底
        if (string.IsNullOrWhiteSpace(result))
        {
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.CharacterName) && !string.IsNullOrWhiteSpace(entry.Dialog))
                {
                    sb.AppendLine($"**{entry.CharacterName}**：{entry.Dialog}");
                }
                else if (!string.IsNullOrWhiteSpace(entry.Dialog))
                {
                    sb.AppendLine(entry.Dialog);
                }
            }
            result = sb.ToString().Trim();
        }

        return result;
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"^\|?[-:|\s]+\|?$", RegexOptions.Multiline)]
    private static partial Regex TableSeparatorRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiNewlineRegex();
}