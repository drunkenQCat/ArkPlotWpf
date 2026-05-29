using SqlSugar;

namespace ArkPlot.Core.Model;

/// <summary>
/// 活动（Act），对应 story_review_table.json 中的一个活动。
/// 跨语言时同一个 ActId 在不同 Lang 下有独立行。
/// </summary>
[SugarTable("Acts")]
public class Act
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    /// <summary>
    /// 跨语言关联键，如 "1stact"、"act3d0"
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string ActId { get; set; } = string.Empty;

    /// <summary>
    /// 语言：zh_CN / en / ja / ko
    /// </summary>
    [SugarColumn(Length = 10, IsNullable = false)]
    public string Lang { get; set; } = "zh_CN";

    /// <summary>
    /// 当前语言下的活动名称
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 活动类型：ACTIVITY_STORY / MINI_STORY / MAIN_STORY
    /// </summary>
    [SugarColumn(Length = 30, IsNullable = false)]
    public string ActType { get; set; } = string.Empty;
}
