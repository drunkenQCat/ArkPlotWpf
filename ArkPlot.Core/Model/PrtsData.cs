using SqlSugar;
using System.Text.Json;

namespace ArkPlot.Core.Model;

/// <summary>
/// 表示用于临时存储PRTS数据的容器�?
/// </summary>
[SugarTable("PrtsData")]
public class PrtsData
{
    private StringDict? _dataCache;

    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }
    [SugarColumn(IsNullable = false, Length = 100)]
    public string Tag { get; set; }
    [SugarColumn(IsNullable = false, ColumnDataType = "TEXT")]
    public string DataJson { get; set; }
    [SugarColumn(IsIgnore = true)]
    public StringDict Data
    {
        get
        {
            if (_dataCache == null)
            {
                _dataCache = string.IsNullOrEmpty(DataJson)
                    ? new StringDict()
                    : JsonSerializer.Deserialize<StringDict>(DataJson) ?? new StringDict();
                _dataCache.OnChanged += () => DataJson = JsonSerializer.Serialize(_dataCache);
            }
            return _dataCache;
        }
        set
        {
            _dataCache = value ?? new StringDict();
            DataJson = JsonSerializer.Serialize(_dataCache);
            // 订阅修改事件
            _dataCache.OnChanged += () => DataJson = JsonSerializer.Serialize(_dataCache);
        }
    }


    /// <summary>
    /// 使用指定的标签初始化 <see cref="PrtsData"/> 类的不使用字典的实例�?
    /// </summary>
    /// <param name="tag">与PRTS数据关联的标签�?/param>
    public PrtsData(string tag)
    {
        Tag = tag;
        DataJson = "{}";
    }

    /// <summary>
    /// 使用指定的标签和数据初始�?<see cref="PrtsData"/> 类的新实例�?
    /// </summary>
    /// <param name="tag">与PRTS数据关联的标签�?/param>
    /// <param name="data">PRTS数据�?/param>
    public PrtsData(string tag, StringDict data)
    {
        Tag = tag;
        Data = data;
        DataJson = JsonSerializer.Serialize(data);
    }
    public PrtsData()
    {
        Tag = string.Empty;
        DataJson = "{}";
    }
}
