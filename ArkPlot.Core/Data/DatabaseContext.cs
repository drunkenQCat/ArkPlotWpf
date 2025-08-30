using SqlSugar;
using ArkPlot.Core.Model;
using System;
using System.Linq;

namespace ArkPlot.Core.Data;

/// <summary>
/// 数据库上下文类，负责管理 SqlSugar 数据库连接和配置
/// </summary>
public class DatabaseContext
{
    private static readonly Lazy<DatabaseContext> _instance = new(() => new DatabaseContext());
    public static DatabaseContext Instance => _instance.Value;

    public SqlSugarClient Db { get; }

    private DatabaseContext()
    {
        Db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=arkplot.db",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices(),
            // 启用日志记录
            AopEvents = new AopEvents
            {
                OnLogExecuting = (sql, parameters) =>
                {
                    Console.WriteLine($"SQL: {sql}");
                    if (parameters?.Length > 0)
                    {
                        Console.WriteLine($"Parameters: {string.Join(", ", parameters.Select(p => $"{p.ParameterName}={p.Value}"))}");
                    }
                }
            }
        });

        // 初始化数据库表结�?
        InitializeTables();
    }

    /// <summary>
    /// 初始化数据库表结�?
    /// </summary>
    private void InitializeTables()
    {
        try
        {
            Db.CodeFirst
                .SetStringDefaultLength(200)
                .InitTables(
                    typeof(Plot),
                    typeof(FormattedTextEntry),
                    typeof(PrtsData)
                );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"数据库初始化失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取数据库连接实�?
    /// </summary>
    public static SqlSugarClient GetDb() => Instance.Db;
}

