using ArkPlotWpf.Model;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace ArkPlotWpf.Utilities;

internal class AkpGetter
{
    // 从GitHub拿到章节的文件名以及相应的所有内容
    private readonly JToken storyTokens;
    private readonly string lang;
    readonly NotificationBlock notifyBlock = NotificationBlock.Instance;
    private readonly List<Task> tasks = new();

    private string GetRawUrl()
    {
        if (lang == "zh_CN")
        {
            return $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/{lang}/gamedata/story/";
        }

        return $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/master/{lang}/gamedata/story/";
    }

    public List<Plot> ContentTable { get; private set; } = new();

    public AkpGetter(ActInfo info)
    {
        lang = info.Lang;
        storyTokens = info.Tokens;
    }

    public async Task GetAllChapters()
    {
        var chapterUrlTable = GetChapterUrls();
        foreach (var chapter in chapterUrlTable)
        {
            async Task GetSingleChapter()
            {
                var content = await NetworkUtility.GetAsync(chapter.Value);
                notifyBlock.OnChapterLoaded(new ChapterLoadedEventArgs(chapter.Key));
                ContentTable.Add(new(chapter.Key, new StringBuilder(content)));
            }

            tasks.Add(GetSingleChapter());
        }
        await Task.WhenAll(tasks);
        ContentTable = ContentTable.OrderBy(plot =>
        {
            var index = chapterUrlTable.Keys.ToList().IndexOf(plot.Title);
            return index;
        }).ToList();
    }

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
