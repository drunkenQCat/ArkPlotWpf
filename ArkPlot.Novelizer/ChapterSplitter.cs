using System.Text.RegularExpressions;

namespace ArkPlot.Novelizer;

/// <summary>
/// 章节拆分器：负责从 Markdown 内容中按 ## 标题拆分章节
/// </summary>
public static partial class ChapterSplitter
{
    [GeneratedRegex(@"^#{1,6}\s*", RegexOptions.Multiline)]
    private static partial Regex MarkdownHeadingRegex();

    [GeneratedRegex(@"^(?=## )", RegexOptions.Multiline)]
    private static partial Regex ChapterSplitRegex();

    /// <summary>
    /// 将预处理后的 Markdown 内容拆分为章节列表
    /// </summary>
    public static IReadOnlyList<Chapter> SplitChapters(string processedContent)
    {
        var rawChapters = ChapterSplitRegex().Split(processedContent)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var chapters = new List<Chapter>(rawChapters.Count);
        for (int i = 0; i < rawChapters.Count; i++)
        {
            var chunk = rawChapters[i];
            var lines = chunk.Split('\n', 2);
            var title = lines[0].TrimStart('#', ' ').Trim();
            var body = lines.Length > 1 ? lines[1].Trim() : "";

            chapters.Add(new Chapter(i, title, body));
        }

        return chapters;
    }

    /// <summary>
    /// 去除文本中的 Markdown 标题（# ## ### 等），保留正文
    /// </summary>
    public static string StripHeadings(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return MarkdownHeadingRegex().Replace(text, "");
    }
}

/// <summary>
/// 表示一个待处理的章节
/// </summary>
public record Chapter(int Index, string Title, string Body);
