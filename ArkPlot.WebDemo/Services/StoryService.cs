using System.Linq;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.PrtsComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;
using SqlSugar;

namespace ArkPlot.WebDemo.Services;

/// <summary>
/// 活动/章节数据加载与解析服务。
/// </summary>
public class StoryService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StoryService> _logger;
    private readonly StorySyncService _sync = new();

    public StoryService(IWebHostEnvironment env, ILogger<StoryService> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>获取所有指定类型的活动列表（从数据库读取）。</summary>
    public List<ActivityItem> GetActivities(string actType = "ACTIVITY_STORY")
    {
        var acts = _sync.GetActsByType("zh_CN", actType);
        return acts.Select((a, i) => new ActivityItem(i, a.Id, a.Name)).ToList();
    }

    /// <summary>加载指定活动的章节列表。</summary>
    public async Task<List<string>> GetChapterNamesAsync(ActivityItem activity)
    {
        var chapters = _sync.GetChaptersByActId(activity.ActDbId);
        var loader = new AkpStoryLoader(
            _sync.GetActsFromDb("zh_CN").First(a => a.Id == activity.ActDbId),
            chapters
        );
        var names = await loader.GetChapterNamesAsync();
        return names.ToList();
    }

    /// <summary>
    /// 完整流水线：下载章节 → Prts 资源索引 → 预加载 → 解析，返回带 ResourceUrls 的条目。
    /// </summary>
    public async Task<ChapterResult> LoadChapterAsync(ActivityItem activity, string chapterName,
        IProgress<string>? progress = null)
    {
        var act = _sync.GetActsFromDb("zh_CN").First(a => a.Id == activity.ActDbId);
        var chapters = _sync.GetChaptersByActId(act.Id);
        var loader = new AkpStoryLoader(act, chapters);

        // 1. 下载章节
        progress?.Report("正在下载章节内容...");
        await loader.GetAllChapters(new[] { chapterName });

        if (loader.ContentTable.Count == 0)
            return new ChapterResult([], "未成功下载任何内容");

        var plotManager = loader.ContentTable[0];
        // Content 为空但 TextVariants 有数据 → 来自缓存加载，正常
        if (plotManager.CurrentPlot.Content.Length == 0 && plotManager.CurrentPlot.TextVariants.Count == 0)
            return new ChapterResult([], "章节内容为空");

        // 2. Prts 资源索引
        progress?.Report("正在加载 Prts 资源索引...");
        var prtsLoaded = false;
        try
        {
            var prts = new PrtsDataProcessor();
            await prts.EnsureSyncedAsync();
            prtsLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Prts 索引加载失败，将使用降级模式");
        }

        // 3. 预加载资源
        progress?.Report("正在预加载资源...");
        var entries = plotManager.CurrentPlot.TextVariants;

        if (prtsLoaded)
        {
            _ = loader.GetPreloadInfo();
        }
        else
        {
            try
            {
                var preloader = new PrtsPreloader(plotManager);
                preloader.ParseAndCollectAssets();
            }
            catch
            {
                // 优雅降级
            }
        }

        // 4. 解析文档
        progress?.Report("正在解析文档...");
        var tagsPath = Path.Combine(_env.ContentRootPath, "tags.json");
        if (File.Exists(tagsPath))
        {
            var parser = new AkpParser(tagsPath);
            await plotManager.StartParseLines(parser);
        }

        // 5. 提取有图片的条目
        var imageEntries = entries
            .Where(e => e.ResourceUrls.Count > 0)
            .Select(e => new ImageEntry
            {
                Entry = e,
                Urls = e.ResourceUrls.ToList(),
                Type = e.Type,
                CharacterName = e.CharacterName,
                Dialog = e.Dialog,
                Index = e.Index
            })
            .ToList();

        progress?.Report($"完成！共 {imageEntries.Count} 个图片条目");
        return new ChapterResult(imageEntries, null);
    }

    // ──────────────────────────────────────────────
    //  缓存浏览查询
    // ──────────────────────────────────────────────

    /// <summary>
    /// 获取缓存统计概览（总章节数、已解析数、PRTS 资源数等）。
    /// </summary>
    public CacheStats GetCacheStats()
    {
        var db = DbFactory.GetClient();
        var zhActs = _sync.GetActsFromDb("zh_CN");

        var totalChapters = 0;
        foreach (var a in zhActs)
            totalChapters += db.Queryable<StoryChapter>().Where(ch => ch.ActId == a.Id).Count();

        var statusCounts = db.Queryable<Plot>()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Status, Count = SqlFunc.AggregateCount(g.Status) })
            .ToList();

        var prtsCount = db.Queryable<PrtsResource>().Count();
        var picDescCount = db.Queryable<PicDescription>().Count();

        return new CacheStats
        {
            TotalActs = zhActs.Count,
            TotalChapters = totalChapters,
            ParsedCount = statusCounts.FirstOrDefault(s => s.Status == 2)?.Count ?? 0,
            DownloadedCount = statusCounts.FirstOrDefault(s => s.Status == 1)?.Count ?? 0,
            PrtsResourceCount = prtsCount,
            PicDescCount = picDescCount,
        };
    }

    /// <summary>
    /// 获取指定活动的每章缓存状态列表。
    /// </summary>
    public List<ChapterCacheStatus> GetChapterCacheStatuses(long actId)
    {
        var db = DbFactory.GetClient();

        // 先查章节列表
        var chapters = db.Queryable<StoryChapter>()
            .Where(ch => ch.ActId == actId)
            .OrderBy(ch => ch.StorySort)
            .ToList();

        // 查该活动的已缓存 Plot（包含行数统计）
        var plots = db.Queryable<Plot>()
            .Where(p => p.ActId == actId)
            .Select(p => new
            {
                p.StoryChapterId,
                p.Status,
                LineCount = SqlFunc.Subqueryable<FormattedTextEntry>()
                    .Where(e => e.PlotId == p.Id).Count()
            })
            .ToList();

        var plotLookup = plots.ToDictionary(p => p.StoryChapterId);

        return chapters.Select(ch => new ChapterCacheStatus
        {
            ChapterId = ch.Id,
            StoryCode = ch.StoryCode ?? "",
            StoryName = ch.StoryName ?? "",
            AvgTag = ch.AvgTag,
            Status = plotLookup.TryGetValue(ch.Id, out var p) ? p.Status : -1,
            LineCount = p?.LineCount ?? 0,
            ImageCount = 0,
        }).ToList();
    }

    /// <summary>
    /// 获取已缓存章节的详细内容摘要。
    /// </summary>
    public ChapterDetail? GetCachedChapterDetail(long actId, string title)
    {
        var db = DbFactory.GetClient();
        var plot = db.Queryable<Plot>()
            .First(p => p.ActId == actId && p.Title == title && p.Status == 2);
        if (plot == null) return null;

        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plot.Id)
            .OrderBy(e => e.Index)
            .ToList();

        return new ChapterDetail
        {
            Title = plot.Title,
            TotalLines = entries.Count,
            TypeStats = entries
                .GroupBy(e => e.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            Characters = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.CharacterName))
                .Select(e => e.CharacterName!)
                .Distinct()
                .ToList(),
            ImageCount = entries.Count(e => e.ResourceUrls.Count > 0),
            DialogLines = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Dialog))
                .Select(e => $"{e.CharacterName ?? "旁白"}: {e.Dialog}")
                .Take(10)
                .ToList(),
            BgCount = entries.Count(e => !string.IsNullOrWhiteSpace(e.Bg)),
        };
    }
}

