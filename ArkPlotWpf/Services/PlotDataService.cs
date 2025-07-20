using Microsoft.Data.Sqlite;
using System;
using System.IO;
using ArkPlotWpf.Model;
using System.Text.Json;

namespace ArkPlotWpf.Services;

public class PlotDataService : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    public readonly string actName;
    public readonly long actId;

    public PlotDataService(string initActName = "default")
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
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        InitializeDatabase();
        actId = GetActId(initActName);
    }


    private void InitializeDatabase()
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
            ResourceUrls TEXT,
            PortraitsInfo TEXT,
            CommandSet TEXT,
            FOREIGN KEY (PlotId) REFERENCES Plots(Id) ON DELETE CASCADE
        );
        """;
        entryCommand.ExecuteNonQuery();
    }

    private long GetActId(string actName)
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

    private long? GetPlotId(SqliteCommand command)
    {
        command.CommandText = "SELECT last_insert_rowid();";
        var result = command.ExecuteScalar();
        long? lastId;
        if (result != null && result != DBNull.Value)
        {
            lastId = (long?)result;
        }
        else
        {
            lastId = null;
            Console.WriteLine("Failed to get last insert rowid, fallback to null");
        }

        return lastId;
    }

    public void AddPlot(Plot plot)
    {
        using var transaction = _connection.BeginTransaction();

        var command = _connection.CreateCommand();
        command.CommandText = "INSERT INTO Plots (Title, Content, ActId) VALUES (@title, @content, @actId)";
        command.Parameters.AddWithValue("@title", plot.Title);
        command.Parameters.AddWithValue("@content", plot.Content.ToString());
        command.Parameters.AddWithValue("@actId", actId);
        command.ExecuteNonQuery();

        long plotId = GetPlotId(command) ?? 0;
        foreach (var entry in plot.TextVariants)
        {
            var entryCommand = _connection.CreateCommand();
            entryCommand.CommandText = """
            INSERT INTO FormattedTextEntries
            (PlotId, IndexNo, OriginalText, MdText, MdDuplicateCounter, TypText, Type, IsTagOnly, CharacterName, Dialog, PngIndex, Bg, ResourceUrls, PortraitsInfo, CommandSet)
            VALUES
            (@PlotId, @IndexNo, @OriginalText, @MdText, @MdDuplicateCounter, @TypText, @Type, @IsTagOnly, @CharacterName, @Dialog, @PngIndex, @Bg, @ResourceUrls, @PortraitsInfo, @CommandSet)
            """;
            entryCommand.Parameters.AddWithValue("@PlotId", plotId);
            entryCommand.Parameters.AddWithValue("@IndexNo", entry.Index);
            entryCommand.Parameters.AddWithValue("@OriginalText", entry.OriginalText);
            entryCommand.Parameters.AddWithValue("@MdText", entry.MdText);
            entryCommand.Parameters.AddWithValue("@MdDuplicateCounter", entry.MdDuplicateCounter);
            entryCommand.Parameters.AddWithValue("@TypText", entry.TypText);
            entryCommand.Parameters.AddWithValue("@Type", entry.Type);
            entryCommand.Parameters.AddWithValue("@IsTagOnly", entry.IsTagOnly ? 1 : 0);
            entryCommand.Parameters.AddWithValue("@CharacterName", entry.CharacterName);
            entryCommand.Parameters.AddWithValue("@Dialog", entry.Dialog);
            entryCommand.Parameters.AddWithValue("@PngIndex", entry.PngIndex);
            entryCommand.Parameters.AddWithValue("@Bg", entry.Bg);
            entryCommand.Parameters.AddWithValue("@ResourceUrls", JsonSerializer.Serialize(entry.ResourceUrls));
            entryCommand.Parameters.AddWithValue("@PortraitsInfo", JsonSerializer.Serialize(entry.PortraitsInfo));
            entryCommand.Parameters.AddWithValue("@CommandSet", JsonSerializer.Serialize(entry.CommandSet));

            entryCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public Plot? GetPlotByName(string title)
    {
        // 查询 Plot 内容
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id, Content FROM Plots WHERE Title = @title";
        command.Parameters.AddWithValue("@title", title);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        var plotId = reader.GetInt32(0);
        var plot = new Plot(
            title,
            new StringBuilder(reader.GetString(1))
        );

        // 查询 TextVariants
        using var entryCommand = _connection.CreateCommand();
        entryCommand.CommandText = "SELECT * FROM FormattedTextEntries WHERE PlotId = @plotId";
        entryCommand.Parameters.AddWithValue("@plotId", plotId);
        using var entryReader = entryCommand.ExecuteReader();

        while (entryReader.Read())
        {
            var entry = new FormattedTextEntry
            {
                Index = entryReader.GetInt32(entryReader.GetOrdinal("IndexNo")),
                OriginalText = entryReader.GetString(entryReader.GetOrdinal("OriginalText")),
                MdText = entryReader.GetString(entryReader.GetOrdinal("MdText")),
                MdDuplicateCounter = entryReader.GetInt32(entryReader.GetOrdinal("MdDuplicateCounter")),
                TypText = entryReader.GetString(entryReader.GetOrdinal("TypText")),
                Type = entryReader.GetString(entryReader.GetOrdinal("Type")),
                IsTagOnly = entryReader.GetInt32(entryReader.GetOrdinal("IsTagOnly")) == 1,
                CharacterName = entryReader.GetString(entryReader.GetOrdinal("CharacterName")),
                Dialog = entryReader.GetString(entryReader.GetOrdinal("Dialog")),
                PngIndex = entryReader.GetInt32(entryReader.GetOrdinal("PngIndex")),
                Bg = entryReader.GetString(entryReader.GetOrdinal("Bg")),
                // 处理可能的 null 引用赋值，若反序列化结果为 null，则使用默认值
                ResourceUrls = JsonSerializer.Deserialize<List<string>>(entryReader.GetString(entryReader.GetOrdinal("ResourceUrls"))) ?? [],
                // 处理可能的 null 引用赋值，若反序列化结果为 null，则使用默认值
                PortraitsInfo = JsonSerializer.Deserialize<PortraitInfo>(entryReader.GetString(entryReader.GetOrdinal("PortraitsInfo"))) ?? new PortraitInfo([], 0),
                // 处理可能的 null 引用赋值，若反序列化结果为 null，则使用默认值
                CommandSet = JsonSerializer.Deserialize<StringDict>(entryReader.GetString(entryReader.GetOrdinal("CommandSet"))) ?? []
            };

            plot.TextVariants.Add(entry);
        }

        return plot;
    }

    public void UpdatePlot(Plot plot)
    {
        using var transaction = _connection.BeginTransaction();

        try
        {
            // 先获取PlotId
            var getIdCommand = _connection.CreateCommand();
            getIdCommand.CommandText = "SELECT Id FROM Plots WHERE Title = @title";
            getIdCommand.Parameters.AddWithValue("@title", plot.Title);
            var plotId = getIdCommand.ExecuteScalar() as long?;

            if (plotId == null)
            {
                throw new Exception($"Plot with title '{plot.Title}' not found");
            }

            // 更新主表
            var command = _connection.CreateCommand();
            command.CommandText = """
            UPDATE Plots 
            SET Content = @content
            WHERE Title = @title
            """;
            command.Parameters.AddWithValue("@content", plot.Content.ToString());
            command.Parameters.AddWithValue("@title", plot.Title);
            command.ExecuteNonQuery();

            // 删除旧的关联数据
            var deleteCommand = _connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM FormattedTextEntries WHERE PlotId = @plotId";
            deleteCommand.Parameters.AddWithValue("@plotId", plotId);
            deleteCommand.ExecuteNonQuery();

            // 插入新的关联数据
            foreach (var entry in plot.TextVariants)
            {
                var entryCommand = _connection.CreateCommand();
                entryCommand.CommandText = """
                INSERT INTO FormattedTextEntries
                (PlotId, IndexNo, OriginalText, MdText, MdDuplicateCounter, TypText, Type, 
                 IsTagOnly, CharacterName, Dialog, PngIndex, Bg, ResourceUrls, PortraitsInfo, CommandSet)
                VALUES
                (@PlotId, @IndexNo, @OriginalText, @MdText, @MdDuplicateCounter, @TypText, @Type, 
                 @IsTagOnly, @CharacterName, @Dialog, @PngIndex, @Bg, @ResourceUrls, @PortraitsInfo, @CommandSet)
                """;
                
                entryCommand.Parameters.AddWithValue("@PlotId", plotId);
                entryCommand.Parameters.AddWithValue("@IndexNo", entry.Index);
                entryCommand.Parameters.AddWithValue("@OriginalText", entry.OriginalText);
                entryCommand.Parameters.AddWithValue("@MdText", entry.MdText);
                entryCommand.Parameters.AddWithValue("@MdDuplicateCounter", entry.MdDuplicateCounter);
                entryCommand.Parameters.AddWithValue("@TypText", entry.TypText);
                entryCommand.Parameters.AddWithValue("@Type", entry.Type);
                entryCommand.Parameters.AddWithValue("@IsTagOnly", entry.IsTagOnly ? 1 : 0);
                entryCommand.Parameters.AddWithValue("@CharacterName", entry.CharacterName);
                entryCommand.Parameters.AddWithValue("@Dialog", entry.Dialog);
                entryCommand.Parameters.AddWithValue("@PngIndex", entry.PngIndex);
                entryCommand.Parameters.AddWithValue("@Bg", entry.Bg);
                entryCommand.Parameters.AddWithValue("@ResourceUrls", JsonSerializer.Serialize(entry.ResourceUrls));
                entryCommand.Parameters.AddWithValue("@PortraitsInfo", JsonSerializer.Serialize(entry.PortraitsInfo));
                entryCommand.Parameters.AddWithValue("@CommandSet", JsonSerializer.Serialize(entry.CommandSet));

                entryCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void DeletePlot(string title)
    {
        using var transaction = _connection.BeginTransaction();

        var deletePlot = _connection.CreateCommand();
        deletePlot.CommandText = "DELETE FROM Plots WHERE Title = @title";
        deletePlot.Parameters.AddWithValue("@title", title);
        deletePlot.ExecuteNonQuery();

        transaction.Commit();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}
