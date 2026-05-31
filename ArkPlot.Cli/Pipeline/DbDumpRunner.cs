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

    public static void ShowMisalignedCharacters()
    {
        var db = DbFactory.GetClient();
        
        // 1. 找到水晶箭行动的 Act
        var act = db.Queryable<Act>()
            .Where(a => a.Name == "水晶箭行动" && a.Lang == "zh_CN")
            .First();
            
        if (act == null)
        {
            Console.WriteLine("❌ 未找到 '水晶箭行动' 活动");
            return;
        }
        
        Console.WriteLine($"✓ 找到活动: {act.Name} (Id={act.Id})\n");
        
        // 2. 找到 CR-ST-1 的 Plot（通过 StoryChapter 关联）
        var storyChapter = db.Queryable<StoryChapter>()
            .Where(sc => sc.ActId == act.Id && sc.StoryCode == "CR-ST-1")
            .First();
            
        if (storyChapter == null)
        {
            Console.WriteLine("❌ 未找到 CR-ST-1 章节");
            return;
        }
        
        var plot = db.Queryable<Plot>()
            .Where(p => p.ActId == act.Id && p.StoryChapterId == storyChapter.Id)
            .First();
            
        if (plot == null)
        {
            Console.WriteLine($"❌ 未找到 CR-ST-1 的 Plot (StoryChapterId={storyChapter.Id})");
            return;
        }
        
        Console.WriteLine($"✓ 找到章节: {plot.Title} (PlotId={plot.Id})\n");
        
        // 3. 查询该 Plot 中包含"军人"或"军官"的条目
        Console.WriteLine("=== FormattedTextEntry 中带'军人'或'军官'的条目 ===\n");
        
        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plot.Id && 
                       (e.CharacterName.Contains("军人") || e.CharacterName.Contains("军官") || e.CharacterName.Contains("医生")))
            .OrderBy(e => e.Index)
            .ToList();
            
        Console.WriteLine($"找到 {entries.Count} 个条目:\n");
        
        foreach (var entry in entries.Take(10)) // 只显示前10个
        {
            Console.WriteLine($"Index: {entry.Index}");
            Console.WriteLine($"CharacterName: {entry.CharacterName}");
            Console.WriteLine($"CharacterCode: {entry.CharacterCode ?? "(null)"}");
            Console.WriteLine($"Type: '{entry.Type}'");
            Console.WriteLine($"IsTagOnly: {entry.IsTagOnly}");
            var origPreview = entry.OriginalText?.Length > 120 
                ? entry.OriginalText.Substring(0, 120) + "..." 
                : entry.OriginalText;
            Console.WriteLine($"OriginalText: {origPreview}");
            var dialogPreview = entry.Dialog?.Length > 60 
                ? entry.Dialog.Substring(0, 60) + "..." 
                : entry.Dialog;
            Console.WriteLine($"Dialog: {dialogPreview}");
            Console.WriteLine();
        }
        
        // 也展示军人/军官/医生条目及其前面的 charslot
        Console.WriteLine("\n=== 军人/军官/医生条目前的 charslot 上下文 ===\n");
        foreach (var entry in entries.Where(e => e.CharacterName?.Contains("医生") == true || 
                                                  e.CharacterName?.Contains("军人") == true || 
                                                  e.CharacterName?.Contains("军官") == true).Take(8))
        {
            Console.WriteLine($"  [{entry.Index}] dialog: {entry.CharacterName}");
            Console.WriteLine($"    CharacterCode (DB): {entry.CharacterCode ?? "(null)"}");
            
            // 向前找最近的 charslot
            var prevCharSlots = db.Queryable<FormattedTextEntry>()
                .Where(e => e.PlotId == plot.Id && e.Index < entry.Index && e.Type == "charslot")
                .OrderBy(e => e.Index, OrderByType.Desc)
                .Take(3)
                .ToList();
                
            foreach (var cs in prevCharSlots)
            {
                Console.WriteLine($"  [{cs.Index}] charslot: Code={cs.CharacterCode ?? "(null)"}");
                Console.WriteLine($"    OriginalText: {(cs.OriginalText?.Length > 100 ? cs.OriginalText.Substring(0, 100) : cs.OriginalText)}");
                Console.WriteLine($"    CommandSet: {cs.CommandSet?.Count ?? 0} items");
                if (cs.CommandSet != null && cs.CommandSet.Count > 0)
                {
                    foreach (var kv in cs.CommandSet)
                        Console.WriteLine($"      {kv.Key}={kv.Value}");
                }
            }
            Console.WriteLine();
        }
        
        // 4. 查询 PicDescription 中相关的角色描述
        Console.WriteLine("\n=== PicDescription 相关描述 ===\n");
        
        var codes = entries
            .Where(e => !string.IsNullOrEmpty(e.CharacterCode))
            .Select(e => e.CharacterCode!)
            .Distinct()
            .ToList();
            
        Console.WriteLine($"相关 CharacterCode: {string.Join(", ", codes)}\n");
        
        foreach (var code in codes)
        {
            var desc = db.Queryable<PicDescription>()
                .Where(p => p.DedupKey == code)
                .First();
                
            if (desc != null)
            {
                Console.WriteLine($"DedupKey: {desc.DedupKey}");
                var descPreview = desc.PicDesc.Length > 300 
                    ? desc.PicDesc.Substring(0, 300) + "..." 
                    : desc.PicDesc;
                Console.WriteLine($"PicDesc: {descPreview}");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"DedupKey {code}: 未找到描述\n");
            }
        }
    }
}
