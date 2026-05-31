using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// 调试命令：dump PicDescription 表内容
/// </summary>
public static class DbDumpRunner
{
    public static void Run()
    {
        var db = DbFactory.GetClient();
        var records = db.Queryable<PicDescription>().Take(20).ToList();

        Console.WriteLine($"PicDescription 表前 20 条记录：");
        foreach (var r in records)
        {
            Console.WriteLine($"  DedupKey: {r.DedupKey}");
            Console.WriteLine($"  PicDesc:  {r.PicDesc[..Math.Min(100, r.PicDesc.Length)]}...");
            Console.WriteLine();
        }
    }

    public static void CopyFromAvalonia()
    {
        var avaloniaDbPath = @"C:\TechProjects\About_MyRepos\ArkPlot\ArkPlot.Avalonia\bin\Debug\net9.0\arkplot.db";
        var cliDbPath = Path.Combine(AppContext.BaseDirectory, "arkplot.db");

        Console.WriteLine($"从 Avalonia 数据库复制 PicDescription...");
        Console.WriteLine($"  源: {avaloniaDbPath}");
        Console.WriteLine($"  目标: {cliDbPath}");

        // 打开源数据库
        var sourceDb = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={avaloniaDbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });

        // 读取源数据库的 PicDescription
        var sourceRecords = sourceDb.Queryable<PicDescription>().ToList();
        Console.WriteLine($"  源数据库有 {sourceRecords.Count} 条 PicDescription");

        // 写入目标数据库
        var targetDb = DbFactory.GetClient();
        targetDb.Deleteable<PicDescription>().ExecuteCommand();
        targetDb.Insertable(sourceRecords).ExecuteCommand();

        Console.WriteLine($"  ✅ 已复制 {sourceRecords.Count} 条记录到 CLI 数据库");
    }
}
