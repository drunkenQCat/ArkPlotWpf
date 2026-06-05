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
    private static string? _testConnectionString;

    public static SqlSugarClient GetClient()
    {
        if (_client != null) return _client;

        var connectionString = _testConnectionString
            ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "arkplot.db")}";

        var isMemoryDb = connectionString.Contains(":memory:");

        _client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = !isMemoryDb,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                SerializeService = new SystemTextJsonSerializer()
            }
        });

        _client.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(Act),
            typeof(StoryChapter),
            typeof(Plot),
            typeof(FormattedTextEntry),
            typeof(SyncState),
            typeof(PicDescription),
            typeof(PrtsData),
            typeof(PrtsResource),
            typeof(PrtsPortraitLink)
        );

        // 建唯一索引（仅约束 StoryChapterId > 0 的记录，避免与旧数据 StoryChapterId=0 冲突）
        var indexExists = _client.Ado.GetInt(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='uk_plot_act_chapter'") > 0;
        if (!indexExists)
            _client.Ado.ExecuteCommand(
                "CREATE UNIQUE INDEX uk_plot_act_chapter ON Plot(ActId, StoryChapterId) WHERE StoryChapterId > 0");

        return _client;
    }

    /// <summary>
    /// 测试用：设置连接字符串并重置单例，下次 GetClient() 将用新连接创建。
    /// 传入 "Data Source=:memory:" 即可使用内存数据库。
    /// </summary>
    public static void ConfigureForTesting(string connectionString)
    {
        _client?.Dispose();
        _client = null;
        _testConnectionString = connectionString;
    }

    /// <summary>
    /// 测试结束：清理单例，恢复默认行为。
    /// </summary>
    public static void Reset()
    {
        _client?.Dispose();
        _client = null;
        _testConnectionString = null;
    }
}
