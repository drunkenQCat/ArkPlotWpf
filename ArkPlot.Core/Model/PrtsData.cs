using SqlSugar;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System;

namespace ArkPlot.Core.Model;

/// <summary>
/// 表示用于临时存储PRTS数据的容器。
/// </summary>
[SugarTable("PrtsData")]
[SugarIndex("uk_prts", nameof(Tag), OrderByType.Asc, isUnique: true)]
public class PrtsData
{
    private StringDict? _dataCache;

    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }
    [SugarColumn(IsNullable = false, Length = 100)]
    public string Tag { get; set; }
    [SugarColumn(IsNullable = false, ColumnDataType = "TEXT")]
    public string DataJson { get; set; }
    [SugarColumn(IsNullable = true, Length = 64)]
    public string? DataHash { get; set; }
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
    /// 计算当前数据的哈希值
    /// </summary>
    /// <returns>数据的SHA256哈希值</returns>
    public string CalculateDataHash()
    {
        var jsonToHash = string.IsNullOrEmpty(DataJson) ? "{}" : DataJson;
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jsonToHash));
            return Convert.ToHexString(hashBytes);
        }
    }

    /// <summary>
    /// 验证数据哈希值是否匹配
    /// </summary>
    /// <returns>如果哈希值匹配返回true，否则返回false</returns>
    public bool VerifyDataHash()
    {
        if (string.IsNullOrEmpty(DataHash))
            return false;
        
        var currentHash = CalculateDataHash();
        return DataHash.Equals(currentHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 更新数据哈希值
    /// </summary>
    public void UpdateDataHash()
    {
        DataHash = CalculateDataHash();
    }

    /// <summary>
    /// 使用指定的标签初始化 <see cref="PrtsData"/> 类的不使用字典的实例。
    /// </summary>
    /// <param name="tag">与PRTS数据关联的标签。</param>
    public PrtsData(string tag)
    {
        Tag = tag;
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
        DataJson = JsonSerializer.Serialize(data);
    }
    public PrtsData()
    {
        Tag = string.Empty;
        DataJson = "{}";
    }
}
