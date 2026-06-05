using System.Text.RegularExpressions;

namespace ArkPlot.Tts;

/// <summary>
/// TTS 文本清洗器。
/// 去除 HTML/Markdown 标记，截断超长文本，确保 TTS 引擎能正常处理。
/// </summary>
public static partial class TextSanitizer
{
    /// <summary>
    /// EdgeTTS 单段文本长度上限（字符数）。
    /// </summary>
    public const int MaxSegmentLength = 2000;

    /// <summary>
    /// 清洗文本用于 TTS：去除 HTML 标签、markdown 图片/链接语法、代码块、多余空白。
    /// 截断超长文本避免 EdgeTTS WebSocket 超时。
    /// </summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var text = raw;

        // 去除 HTML 标签（包括多行）
        text = HtmlTagRegex().Replace(text, " ");

        // 去除 markdown 图片: ![alt](url)
        text = MarkdownImageRegex().Replace(text, "");

        // 去除 markdown 链接但保留文本: [text](url) → text
        text = MarkdownLinkRegex().Replace(text, "$1");

        // 去除反引号包裹的代码
        text = InlineCodeRegex().Replace(text, "");

        // 去除 markdown 加粗/斜体标记
        text = BoldItalicRegex().Replace(text, "$1");

        // 去除 HTML 实体
        text = HtmlEntityRegex().Replace(text, " ");

        // 压缩空白
        text = WhitespaceRegex().Replace(text, " ").Trim();

        // 截断
        if (text.Length > MaxSegmentLength)
            text = text[..MaxSegmentLength];

        return text;
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex MarkdownImageRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"`[^`]*`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*{1,3}([^*]+)\*{1,3}")]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex(@"&\w+;")]
    private static partial Regex HtmlEntityRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
