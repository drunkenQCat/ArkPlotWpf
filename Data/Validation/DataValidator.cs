using ArkPlotWpf.Data.Exceptions;
using ArkPlotWpf.Model;
using System;
using System.Text.Json;

namespace ArkPlotWpf.Data.Validation;

/// <summary>
/// 数据验证器，提供通用的数据验证功能
/// </summary>
public static class DataValidator
{
    /// <summary>
    /// 验证PrtsData模型
    /// </summary>
    /// <param name="prtsData">要验证的PrtsData</param>
    /// <exception cref="DataValidationException">验证失败时抛出</exception>
    public static void ValidatePrtsData(PrtsData prtsData)
    {
        if (prtsData == null)
        {
            throw new DataValidationException("PrtsData", null, "PrtsData不能为null");
        }

        if (string.IsNullOrWhiteSpace(prtsData.Tag))
        {
            throw new DataValidationException("Tag", prtsData.Tag, "Tag不能为空");
        }

        if (prtsData.Tag.Length > 100)
        {
            throw new DataValidationException("Tag", prtsData.Tag, "Tag长度不能超过100个字符");
        }

        if (prtsData.Data == null)
        {
            throw new DataValidationException("Data", null, "Data不能为null");
        }

        // 验证JSON序列化
        try
        {
            var json = JsonSerializer.Serialize(prtsData.Data);
            if (json.Length > 10000) // 10KB限制
            {
                throw new DataValidationException("Data", prtsData.Data, "Data序列化后大小不能超过10KB");
            }
        }
        catch (JsonException ex)
        {
            throw new DataValidationException("Data", prtsData.Data, $"Data序列化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 验证PrtsAssets模型
    /// </summary>
    /// <param name="prtsAssets">要验证的PrtsAssets</param>
    /// <exception cref="DataValidationException">验证失败时抛出</exception>
    public static void ValidatePrtsAssets(PrtsAssets prtsAssets)
    {
        if (prtsAssets == null)
        {
            throw new DataValidationException("PrtsAssets", null, "PrtsAssets不能为null");
        }

        // 验证各个集合
        ValidateStringDict("DataAudio", prtsAssets.DataAudio);
        ValidateStringDict("DataChar", prtsAssets.DataChar);
        ValidateStringDict("DataImage", prtsAssets.DataImage);
        ValidateStringDict("PreLoaded", prtsAssets.PreLoaded);

        // 验证JSON文档
        ValidateJsonDocument("DataOverrideDocument", prtsAssets.DataOverrideDocument);
        ValidateJsonDocument("PortraitLinkDocument", prtsAssets.PortraitLinkDocument);
    }

    /// <summary>
    /// 验证StringDict
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="stringDict">要验证的StringDict</param>
    /// <exception cref="DataValidationException">验证失败时抛出</exception>
    private static void ValidateStringDict(string propertyName, StringDict stringDict)
    {
        if (stringDict == null)
        {
            throw new DataValidationException(propertyName, null, $"{propertyName}不能为null");
        }

        foreach (var kvp in stringDict)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                throw new DataValidationException($"{propertyName}.Key", kvp.Key, $"{propertyName}的键不能为空");
            }

            if (kvp.Key.Length > 200)
            {
                throw new DataValidationException($"{propertyName}.Key", kvp.Key, $"{propertyName}的键长度不能超过200个字符");
            }

            if (kvp.Value != null && kvp.Value.Length > 1000)
            {
                throw new DataValidationException($"{propertyName}.Value", kvp.Value, $"{propertyName}的值长度不能超过1000个字符");
            }
        }
    }

    /// <summary>
    /// 验证JSON文档
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="jsonDocument">要验证的JSON文档</param>
    /// <exception cref="DataValidationException">验证失败时抛出</exception>
    private static void ValidateJsonDocument(string propertyName, JsonDocument jsonDocument)
    {
        if (jsonDocument == null)
        {
            throw new DataValidationException(propertyName, null, $"{propertyName}不能为null");
        }

        try
        {
            var json = jsonDocument.RootElement.GetRawText();
            if (json.Length > 50000) // 50KB限制
            {
                throw new DataValidationException(propertyName, jsonDocument, $"{propertyName}大小不能超过50KB");
            }
        }
        catch (Exception ex)
        {
            throw new DataValidationException(propertyName, jsonDocument, $"{propertyName}验证失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 验证数据库连接字符串
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <exception cref="DataValidationException">验证失败时抛出</exception>
    public static void ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DataValidationException("ConnectionString", connectionString, "连接字符串不能为空");
        }

        if (!connectionString.Contains("Data Source="))
        {
            throw new DataValidationException("ConnectionString", connectionString, "连接字符串格式无效，必须包含Data Source");
        }
    }

    /// <summary>
    /// 验证SQL语句
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <exception cref="DataValidationException">验证失败时抛出</exception>
    public static void ValidateSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new DataValidationException("SQL", sql, "SQL语句不能为空");
        }

        // 检查SQL注入风险
        var dangerousKeywords = new[] { "DROP", "DELETE", "TRUNCATE", "ALTER", "CREATE", "EXEC", "EXECUTE" };
        var upperSql = sql.ToUpperInvariant();
        
        foreach (var keyword in dangerousKeywords)
        {
            if (upperSql.Contains(keyword))
            {
                throw new DataValidationException("SQL", sql, $"SQL语句包含危险关键字: {keyword}");
            }
        }
    }

    /// <summary>
    /// 验证表名
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <exception cref="DataValidationException">验证失败时抛出</exception>
    public static void ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new DataValidationException("TableName", tableName, "表名不能为空");
        }

        if (tableName.Length > 50)
        {
            throw new DataValidationException("TableName", tableName, "表名长度不能超过50个字符");
        }

        // 检查表名是否只包含字母、数字和下划线
        if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            throw new DataValidationException("TableName", tableName, "表名只能包含字母、数字和下划线，且必须以字母或下划线开头");
        }
    }
} 