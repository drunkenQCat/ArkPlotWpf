using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace ArkPlotWpf.Data;

/// <summary>
/// 数据库迁移管理类，用于处理数据库架构的版本升级
/// </summary>
public static class DatabaseMigration
{
    private const string VersionTableName = "DatabaseVersion";
    private const int CurrentVersion = 2; // 当前数据库版本

    /// <summary>
    /// 执行数据库迁移
    /// </summary>
    /// <param name="connectionString">数据库连接字符串</param>
    public static void Migrate(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // 创建版本表
        CreateVersionTable(connection);

        // 获取当前版本
        var currentDbVersion = GetCurrentVersion(connection);

        // 执行迁移
        if (currentDbVersion < 1)
        {
            MigrateToVersion1(connection);
        }

        if (currentDbVersion < 2)
        {
            MigrateToVersion2(connection);
        }

        // 更新版本号
        UpdateVersion(connection, CurrentVersion);
    }

    private static void CreateVersionTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
        CREATE TABLE IF NOT EXISTS {VersionTableName} (
            Id INTEGER PRIMARY KEY,
            Version INTEGER NOT NULL,
            AppliedAt TEXT NOT NULL
        );
        """;
        command.ExecuteNonQuery();

        // 插入初始版本记录（如果不存在）
        command.CommandText = $"""
        INSERT OR IGNORE INTO {VersionTableName} (Id, Version, AppliedAt)
        VALUES (1, 0, datetime('now'));
        """;
        command.ExecuteNonQuery();
    }

    private static int GetCurrentVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Version FROM {VersionTableName} WHERE Id = 1";
        var result = command.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    private static void UpdateVersion(SqliteConnection connection, int version)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
        UPDATE {VersionTableName} 
        SET Version = @Version, AppliedAt = datetime('now')
        WHERE Id = 1
        """;
        command.Parameters.AddWithValue("@Version", version);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 迁移到版本1：创建基础表结构
    /// </summary>
    private static void MigrateToVersion1(SqliteConnection connection)
    {
        Console.WriteLine("执行数据库迁移到版本1...");

        // 创建Acts表
        using var actCommand = connection.CreateCommand();
        actCommand.CommandText = """
        CREATE TABLE IF NOT EXISTS Acts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL UNIQUE
        );
        """;
        actCommand.ExecuteNonQuery();

        // 创建Plots表
        using var plotsCommand = connection.CreateCommand();
        plotsCommand.CommandText = """
        CREATE TABLE IF NOT EXISTS Plots (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL UNIQUE,
            Content TEXT NOT NULL,
            ActId INTEGER NOT NULL,
            FOREIGN KEY (ActId) REFERENCES Acts(Id) ON DELETE CASCADE
        );
        """;
        plotsCommand.ExecuteNonQuery();

        // 创建FormattedTextEntries表
        using var entryCommand = connection.CreateCommand();
        entryCommand.CommandText = """
        CREATE TABLE IF NOT EXISTS FormattedTextEntries (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PlotId INTEGER NOT NULL,
            IndexNo INTEGER,
            OriginalText TEXT,
            MdText TEXT,
            MdDuplicateCounter INTEGER,
            TypText TEXT,
            Type TEXT,
            IsTagOnly INTEGER,
            CharacterName TEXT,
            Dialog TEXT,
            PngIndex INTEGER,
            Bg TEXT,
            MetadataJson TEXT,
            FOREIGN KEY (PlotId) REFERENCES Plots(Id) ON DELETE CASCADE
        );
        """;
        entryCommand.ExecuteNonQuery();

        // 创建PrtsData表
        using var prtsDataCommand = connection.CreateCommand();
        prtsDataCommand.CommandText = """
        CREATE TABLE IF NOT EXISTS PrtsData (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Tag TEXT NOT NULL UNIQUE,
            DataJson TEXT NOT NULL
        );
        """;
        prtsDataCommand.ExecuteNonQuery();

        Console.WriteLine("数据库迁移到版本1完成");
    }

    /// <summary>
    /// 迁移到版本2：添加索引优化
    /// </summary>
    private static void MigrateToVersion2(SqliteConnection connection)
    {
        Console.WriteLine("执行数据库迁移到版本2...");

        // 为FormattedTextEntries表添加索引
        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
        CREATE INDEX IF NOT EXISTS IX_FormattedTextEntries_PlotId ON FormattedTextEntries(PlotId);
        CREATE INDEX IF NOT EXISTS IX_FormattedTextEntries_IndexNo ON FormattedTextEntries(IndexNo);
        CREATE INDEX IF NOT EXISTS IX_FormattedTextEntries_Type ON FormattedTextEntries(Type);
        CREATE INDEX IF NOT EXISTS IX_FormattedTextEntries_CharacterName ON FormattedTextEntries(CharacterName);
        """;
        indexCommand.ExecuteNonQuery();

        // 为PrtsData表添加索引
        using var prtsDataIndexCommand = connection.CreateCommand();
        prtsDataIndexCommand.CommandText = """
        CREATE INDEX IF NOT EXISTS IX_PrtsData_Tag ON PrtsData(Tag);
        """;
        prtsDataIndexCommand.ExecuteNonQuery();

        Console.WriteLine("数据库迁移到版本2完成");
    }

    /// <summary>
    /// 获取数据库版本信息
    /// </summary>
    /// <param name="connectionString">数据库连接字符串</param>
    /// <returns>版本信息</returns>
    public static string GetVersionInfo(string connectionString)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"""
            SELECT Version, AppliedAt FROM {VersionTableName} WHERE Id = 1
            """;
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var version = reader.GetInt32(0);
                var appliedAt = reader.GetString(1);
                return $"数据库版本: {version}, 最后更新: {appliedAt}";
            }
            
            return "数据库版本信息不可用";
        }
        catch (Exception ex)
        {
            return $"获取版本信息失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 检查是否需要迁移
    /// </summary>
    /// <param name="connectionString">数据库连接字符串</param>
    /// <returns>是否需要迁移</returns>
    public static bool NeedsMigration(string connectionString)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT Version FROM {VersionTableName} WHERE Id = 1";
            var result = command.ExecuteScalar();
            
            if (result == null)
                return true;

            var currentVersion = Convert.ToInt32(result);
            return currentVersion < CurrentVersion;
        }
        catch
        {
            return true; // 如果出现异常，认为需要迁移
        }
    }
} 