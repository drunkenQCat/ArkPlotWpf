using System.Text.RegularExpressions;

namespace ArkPlot.Novelizer;

/// <summary>
/// 小说文本中的一个片段（旁白或对话）。
/// </summary>
/// <param name="Text">文本内容（不含引号）</param>
/// <param name="IsDialog">true=引号内对话，false=旁白/叙述</param>
public record NovelSegment(string Text, bool IsDialog)
{
    /// <summary>对齐后填入：对应 FormattedTextEntry 的角色名（旁白为 null）</summary>
    public string? CharacterName { get; set; }

    /// <summary>对齐后填入：对应 FormattedTextEntry 的角色 code（旁白为 null）</summary>
    public string? CharacterCode { get; set; }

    /// <summary>对齐后填入：对应 FormattedTextEntry.Index（旁白为 -1）</summary>
    public int EntryIndex { get; set; } = -1;
}

/// <summary>
/// 小说文本中的一个章节（对应原始 Plot）。
/// </summary>
/// <param name="Title">章节标题（## 后面的文本）</param>
/// <param name="Segments">该章节内的旁白/对话片段列表</param>
public record NovelChapter(string Title, List<NovelSegment> Segments)
{
    /// <summary>该章节内所有 IsDialog=true 的片段（方便对齐使用）</summary>
    public IEnumerable<NovelSegment> Dialogs => Segments.Where(s => s.IsDialog);
}

/// <summary>
/// 从小说化文本中提取章节结构和对话/旁白分段。
/// </summary>
public static partial class DialogExtractor
{
    /// <summary>
    /// 将小说文本按 ## 标题拆分为章节，每章内提取旁白/对话交替的片段列表。
    /// </summary>
    public static List<NovelChapter> ExtractChapters(string novelText)
    {
        var chapters = new List<NovelChapter>();
        // 用不含捕获组的正则 Split，避免捕获组干扰结果
        var chapterChunks = ChapterSplitRegex().Split(novelText);

        foreach (var chunk in chapterChunks)
        {
            var trimmed = chunk.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // 第一行是标题（## 已被 Split 消费），剩余是正文
            var lines = trimmed.Split('\n', 2);
            var title = lines[0].Trim();
            var body = lines.Length > 1 ? lines[1].Trim() : "";

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)) continue;

            var segments = ExtractSegments(body);
            chapters.Add(new NovelChapter(title, segments));
        }

        return chapters;
    }

    /// <summary>
    /// 从一段正文中提取旁白/对话交替的片段。
    /// 识别中文引号 "" 内的内容作为对话。
    /// </summary>
    public static List<NovelSegment> ExtractSegments(string text)
    {
        var segments = new List<NovelSegment>();
        int pos = 0;

        while (pos < text.Length)
        {
            int openIdx = text.IndexOf('\u201C', pos); // "
            if (openIdx < 0)
            {
                // 没有更多引号，剩余全是旁白
                AddNarration(segments, text[pos..]);
                break;
            }

            // 引号前的旁白
            if (openIdx > pos)
                AddNarration(segments, text[pos..openIdx]);

            int closeIdx = text.IndexOf('\u201D', openIdx + 1); // "
            if (closeIdx < 0)
            {
                // 未闭合引号，剩余全当旁白
                AddNarration(segments, text[openIdx..]);
                break;
            }

            // 引号内的对话
            var dialog = text[(openIdx + 1)..closeIdx].Trim();
            if (!string.IsNullOrWhiteSpace(dialog))
                segments.Add(new NovelSegment(dialog, IsDialog: true));

            pos = closeIdx + 1;
        }

        return segments;
    }

    /// <summary>
    /// 提取文本中所有引号内的对话（纯字符串列表，不含分段信息）。
    /// </summary>
    public static List<string> ExtractDialogs(string text)
    {
        var dialogs = new List<string>();
        var matches = QuotedDialogRegex().Matches(text);
        foreach (Match match in matches)
        {
            var dialog = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(dialog))
                dialogs.Add(dialog);
        }
        return dialogs;
    }

    /// <summary>
    /// 标准化对话文本，用于比较时消除标点差异。
    /// 处理：省略号变体、空白、引号等。
    /// </summary>
    public static string Normalize(string text)
    {
        // 省略号变体统一
        text = text.Replace("......", "…");
        text = text.Replace("…", "…");
        // 去除多余空白
        text = WhitespaceRegex().Replace(text, " ").Trim();
        return text;
    }

    private static void AddNarration(List<NovelSegment> segments, string text)
    {
        var trimmed = text.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
            segments.Add(new NovelSegment(trimmed, IsDialog: false));
    }

    [GeneratedRegex(@"^#+\s+", RegexOptions.Multiline)]
    private static partial Regex ChapterSplitRegex();

    [GeneratedRegex(@"\u201C([^\u201D]*)\u201D")]
    private static partial Regex QuotedDialogRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
