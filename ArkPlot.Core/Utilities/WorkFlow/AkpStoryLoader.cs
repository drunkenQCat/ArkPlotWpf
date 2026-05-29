using System.Text.RegularExpressions;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.PrtsComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;

namespace ArkPlot.Core.Utilities.WorkFlow;

/// <summary>
/// 从GitHub获取明日方舟各个章节数据的类。
/// </summary>
public class AkpStoryLoader
{
    public string StoryName { get; }
    private readonly string lang;
    private readonly List<StoryChapter> _chapters;

    private readonly NotificationBlock notifyBlock = NotificationBlock.Instance;
    private readonly List<Task> tasks = new();

    /// <param name="act">当前活动</param>
    /// <param name="chapters">该活动下的章节列表</param>
    public AkpStoryLoader(Act act, List<StoryChapter> chapters)
    {
        StoryName = act.Name;
        lang = act.Lang;
        _chapters = chapters;
    }

    /// <summary>
    /// 当前活动内所有章节的内容。
    /// </summary>
    public List<PlotManager> ContentTable { get; private set; } = new();

    /// <summary>
    /// 获取GitHub上对应本次活动的RAW数据URL的开头。
    /// </summary>
    private string GetRawUrl()
    {
        if (lang == "zh_CN")
            return $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/{lang}/gamedata/story/";

        return $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/master/{lang}/gamedata/story/";
    }

    /// <summary>
    /// 获取所有章节名称。
    /// </summary>
    public Task<IEnumerable<string>> GetChapterNamesAsync()
    {
        var chapterUrlTable = GetChapterUrls();
        return Task.FromResult(chapterUrlTable.Keys.AsEnumerable());
    }

    /// <summary>
    /// 下载所有章节的文本。
    /// </summary>
    public async Task GetAllChapters()
    {
        var chapterUrlTable = GetChapterUrls();
        await GetAllChapters(chapterUrlTable.Keys);
    }

    /// <summary>
    /// 下载指定章节的文本内容。
    /// </summary>
    /// <param name="chaptersToLoad">需要加载的章节名称列表。</param>
    public async Task GetAllChapters(IEnumerable<string> chaptersToLoad)
    {
        var chapterUrlTable = GetChapterUrls();
        var filteredChapters = chapterUrlTable
            .Where(kvp => chaptersToLoad.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        foreach (var chapter in filteredChapters)
        {
            async Task GetSingleChapter()
            {
                var content = await NetworkUtility.GetAsync(chapter.Value);
                notifyBlock.OnChapterLoaded(new ChapterLoadedEventArgs(chapter.Key));
                var plot = new PlotManager(chapter.Key, new StringBuilder(content));
                plot.InitializePlot();
                ContentTable.Add(plot);
            }

            tasks.Add(GetSingleChapter());
        }

        await Task.WhenAll(tasks);
        ContentTable = ContentTable.OrderBy(plot =>
        {
            var index = chapterUrlTable.Keys.ToList().IndexOf(plot.CurrentPlot.Title);
            return index;
        }).ToList();
    }

    /// <summary>
    /// 获取预加载信息。
    /// </summary>
    public PreloadSet GetPreloadInfo()
    {
        var resourceSets = ContentTable.Select(c =>
        {
            var pl = new PrtsPreloader(c);
            pl.ParseAndCollectAssets();
            return pl;
        }).ToList();
        var toPreLoad = new PreloadSet();
        foreach (var res in resourceSets) toPreLoad.UnionWith(res.Assets);
        PrtsAssets.Instance.PreLoaded = StringDict.FromEnumerable(toPreLoad);
        return toPreLoad;
    }

    /// <summary>
    /// 预加载所有章节相关的资源。
    /// </summary>
    public async Task PreloadAssetsForAllChapters()
    {
        var toPreLoad = GetPreloadInfo();
        await PrtsResLoader.DownloadAssets(StoryName, toPreLoad);
    }

    public void ParseAllDocuments(string jsonPath)
    {
        var parser = new AkpParser(jsonPath);
        ContentTable.ForEach(p => p.StartParseLines(parser));
    }

    /// <summary>
    /// 构建章节 → 下载URL 的映射。
    /// </summary>
    private Dictionary<string, string> GetChapterUrls()
    {
        var collection =
            from chapter in _chapters
            let variation = chapter.StoryId.Contains("variation") ? ExtractVariationNumber(chapter.StoryId) : ""
            let title = $"{chapter.StoryCode} {chapter.StoryName} {chapter.AvgTag}{variation}"
            let txt = $"{GetRawUrl()}{chapter.StoryTxt}.txt"
            let plot = new KeyValuePair<string, string>(title, txt)
            select plot;
        return collection.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static string ExtractVariationNumber(string storyCode)
    {
        var regex = new Regex(@"variation(\d+)");
        var match = regex.Match(storyCode);
        return match.Success ? match.Groups[1].Value : "";
    }
}
