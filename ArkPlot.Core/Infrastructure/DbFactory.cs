using System;
using System.IO;
using System.Text.Json;
using SqlSugar;
using ArkPlot.Core.Model;

namespace ArkPlot.Core.Infrastructure;

/// <summary>
/// 让 SqlSugar 的 IsJson=true 使用 System.Text.Json 而不是默认的 Newtonsoft.Json
/// </summary>
public class SystemTextJsonSerializer : ISerializeService
{
    public string SerializeObject(object value) => JsonSerializer.Serialize(value);
    public string SugarSerializeObject(object value) => JsonSerializer.Serialize(value);
    public T DeserializeObject<T>(string value) => JsonSerializer.Deserialize<T>(value) ?? default!;
}

public static class DbFactory
{
    private static SqlSugarClient? _client;

    public static SqlSugarClient GetClient()
    {
        if (_client != null) return _client;

        var dbPath = Path.Combine(AppContext.BaseDirectory, "arkplot.db");

        _client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                SerializeService = new SystemTextJsonSerializer()
            }
        });

        _client.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(Plot),
            typeof(FormattedTextEntry),
            typeof(PrtsData),
            typeof(Act),
            typeof(PicDescription),
            typeof(StoryChapter),
            typeof(SyncState),
            typeof(PrtsResource),
            typeof(PrtsPortraitLink)
        );

        return _client;
    }
}
