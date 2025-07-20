using ArkPlotWpf.Data.Entities;
using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using System.IO;

using ArkPlotWpf.Model;
using ArkPlotWpf.Data.Mappers;


namespace ArkPlotWpf.Data.Repositories;


public class PlotRepository
{
    private readonly string _connectionString;
    public readonly string actName;
    public readonly long actId;

public PlotRepository(string initActName = "default", string? customDbPath = null)
    {
        actName = initActName;

        if (string.IsNullOrWhiteSpace(customDbPath))
        {
            var rootPath = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.Combine(rootPath, "Data");
            // 要是没有，就创建 Data 文件夹
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            // 数据库文件路径
            var dbPath = Path.Combine(dataDir, "PlotData.db");
            _connectionString = $"Data Source={dbPath}";
        }
        else
        {
            // Handle cases where customDbPath is already a complete connection string
            if (customDbPath.StartsWith("Data Source="))
            {
                _connectionString = customDbPath;
            }
            else
            {
                _connectionString = $"Data Source={customDbPath}";
            }
        }
        
        Console.WriteLine($"DB path: {_connectionString}");

        // 执行数据库迁移
        DatabaseMigration.Migrate(_connectionString);

        // 开始连接数据库
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        InitializeDatabase(connection);
        actId = GetActId(connection, initActName);
    }

    private void InitializeDatabase(SqliteConnection _connection)
    {
        // 数据库迁移系统已经处理了表创建和索引
        // 这里只需要处理Act相关的初始化逻辑
    }

    private long GetActId(SqliteConnection _connection, string actName)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Acts WHERE Title = @title";
        command.Parameters.AddWithValue("@title", actName);
        var result = command.ExecuteScalar();

        if (result != null && result != DBNull.Value)
        {
            return Convert.ToInt64(result);
        }
        else
        {
            Console.WriteLine($"Act \"{actName}\" not found. Creating a new one...");

            // 插入新 Act
            using var insertCommand = _connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO Acts (Title) VALUES (@title)";
            insertCommand.Parameters.AddWithValue("@title", actName);
            insertCommand.ExecuteNonQuery();

            command.CommandText = "SELECT last_insert_rowid();";
            return (long)command.ExecuteScalar()!;
        }
    }

    public long AddPlot(Plot plot, long actId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        // 先把 Plot 转成 PlotEntity
        var plotEntity = plot.ToEntity(actId);

        var plotSql = """
        INSERT INTO Plots (Title, Content, ActId)
        VALUES (@Title, @Content, @ActId);
        SELECT last_insert_rowid();
        """;

        // 插入 PlotEntity，拿到新生成的 plotId
        var plotId = connection.ExecuteScalar<long>(plotSql, plotEntity, transaction);

        // 把 Plot 的 TextVariants 转成 FormattedTextEntryEntity 列表，带上 plotId
        var entryEntities = plot.TextVariants.Select(entry => entry.ToEntity(plotId));

        var entrySql = """
        INSERT INTO FormattedTextEntries
        (PlotId, IndexNo, OriginalText, MdText, MdDuplicateCounter, TypText, Type, IsTagOnly, CharacterName, Dialog, PngIndex, Bg, MetadataJson)
        VALUES
        (@PlotId, @IndexNo, @OriginalText, @MdText, @MdDuplicateCounter, @TypText, @Type, @IsTagOnly, @CharacterName, @Dialog, @PngIndex, @Bg, @MetadataJson)
        """;

        connection.Execute(entrySql, entryEntities, transaction);

        transaction.Commit();

        return plotId;
    }


    public Plot? GetPlotByTitle(string title, long actId)
    {
        using var connection = CreateConnection();
        connection.Open();

        var plotEntity = connection.QueryFirstOrDefault<PlotEntity>(
            "SELECT * FROM Plots WHERE Title = @Title AND ActId = @ActId",
            new { Title = title, ActId = actId });

        if (plotEntity == null)
            return null;

        var entries = connection.Query<FormattedTextEntryEntity>(
            "SELECT * FROM FormattedTextEntries WHERE PlotId = @PlotId",
            new { PlotId = plotEntity.Id }).AsList();

        var plot = plotEntity.ToModel(entries);

        return plot;
    }

    protected virtual SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
