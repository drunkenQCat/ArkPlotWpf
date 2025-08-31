using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.PrtsComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using Newtonsoft.Json.Linq;
using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;

namespace ArkPlot.Core.Utilities.WorkFlow;

/// <summary>
/// 从GitHub获取明日方舟各个章节数据的类。
/// </summary>
public class AkpStoryLoader
{
    public string StoryName { get; }
    private readonly string lang;

    private readonly NotificationBlock notifyBlock = NotificationBlock.Instance;

    // 从GitHub拿到章节的文件名以及相应的所有内容
    private readonly JToken storyTokens;
    private readonly List<Task> tasks = new();

    public AkpStoryLoader(ActInfo info)
    {
        StoryName = info.Name;
        lang = info.Lang;
        storyTokens = info.Tokens;
    }

    /// <summary>
    /// 当前，活动内所有章节的内容。
    /// </summary>
    public List<PlotManager> ContentTable { get; private set; } = new();

    /// <summary>
    /// 获取GitHub 上对应本次活动的 RAW 数据URL的开头。
    /// </summary>
    /// <returns>GitHub 上的 RAW 数据 URL。</returns>
    private string GetRawUrl()
    {
        if (lang == "zh_CN")
            return $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/{lang}/gamedata/story/";

        return $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/master/{lang}/gamedata/story/";
    }

    /// <summary>
    /// 下载所有章节的文本。
    /// </summary>
    /// <returns>表示异步操作的任务�?/returns>
    /// <returns>包含所有章节名称的集合。</returns>
    public Task<IEnumerable<string>> GetChapterNamesAsync()
    {
        var chapterUrlTable = GetChapterUrls();
        return Task.FromResult(chapterUrlTable.Keys.AsEnumerable());
    }

    /// <summary>
    /// 下载所有章节的文本。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    public async Task GetAllChapters()
    {
        var chapterUrlTable = GetChapterUrls();
        await GetAllChapters(chapterUrlTable.Keys);
    }

    /// <summary>
    /// 下载指定章节的文本内容。
    /// </summary>
    /// <param name="chaptersToLoad">需要加载的章节名称列表。</param>
    /// <returns>表示异步操作的任务。</returns>
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
    /// <returns>预加载信息。</returns>
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
        // 将所有数据写入ResourceCsv.Instance.PreLoaded
        PrtsAssets.Instance.PreLoaded = StringDict.FromEnumerable(toPreLoad);
        return toPreLoad;
    }

    /// <summary>
    /// 预加载所有章节相关的资源。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    public async Task PreloadAssetsForAllChapters()
    {
        var toPreLoad = GetPreloadInfo();
        // 下载所有资源
        await PrtsResLoader.DownloadAssets(StoryName, toPreLoad);
    }

    public void ParseAllDocuments(string jsonPath)
    {
        var parser = new AkpParser(jsonPath);
        ContentTable.ForEach(p =>
        {
            p.StartParseLines(parser);
        });
    }

    /// <summary>
    /// 获取活动内每个章节URL。
    /// </summary>
    /// <returns>包含章节URL的字典。</returns>
    private Dictionary<string, string> GetChapterUrls()
    {
        var plots = storyTokens["infoUnlockDatas"]?.ToObject<JArray>();
        var collection =
            from chapter in plots
            let title = $"{chapter["storyCode"]} {chapter["storyName"]} {chapter["avgTag"]}"
            let txt = $"{GetRawUrl()}{chapter["storyTxt"]}.txt"
            let plot = new KeyValuePair<string, string>(title, txt)
            select plot;
        return collection.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
