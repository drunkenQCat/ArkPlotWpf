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

    private readonly List<Task> tasks = new ();
    private readonly string rawUrl = "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/story/activities/";

    public event EventHandler<ChapterLoadedEventArgs>? ChapterLoaded;

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
                var content = await GetAsync(chapter.Value);
                // Console.WriteLine($"{chapter.Key} 已加载");
                ChapterLoaded!.Invoke(this, new ChapterLoadedEventArgs(chapter.Key));
                ContentTable.Add(chapter.Key, content);
            }

            tasks.Add(GetSingleChapter());
        }
        await Task.WhenAll(tasks);
    }

    private async Task<Dictionary<string, string>?> GetChapterUrls()
    {
        var jsonContent = await GetJsonContent();
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

    // 获取查询的json
    private async Task<string> GetJsonContent()
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage();
        request.RequestUri = new Uri(plotsJsonRequestUrl);
        request.Method = HttpMethod.Get;
        request.Headers.Add("Accept", "application/json");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return content;
    }
    private static async Task<string> GetAsync(string url)
    {
        // 发送一个request请求
        // Todo: 请求失败发送event

        using var client = new HttpClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[4096];
        var isMoreToRead = true;
        var memoryStream = new MemoryStream();
        do
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                isMoreToRead = false;
            }
            else
            {
                await memoryStream.WriteAsync(buffer.AsMemory(0, read));
            }
        } while (isMoreToRead);
        var fileContent = Encoding.UTF8.GetString(memoryStream.ToArray());
        return fileContent;
    }
}

class  ChapterLoadedEventArgs : EventArgs
{
    public ChapterLoadedEventArgs(string title)
    {
        Title = title;
    }

    public string Title { get; }
}