// ──────────────────────────────────────────────
//  缓存浏览 DTO
// ──────────────────────────────────────────────

public record ActivityItem(int Index, long ActDbId, string Name);

public class ChapterResult
{
    public List<ImageEntry> ImageEntries { get; }
    public string? Error { get; }
    public bool Success => Error == null;

    public ChapterResult(List<ImageEntry> entries, string? error)
    {
        ImageEntries = entries;
        Error = error;
    }
}

public class ImageEntry
{
    public FormattedTextEntry Entry { get; init; } = new();
    public List<string> Urls { get; init; } = [];
    public string Type { get; init; } = "";
    public string CharacterName { get; init; } = "";
    public string Dialog { get; init; } = "";
    public int Index { get; init; }

    // 视觉描述结果（UI 绑定）
    public string? Description { get; set; }
    public bool IsDescribing { get; set; }
    public string? DescribeError { get; set; }
}

// ──────────────────────────────────────────────
//  缓存浏览 DTO
// ──────────────────────────────────────────────

public class CacheStats
{
    public int TotalActs { get; init; }
    public int TotalChapters { get; init; }
    public int ParsedCount { get; init; }
    public int DownloadedCount { get; init; }
    public int UncachedCount => TotalChapters - ParsedCount - DownloadedCount;
    public int PrtsResourceCount { get; init; }
    public int PicDescCount { get; init; }
}

public class ChapterCacheStatus
{
    public long ChapterId { get; init; }
    public string StoryCode { get; init; } = "";
    public string StoryName { get; init; } = "";
    public string? AvgTag { get; init; }
    public string DisplayName => $"{StoryCode} {StoryName}".Trim();
    public int Status { get; init; }   // -1=未缓存, 1=仅下载, 2=已解析
    public int LineCount { get; init; }
    public int ImageCount { get; init; }
    public string StatusBadge => Status switch
    {
        2 => "✅ 已解析",
        1 => "⚡ 仅下载",
        _ => "⬜ 未缓存"
    };
}

public class ChapterDetail
{
    public string Title { get; init; } = "";
    public int TotalLines { get; init; }
    public Dictionary<string, int> TypeStats { get; init; } = [];
    public List<string> Characters { get; init; } = [];
    public int ImageCount { get; init; }
    public int BgCount { get; init; }
    public List<string> DialogLines { get; init; } = [];
}
