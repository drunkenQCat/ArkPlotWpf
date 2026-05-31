using ArkPlot.Cli.Pipeline;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;

// 命令行参数解析
if (args.Length > 0 && args[0].Equals("align", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: ArkPlot.Cli align <novel_file.md>");
        return;
    }
    await AlignRunner.RunAsync(args[1]);
    return;
}

if (args.Length > 0 && args[0].Equals("dump-db", StringComparison.OrdinalIgnoreCase))
{
    DbDumpRunner.Run();
    return;
}

if (args.Length > 0 && args[0].Equals("copy-avalonia-db", StringComparison.OrdinalIgnoreCase))
{
    DbDumpRunner.CopyFromAvalonia();
    return;
}

// 清空旧的 PicDescription 记录
var db = DbFactory.GetClient();
var before = db.Queryable<PicDescription>().Count();
db.Deleteable<PicDescription>().ExecuteCommand();
Console.WriteLine($"已清空 PicDescription 表（{before} 条旧记录）");

var tagsJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tags.json");
var pipeline = new CliPipeline(tagsJson);
await pipeline.RunAsync();
