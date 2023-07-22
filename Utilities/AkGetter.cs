using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArkPlotWpf.Utilities;

internal partial class AkGetter
{
    // 从GitHub拿到章节的文件名以及相应的所有内容
    private readonly string totalPlotsUrl = "https://github.com/Kengxxiao/ArknightsGameData/tree/master/zh_CN/gamedata/story/activities/";
    public Dictionary<string, string> ContentTable { get; } = new ();

    private readonly List<Task> tasks = new ();
    private readonly string blobSub = ""; //对于blob，github和gitee不太一样
    private readonly string rawUrl = "https://raw.githubusercontent.com";
    private readonly Regex urlsReg = GithubPlotsRegex();

    public event EventHandler<ChapterLoadedEventArgs>? ChapterLoaded;

    public AkGetter(string? active, bool isGitee = false)
    {
        if (isGitee)
        {
            totalPlotsUrl = "https://gitee.com/dr_cat/ArknightsGameData/tree/master/zh_CN/gamedata/story/activities/";
            rawUrl = "https://gitee.com";
            urlsReg = GiteePlotsRegex();
            blobSub = "raw";
        }
        totalPlotsUrl += active;
    }
        
    public async Task GetAllChapters()
    {
        var chapterUrlTable =  await GetChapterList();
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
    // 获取某一个章节的内容
    private async Task<Dictionary<string, string>?> GetChapterList()
    {
        var content = await GetAsync(totalPlotsUrl);
        if (content is null)
        {
            return null;
        }
        Dictionary<string, string> urls = new ();
        //遍历我们查找的的结果
        foreach (Match match in urlsReg.Matches(content))
        {
            var chapterUrl = rawUrl + match.Groups[1].Value;
            chapterUrl = chapterUrl.Replace(@"/blob", blobSub);
            var title = Regex.Match(chapterUrl , @"(level.*)\.txt").Groups[1].Value;
            if (title  == "") continue;
            urls.Add(title,chapterUrl);
        }
        return urls;
    }

    private async Task DownloadChapters(string title, Dictionary<string, string> links)
    {
        var cptLink = links[title];
        var content = await GetAsync(cptLink);
        Console.WriteLine($"{title} 已加载");
        ContentTable.Add(title, content);
    }

    [GeneratedRegex("<a class=\"js-navigation-open.*\"[^>]+href=[\"'](.*?)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled, "zh-CN")]
    private static partial Regex GithubPlotsRegex();
    [GeneratedRegex("href=[\"'](.*?/blob/master.*?)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled, "zh-CN")]
    private static partial Regex GiteePlotsRegex();
}

class  ChapterLoadedEventArgs : EventArgs
{
    public ChapterLoadedEventArgs(string title)
    {
        Title = title;
    }

    public string Title { get; }
}