using SqlSugar;
using ArkPlot.Core.Model;
using System;
using System.Linq;

namespace ArkPlot.Core.Data;

/// <summary>
/// 数据库迁移类，用于数据库版本管理和数据初始化
/// </summary>
public static class DatabaseMigration
{
    /// <summary>
    /// 执行数据库迁移
    /// </summary>
    public static void Migrate()
    {
        var db = DatabaseContext.GetDb();
        try
        {
            // 创建版本控制表
            CreateVersionTable(db);

            // 获取当前版本
            var currentVersion = GetCurrentVersion(db);

            // 执行迁移
            ExecuteMigrations(db, currentVersion);
            Console.WriteLine("数据库迁移完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"数据库迁移失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 创建版本控制表
    /// </summary>
    private static void CreateVersionTable(SqlSugarClient db)
    {
        // 检查版本表是否存在
        var tableExists = db.DbMaintenance.IsAnyTable("__EFMigrationsHistory");
        if (!tableExists)
        {
            db.CodeFirst.InitTables(typeof(DatabaseVersion));
        }
    }

    /// <summary>
    /// 获取当前数据库版本
    /// </summary>
    private static int GetCurrentVersion(SqlSugarClient db)
    {
        try
        {
            var version = db.Queryable<DatabaseVersion>()
                           .OrderByDescending(x => x.Version)
                           .First();
            return version?.Version ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 执行迁移
    /// </summary>
    private static void ExecuteMigrations(SqlSugarClient db, int currentVersion)
    {
        var migrations = GetMigrations();
        foreach (var migration in migrations.Where(m => m.Version > currentVersion))
        {
            try
            {
                migration.Execute(db);

                // 记录迁移版本
                db.Insertable(new DatabaseVersion
                {
                    Version = migration.Version,
                    Description = migration.Description,
                    AppliedAt = DateTime.Now
                }).ExecuteCommand();
                Console.WriteLine($"执行迁移: {migration.Description} (版本 {migration.Version})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"迁移失败: {migration.Description} - {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 获取所有迁移
    /// </summary>
    private static List<IMigration> GetMigrations()
    {
        return new List<IMigration>
        {
            new Migration_001_InitialSchema(),
            new Migration_002_AddIndexes(),
        };
    }
}

/// <summary>
/// 数据库版本记录
/// </summary>
public class DatabaseVersion
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    public int Version { get; set; }
    [SugarColumn(Length = 500)]
    public string Description { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
}

/// <summary>
/// 迁移接口
/// </summary>
public interface IMigration
{
    int Version { get; }
    string Description { get; }
    void Execute(SqlSugarClient db);
}

/// <summary>
/// 初始架构迁移
/// </summary>
public class Migration_001_InitialSchema : IMigration
{
    public int Version => 1;
    public string Description => "创建初始数据库架构";

    public void Execute(SqlSugarClient db)
    {
        // 创建基础表结构
        db.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(Plot),
            typeof(FormattedTextEntry),
            typeof(PrtsData)
        );
    }
}

/// <summary>
/// 添加索引迁移
/// </summary>
public class Migration_002_AddIndexes : IMigration
{
    public int Version => 2;
    public string Description => "添加数据库索引";

    public void Execute(SqlSugarClient db)
    {
        // 为Plot 表添加索引
        db.CodeFirst.SetStringDefaultLength(200).InitTables(typeof(Plot));

        // 为FormattedTextEntry 表添加索引
        db.CodeFirst.SetStringDefaultLength(200).InitTables(typeof(FormattedTextEntry));

        // 为PrtsData 表添加索引
        db.CodeFirst.SetStringDefaultLength(200).InitTables(typeof(PrtsData));
    }
}