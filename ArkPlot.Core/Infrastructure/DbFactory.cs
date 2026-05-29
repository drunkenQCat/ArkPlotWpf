using System;
using System.IO;
using SqlSugar;
using ArkPlot.Core.Model;

namespace ArkPlot.Core.Infrastructure;

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
            IsAutoCloseConnection = true
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
