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

    public PlotRepository(string initActName = "default")
    {
        actName = initActName;

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
        Console.WriteLine($"DB path: {_connectionString}");

        // 开始连接数据库
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        InitializeDatabase(connection);
        actId = GetActId(connection, initActName);
    }

    private void InitializeDatabase(SqliteConnection _connection)
    {
        using var actCommand = _connection.CreateCommand();
        actCommand.CommandText = """
        CREATE TABLE IF NOT EXISTS Acts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL UNIQUE
        );
        """;
        actCommand.ExecuteNonQuery();

        using var plotsCommand = _connection.CreateCommand();
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

        using var entryCommand = _connection.CreateCommand();
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
        using var connection = new SqliteConnection(_connectionString);
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
        using var connection = new SqliteConnection(_connectionString);
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
}
