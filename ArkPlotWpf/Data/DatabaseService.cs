using SqlSugar;
using ArkPlotWpf.Model;
using System;
using System.IO;

namespace ArkPlotWpf.Data;

/// <summary>
/// 数据库服务类，提供数据库操作的统一入口
/// </summary>
public class DatabaseService
{
    private static readonly Lazy<DatabaseService> _instance = new(() => new DatabaseService());
    public static DatabaseService Instance => _instance.Value;

    private readonly SqlSugarClient _db;

    private DatabaseService()
    {
        _db = DatabaseContext.GetDb();
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    public void Initialize()
    {
        try
        {
            // 执行数据库迁移
            DatabaseMigration.Migrate();
            Console.WriteLine("数据库初始化完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"数据库初始化失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 备份数据库
    /// </summary>
    /// <param name="backupPath">备份文件路径</param>
    public void Backup(string backupPath)
    {
        try
        {
            _db.Ado.ExecuteCommand($"BACKUP DATABASE arkplot TO DISK = '{backupPath}'");
            Console.WriteLine($"数据库备份完成: {backupPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"数据库备份失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 恢复数据库
    /// </summary>
    /// <param name="backupPath">备份文件路径</param>
    public void Restore(string backupPath)
    {
        try
        {
            _db.Ado.ExecuteCommand($"RESTORE DATABASE arkplot FROM DISK = '{backupPath}'");
            Console.WriteLine($"数据库恢复完成: {backupPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"数据库恢复失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取数据库统计信息
    /// </summary>
    /// <returns>数据库统计信息</returns>
    public DatabaseStatistics GetStatistics()
    {
        try
        {
            var plotCount = _db.Queryable<Plot>().Count();
            var formattedTextEntryCount = _db.Queryable<FormattedTextEntry>().Count();
            var prtsDataCount = _db.Queryable<PrtsData>().Count();

            return new DatabaseStatistics
            {
                PlotCount = plotCount,
                FormattedTextEntryCount = formattedTextEntryCount,
                PrtsDataCount = prtsDataCount,
                TotalRecords = plotCount + formattedTextEntryCount + prtsDataCount,
                LastUpdated = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取数据库统计信息失败: {ex.Message}");
            return new DatabaseStatistics();
        }
    }

    /// <summary>
    /// 清理数据库
    /// </summary>
    public void Cleanup()
    {
        try
        {
            // 清理空数据
            _db.Deleteable<Plot>().Where(x => string.IsNullOrEmpty(x.Title)).ExecuteCommand();
            _db.Deleteable<FormattedTextEntry>().Where(x => string.IsNullOrEmpty(x.OriginalText)).ExecuteCommand();
            _db.Deleteable<PrtsData>().Where(x => string.IsNullOrEmpty(x.Tag)).ExecuteCommand();

            // 优化数据库
            _db.Ado.ExecuteCommand("VACUUM");
            Console.WriteLine("数据库清理完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"数据库清理失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 检查数据库连接
    /// </summary>
    /// <returns>连接是否正常</returns>
    public bool CheckConnection()
    {
        try
        {
            _db.Ado.ExecuteCommand("SELECT 1");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取数据库大小（字节）
    /// </summary>
    /// <returns>数据库大小</returns>
    public long GetDatabaseSize()
    {
        try
        {
            var dbPath = "arkplot.db";
            if (File.Exists(dbPath))
            {
                var fileInfo = new FileInfo(dbPath);
                return fileInfo.Length;
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// 数据库统计信息
/// </summary>
public class DatabaseStatistics
{
    public int PlotCount { get; set; }
    public int FormattedTextEntryCount { get; set; }
    public int PrtsDataCount { get; set; }
    public int TotalRecords { get; set; }
    public DateTime LastUpdated { get; set; }

    public override string ToString()
    {
        return $"数据库统计信息 - Plot: {PlotCount}, FormattedTextEntry: {FormattedTextEntryCount}, PrtsData: {PrtsDataCount}, 总计: {TotalRecords}, 更新时间: {LastUpdated:yyyy-MM-dd HH:mm:ss}";
    }
}
