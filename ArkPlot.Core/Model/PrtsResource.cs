using SqlSugar;

namespace ArkPlot.Core.Model;

/// <summary>
/// PRTS 资源表，替代原来 Data_Image / Data_Char / Data_Audio 的 StringDict → DataJson 列。
/// 每条记录是一个 key → URL 映射。
/// </summary>
[SugarTable("PrtsResources")]
public class PrtsResource
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    /// <summary>
    /// 资源类型：Image / Char / Audio
    /// </summary>
    [SugarColumn(Length = 10, IsNullable = false)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// 资源键，如 "char_293_thorns_1"
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string ResourceKey { get; set; } = string.Empty;

    /// <summary>
    /// 资源 URL
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string ResourceUrl { get; set; } = string.Empty;
}
