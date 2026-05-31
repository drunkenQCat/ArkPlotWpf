using ArkPlot.Cli.Pipeline;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;

// 清空旧的 PicDescription 记录
var db = DbFactory.GetClient();
var before = db.Queryable<PicDescription>().Count();
db.Deleteable<PicDescription>().ExecuteCommand();
Console.WriteLine($"已清空 PicDescription 表（{before} 条旧记录）");

var tagsJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tags.json");
var pipeline = new CliPipeline(tagsJson);
await pipeline.RunAsync();
