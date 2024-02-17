namespace ArkPlotWpf.Model;

/// <summary>
/// 用来表示一个章节的类。
/// </summary>
/// <param name="Title">类的标题。</param>
/// <param name="Content">类的内容。</param>
public record Plot(string Title, StringBuilder Content);