using ArkPlot.Tts.Alignment;

namespace ArkPlot.Avalonia.Tests;

public class DialogExtractorTests
{
    private const char LQ = '\u201C'; // "
    private const char RQ = '\u201D'; // "

    [Fact]
    public void ExtractSegments_AlternatingNarrationAndDialog()
    {
        var text = $"旁白开始。{LQ}第一句对话。{RQ}中间旁白。{LQ}第二句对话。{RQ}结尾旁白。";
        var segments = DialogExtractor.ExtractSegments(text);

        Assert.Equal(5, segments.Count);
        Assert.False(segments[0].IsDialog);
        Assert.Equal("旁白开始。", segments[0].Text);
        Assert.True(segments[1].IsDialog);
        Assert.Equal("第一句对话。", segments[1].Text);
        Assert.False(segments[2].IsDialog);
        Assert.Equal("中间旁白。", segments[2].Text);
        Assert.True(segments[3].IsDialog);
        Assert.Equal("第二句对话。", segments[3].Text);
        Assert.False(segments[4].IsDialog);
        Assert.Equal("结尾旁白。", segments[4].Text);
    }

    [Fact]
    public void ExtractSegments_OnlyDialog()
    {
        var text = $"{LQ}只有对话。{RQ}";
        var segments = DialogExtractor.ExtractSegments(text);

        Assert.Single(segments);
        Assert.True(segments[0].IsDialog);
        Assert.Equal("只有对话。", segments[0].Text);
    }

    [Fact]
    public void ExtractSegments_OnlyNarration()
    {
        var text = "只有旁白，没有引号。";
        var segments = DialogExtractor.ExtractSegments(text);

        Assert.Single(segments);
        Assert.False(segments[0].IsDialog);
    }

    [Fact]
    public void ExtractSegments_UnclosedQuote_TreatedAsNarration()
    {
        var text = $"正常旁白。{LQ}未闭合的引号内容";
        var segments = DialogExtractor.ExtractSegments(text);

        // 未闭合引号后的内容全部当作旁白
        Assert.Equal(2, segments.Count);
        Assert.False(segments[0].IsDialog);
        Assert.False(segments[1].IsDialog);
    }

    [Fact]
    public void ExtractChapters_SplitsOnMarkdownHeadings()
    {
        var text = $"## 第一章 标题\n\n正文内容。{LQ}对话。{RQ}\n\n## 第二章 标题\n\n第二章正文。";
        var chapters = DialogExtractor.ExtractChapters(text);

        Assert.Equal(2, chapters.Count);
        Assert.Equal("第一章 标题", chapters[0].Title);
        Assert.Equal("第二章 标题", chapters[1].Title);
        Assert.Single(chapters[0].Dialogs.ToList());
    }

    [Fact]
    public void ExtractChapters_ActivityHeader_StillExtracted()
    {
        var text = $"## 水晶箭行动\n\n> *（本章无正文）*\n\n## CR-ST-1 特别参观通道 幕间\n\n正文内容。{LQ}对话。{RQ}";
        var chapters = DialogExtractor.ExtractChapters(text);

        Assert.Equal(2, chapters.Count);
        Assert.Equal("水晶箭行动", chapters[0].Title);
        Assert.Equal("CR-ST-1 特别参观通道 幕间", chapters[1].Title);
    }

    [Fact]
    public void ExtractDialogs_ReturnsOnlyQuotedText()
    {
        var text = $"旁白。{LQ}对话一。{RQ}旁白。{LQ}对话二。{RQ}";
        var dialogs = DialogExtractor.ExtractDialogs(text);

        Assert.Equal(2, dialogs.Count);
        Assert.Equal("对话一。", dialogs[0]);
        Assert.Equal("对话二。", dialogs[1]);
    }

    [Fact]
    public void Normalize_EllipsisVariants()
    {
        Assert.Equal("等\u2026", DialogExtractor.Normalize("等......"));
        Assert.Equal("等\u2026", DialogExtractor.Normalize("等\u2026"));
    }

    [Fact]
    public void ExtractSegments_RealNovelText()
    {
        var text = $"磁山二号实验室的走廊里，艾拉压低声音：{LQ}我们已进入磁山二号。{RQ}\n\n她顿了顿，{LQ}医生，你那边情况如何？{RQ}\n\n通讯器里传来回应：{LQ}已确认外围安全。{RQ}";
        var segments = DialogExtractor.ExtractSegments(text);
        var dialogs = segments.Where(s => s.IsDialog).ToList();

        Assert.Equal(3, dialogs.Count);
        Assert.Equal("我们已进入磁山二号。", dialogs[0].Text);
        Assert.Equal("医生，你那边情况如何？", dialogs[1].Text);
        Assert.Equal("已确认外围安全。", dialogs[2].Text);
    }
}
