using SqlSugar;

namespace ArkPlot.Core.Model;

/// <summary>
/// 活动下的章节，对应 story_review_table 中 infoUnlockDatas 的每一项。
/// </summary>
[SugarTable("StoryChapters")]
[SugarIndex("uk_chapter", nameof(ActId), OrderByType.Asc, nameof(StoryId), OrderByType.Asc, isUnique: true)]
public class StoryChapter
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    /// <summary>
    /// 所属活动的外键
    /// </summary>
    [SugarColumn(ColumnDataType = "INTEGER", IsNullable = false)]
    public long ActId { get; set; }

    /// <summary>
    /// 章节的唯一 ID，如 "1stact_level_a001_01_beg"
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string StoryId { get; set; } = string.Empty;

    /// <summary>
    /// 关卡代号，如 "GT-1"
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string StoryCode { get; set; } = string.Empty;

    /// <summary>
    /// 章节名称，如 "日正当中"
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string StoryName { get; set; } = string.Empty;

    /// <summary>
    /// GitHub 上原始文本文件的相对路径，如 "activities/a001/level_a001_01_beg"
    /// </summary>
    [SugarColumn(Length = 300, IsNullable = false)]
    public string StoryTxt { get; set; } = string.Empty;

    /// <summary>
    /// 标签，如 "行动前" / "行动后" / "幕间"
    /// </summary>
    [SugarColumn(Length = 30, IsNullable = true)]
    public string? AvgTag { get; set; }

    /// <summary>
    /// 排序序号
    /// </summary>
    [SugarColumn(ColumnDataType = "INTEGER")]
    public int StorySort { get; set; }

    /// <summary>
    /// 前置依赖的 storyId，无依赖则为 null
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? StoryDependence { get; set; }
}
