namespace ArkPlotWpf.Model;

/// <summary>
/// 用来表示一个章节的类。
/// </summary>
public class Plot
{
    /// <summary>
    /// 用来表示一个章节的类。
    /// </summary>
    /// <param name="title">类的标题。</param>
    /// <param name="content">类的内容。</param>
    public Plot(string title, StringBuilder content)
    {
        this.Title = title;
        this.Content = content;
    }

    /// <summary>类的标题。</summary>
    public string Title { get; init; }

    /// <summary>类的内容。</summary>
    public StringBuilder Content { get; init; }

    /// <summary>每一行文字</summary>
    public List<FormattedTextEntry> TextVariants = new();
}
