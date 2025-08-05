using SqlSugar;
using System.Text.Json;

namespace ArkPlotWpf.Model;

/// <summary>
/// 表示用于临时存储PRTS数据的容器。
/// </summary>
[SugarTable("PrtsData")]
public class PrtsData
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }
    [SugarColumn(IsNullable = false, Length = 100)]
    public string Tag { get; set; }
    [SugarColumn(IsNullable = false, ColumnDataType = "TEXT")]
    public string DataJson { get; set; }
    [SugarColumn(IsIgnore = true)]
    public StringDict Data
    {
        get => string.IsNullOrEmpty(DataJson) ? new StringDict() : JsonSerializer.Deserialize<StringDict>(DataJson) ?? new StringDict();
        set => DataJson = value != null ? JsonSerializer.Serialize(value) : "{}";
    }

    /// <summary>
    /// 使用指定的标签初始化 <see cref="PrtsData"/> 类的不使用字典的实例。
    /// </summary>
    /// <param name="tag">与PRTS数据关联的标签。</param>
    public PrtsData(string tag)
    {
        Tag = tag;
        Data = new StringDict();
        DataJson = "{}";
    }

    /// <summary>
    /// 使用指定的标签和数据初始化 <see cref="PrtsData"/> 类的新实例。
    /// </summary>
    /// <param name="tag">与PRTS数据关联的标签。</param>
    /// <param name="data">PRTS数据。</param>
    public PrtsData(string tag, StringDict data)
    {
        Tag = tag;
        Data = data;
        DataJson = data != null ? JsonSerializer.Serialize(data) : "{}";
    }
    public PrtsData()
    {
        Tag = string.Empty;
        Data = new StringDict();
        DataJson = "{}";
    }
}
