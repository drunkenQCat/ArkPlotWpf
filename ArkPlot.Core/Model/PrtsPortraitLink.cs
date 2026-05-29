using SqlSugar;

namespace ArkPlot.Core.Model;

/// <summary>
/// 角色立绘链接表，替代原来 Data_Link 的 PortraitLinkDocument。
/// </summary>
[SugarTable("PrtsPortraitLinks")]
public class PrtsPortraitLink
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    /// <summary>
    /// 角色代码，如 "char_220_grani"
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string CharacterCode { get; set; } = string.Empty;

    /// <summary>
    /// 立绘名称，如 "char_220_grani_3"
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string PortraitName { get; set; } = string.Empty;

    /// <summary>
    /// 别名（可选）
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? Alias { get; set; }

    /// <summary>
    /// 数组中的顺序索引
    /// </summary>
    [SugarColumn(ColumnDataType = "INTEGER")]
    public int SortOrder { get; set; }
}
