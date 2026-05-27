using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.ArknightsDbComponents;
using ArkPlot.Core.Utilities.PrtsComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;
using Newtonsoft.Json.Linq;

namespace ArkPlot.WebDemo.Services;

/// <summary>
/// 活动/章节数据加载与解析服务。
/// </summary>
public class StoryService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StoryService> _logger;

    public StoryService(IWebHostEnvironment env, ILogger<StoryService> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>获取所有指定类型的活动列表。</summary>
    public List<ActivityItem> GetActivities(string actType = "ACTIVITY_STORY")
    {
        var parser = new ReviewTableParser("zh_CN");
        var stories = parser.GetStories(actType);
        return stories.Select((s, i) => new ActivityItem(i, s["name"]?.ToString() ?? "未知", s)).ToList();
    }

    /// <summary>加载指定活动的章节列表。</summary>
    public async Task<List<string>> GetChapterNamesAsync(ActivityItem activity)
    {
        var actInfo = new ActInfo("zh_CN", "ACTIVITY_STORY", activity.Name, activity.RawToken);
        var loader = new AkpStoryLoader(actInfo);
        var names = await loader.GetChapterNamesAsync();
        return names.ToList();
    }

    /// <summary>
    /// 完整流水线：下载章节 → Prts 资源索引 → 预加载 → 解析，返回带 ResourceUrls 的条目。
    /// </summary>
    public async Task<ChapterResult> LoadChapterAsync(ActivityItem activity, string chapterName,
        IProgress<string>? progress = null)
    {
        var actInfo = new ActInfo("zh_CN", "ACTIVITY_STORY", activity.Name, activity.RawToken);
        var loader = new AkpStoryLoader(actInfo);

        // 1. 下载章节
        progress?.Report("正在下载章节内容...");
        await loader.GetAllChapters(new[] { chapterName });

        if (loader.ContentTable.Count == 0)
            return new ChapterResult([], "未成功下载任何内容");

        var plotManager = loader.ContentTable[0];
        if (plotManager.CurrentPlot.Content.Length == 0)
            return new ChapterResult([], "章节内容为空");

        // 2. Prts 资源索引
        progress?.Report("正在加载 Prts 资源索引...");
        var prtsLoaded = false;
        try
        {
            var prts = new PrtsDataProcessor();
            await prts.GetAllData();
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
            plotManager.StartParseLines(parser);
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
}

public record ActivityItem(int Index, string Name, JToken RawToken);

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
