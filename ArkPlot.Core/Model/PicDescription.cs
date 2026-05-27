using System;
using SqlSugar;

namespace ArkPlot.Core.Model;

[SugarTable("PicDescriptions")]
public class PicDescription
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string ImageUrl { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string PicDesc { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = false)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
