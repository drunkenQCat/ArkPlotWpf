using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ArkPlotWpf.Utilities;

internal class AkGetter
{
    // 从GitHub拿到章节的文件名以及相应的所有内容
    private readonly string plotsJsonRequestUrl = "https://github.com/Kengxxiao/ArknightsGameData/tree-commit-info/master/zh_CN/gamedata/story/activities/";
    public Dictionary<string, string> ContentTable { get; } = new ();
    private readonly JObject storyNode;
    readonly NotificationBlock notifyBlock = NotificationBlock.Instance;

    private readonly List<Task> tasks = new ();
    private readonly string rawUrl = "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/story/activities/";

    public AkGetter(string? active)
    {
        plotsJsonRequestUrl += active;
        rawUrl += $"{active}/";
    }
        
    public async Task GetAllChapters()
    {
        var chapterUrlTable =  await GetChapterUrls();
        foreach (var chapter in chapterUrlTable!)
        {
            async Task GetSingleChapter()
            {
                var content = await NetworkUtility.GetAsync(chapter.Value);
                // Console.WriteLine($"{chapter.Key} 已加载");
                notifyBlock.OnChapterLoaded(new ChapterLoadedEventArgs(chapter.Key));
                ContentTable.Add(chapter.Key, content);
            }

            tasks.Add(GetSingleChapter());
        }
        await Task.WhenAll(tasks);
    }

    private async Task<Dictionary<string, string>?> GetChapterUrls()
    {
        var jsonContent = await NetworkUtility.GetJsonContent(plotsJsonRequestUrl);
        var fileNames = GetFileNames(jsonContent);
        var urls = fileNames.ToDictionary(
            name => name, 
            name => rawUrl + name);
        return urls;
    }

    private static List<string> GetFileNames(string jsonContent)
    {
        var result = JObject.Parse(jsonContent);
        var fileNames =
            from tag in result.Properties()
            let name = tag.Name
            where name.EndsWith(".txt")
            select name;
        return fileNames.ToList(); 
    }
}