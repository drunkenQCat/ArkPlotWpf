using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// Step 1-2: SHA 验证 → 同步活动列表到数据库 → 加载第一个活动。
/// CLI 为 debug 模式：始终从 GitHub 下载，不依赖缓存。
/// </summary>
public static class ActivityLoader
{
    public static async Task<(Act Act, List<StoryChapter> Chapters, string ActName, AkpStoryLoader StoryLoader)>
        LoadFirstActivityAsync(string lang = "zh_CN")
    {
        Console.WriteLine("[1/6] 正在同步活动列表...");
        var sync = new StorySyncService();

        // 获取远程 SHA（仅用于验证，不影响后续流程）
        var repo = StorySyncService.GetRepoByLang(lang);
        var remoteSha = await StorySyncService.GetLatestCommitShaAsync(repo);
        var localSha = sync.GetSyncState(lang)?.LastCommitSha;

        Console.WriteLine($"    仓库     ：{repo}");
        Console.WriteLine($"    远程 SHA ：{remoteSha ?? "（获取失败）"}");
        Console.WriteLine($"    本地 SHA ：{localSha ?? "（无缓存）"}");

        // CLI debug 模式：始终从 GitHub 下载
        Console.WriteLine("    CLI Debug 模式：强制从 GitHub 重新下载...");
        var (acts, _) = await sync.DownloadAndSaveAsync(lang);
        Console.WriteLine($"    同步完成：共 {acts.Count} 个活动");

        // 更新 SHA（验证写入路径）
        if (remoteSha != null)
        {
            sync.UpsertSyncState(lang, remoteSha);
            Console.WriteLine($"    已更新本地 SHA");
        }

        // 取第一个 ACTIVITY_STORY 类型的活动
        var activity = acts.FirstOrDefault(a => a.ActType == "ACTIVITY_STORY");
        if (activity == null)
            throw new InvalidOperationException("未找到任何 ACTIVITY_STORY 类型的活动。");

        var actName = activity.Name;
        Console.WriteLine($"    活动     ：{actName}");

        // 从 DB 读章节列表
        var chapters = sync.GetChaptersByActId(activity.Id);
        Console.WriteLine($"    章节数   ：{chapters.Count}");

        var storyLoader = new AkpStoryLoader(activity, chapters);
        return (activity, chapters, actName, storyLoader);
    }

    public static async Task<string?> GetFirstChapterAsync(AkpStoryLoader storyLoader)
    {
        Console.WriteLine("[2/6] 正在获取章节名称...");
        var chapterNames = await storyLoader.GetChapterNamesAsync();
        var chapterList = chapterNames.ToList();

        if (chapterList.Count == 0)
        {
            Console.WriteLine("❌ 该活动没有章节。");
            return null;
        }

        var firstChapter = chapterList[0];
        Console.WriteLine($"    第一章：{firstChapter}");
        return firstChapter;
    }
}
