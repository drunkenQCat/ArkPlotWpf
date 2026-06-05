namespace ArkPlot.Tts.Tests;

public class TextSanitizerTests
{
    [Fact]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal("", TextSanitizer.Sanitize(""));
        Assert.Equal("", TextSanitizer.Sanitize("   "));
        Assert.Equal("", TextSanitizer.Sanitize(null!));
    }

    [Fact]
    public void Sanitize_PlainText_ReturnsAsIs()
    {
        Assert.Equal("你好世界", TextSanitizer.Sanitize("你好世界"));
    }

    [Fact]
    public void Sanitize_HtmlTags_Removed()
    {
        Assert.Equal("hello world", TextSanitizer.Sanitize("<p>hello</p> <b>world</b>"));
    }

    [Fact]
    public void Sanitize_MarkdownImage_Removed()
    {
        Assert.Equal("", TextSanitizer.Sanitize("![alt text](http://example.com/img.png)"));
    }

    [Fact]
    public void Sanitize_MarkdownLink_KeepsText()
    {
        Assert.Equal("点击这里", TextSanitizer.Sanitize("[点击这里](http://example.com)"));
    }

    [Fact]
    public void Sanitize_InlineCode_Removed()
    {
        Assert.Equal("some text", TextSanitizer.Sanitize("some `code` text"));
    }

    [Fact]
    public void Sanitize_BoldItalic_Removed()
    {
        Assert.Equal("重要文本", TextSanitizer.Sanitize("**重要文本**"));
        Assert.Equal("斜体文本", TextSanitizer.Sanitize("*斜体文本*"));
    }

    [Fact]
    public void Sanitize_HtmlEntities_Removed()
    {
        Assert.Equal("a b", TextSanitizer.Sanitize("a&nbsp;b"));
    }

    [Fact]
    public void Sanitize_MultipleWhitespaces_Collapsed()
    {
        Assert.Equal("a b c", TextSanitizer.Sanitize("a   b\n\n  c"));
    }

    [Fact]
    public void Sanitize_LongText_Truncated()
    {
        var longText = new string('A', 3000);
        var result = TextSanitizer.Sanitize(longText);
        Assert.Equal(TextSanitizer.MaxSegmentLength, result.Length);
    }

    [Fact]
    public void Sanitize_CombinedMarkup_AllCleaned()
    {
        var input = "<p>**重要**的[链接](url)和![图](img.png)</p>";
        var result = TextSanitizer.Sanitize(input);
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain("**", result);
        Assert.DoesNotContain("](url)", result);
        Assert.DoesNotContain("](img.png)", result);
    }
}
