using SqlSugar;

namespace ArkPlot.Core.Model;

/// <summary>
/// 用来表示一个章节的类。
/// </summary>
[SugarTable("Plot")]
// 索引由 DbFactory 按需创建（过滤索引 SQLite 不支持 Attribute 方式）
public class Plot
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    /// <summary>
    /// 关联的活动 ID（Acts 表主键），0 表示未关联
    /// </summary>
    [SugarColumn(ColumnDataType = "INTEGER", IsNullable = false)]
    public long ActId { get; set; }

    /// <summary>
    /// 关联的章节 ID（StoryChapters 表主键），0 表示未关联
    /// </summary>
    [SugarColumn(ColumnDataType = "INTEGER", IsNullable = false)]
    public long StoryChapterId { get; set; }

    /// <summary>
    /// 导航到关联的 StoryChapter（一对一）
    /// </summary>
    [Navigate(NavigateType.OneToOne, nameof(StoryChapterId))]
    public StoryChapter? Chapter { get; set; }

    /// <summary>
    /// 导航到解析后的 FormattedTextEntry 列表（一对多）
    /// </summary>
    [Navigate(NavigateType.OneToMany, nameof(FormattedTextEntry.PlotId))]
    public List<FormattedTextEntry> Entries { get; set; } = [];

    /// <summary>
    /// 处理状态：0=未处理，1=处理中，2=已完成
    /// </summary>
    public int Status { get; set; }

    [SugarColumn(Length = 200)]
    public string Title { get; init; }

    /// <summary>
    /// 原始内容（运行时仅用于下载后、解析前的管道中转）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public StringBuilder Content { get; set; }

    /// <summary>
    /// 解析后的文本条目列表（运行时使用，不存库；入库后在 FormattedTextEntry 表中）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public List<FormattedTextEntry> TextVariants { get; set; } = [];

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
