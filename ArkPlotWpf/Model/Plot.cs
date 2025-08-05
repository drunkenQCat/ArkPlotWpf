using SqlSugar;
using System.Text.Json;

namespace ArkPlotWpf.Model;

/// <summary>
/// 用来表示一个章节的类。
/// </summary>
[SugarTable("Plot")]
public class Plot
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(Length = 200)]
    public string Title { get; init; }

    [SugarColumn(IsIgnore = true)]
    public StringBuilder Content { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ContentText
    {
        get => Content?.ToString() ?? "";
        set => Content = new StringBuilder(value);
    }

    [SugarColumn(IsIgnore = true)]
    public List<FormattedTextEntry> TextVariants { get; set; } = [];

    [SugarColumn(ColumnDataType = "TEXT")]
    public string TextVariantsJson
    {
        get => JsonSerializer.Serialize(TextVariants);
        set => TextVariants = JsonSerializer.Deserialize<List<FormattedTextEntry>>(value) ?? [];
    }

    /// <summary>
    /// 用来表示一个章节的类。
    /// </summary>
    /// <param name="title">类的标题。</param>
    /// <param name="content">类的内容。</param>
    public Plot(string title, StringBuilder content)
    {
        Title = title;
        Content = content;
    }
    public Plot()
    {
        Title = string.Empty;
        Content = new StringBuilder();
    }
}
