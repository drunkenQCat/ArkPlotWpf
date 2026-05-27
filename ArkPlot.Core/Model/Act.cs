using SqlSugar;

namespace ArkPlot.Core.Model;

[SugarTable("Acts")]
public class Act
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;
}
