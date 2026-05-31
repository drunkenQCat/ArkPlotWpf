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
        LoadFirstActivityAsync(string lang = "zh_CN", string? actTitleFilter = null)
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

        // 优先使用 DB 缓存，减少网络请求
        var acts = sync.GetActsFromDb(lang);
        if (acts.Count == 0)
        {
            Console.WriteLine("    本地无缓存，从 GitHub 下载...");
            (acts, _) = await sync.DownloadAndSaveAsync(lang);
            Console.WriteLine($"    同步完成：共 {acts.Count} 个活动");
            if (remoteSha != null)
            {
                sync.UpsertSyncState(lang, remoteSha);
                Console.WriteLine($"    已更新本地 SHA");
            }
        }
        else
        {
            Console.WriteLine($"    使用本地缓存：{acts.Count} 个活动");
        }

        // 按标题过滤或取第一个 ACTIVITY_STORY 类型的活动
        Act? activity;
        if (actTitleFilter != null)
            activity = acts.FirstOrDefault(a => a.ActType == "ACTIVITY_STORY" && a.Name.Contains(actTitleFilter));
        else
            activity = acts.FirstOrDefault(a => a.ActType == "ACTIVITY_STORY");

        if (activity == null)
            throw new InvalidOperationException(actTitleFilter != null
                ? $"未找到名称包含「{actTitleFilter}」的 ACTIVITY_STORY 活动。"
                : "未找到任何 ACTIVITY_STORY 类型的活动。");

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

    public static async Task<List<string>> GetChaptersAsync(AkpStoryLoader storyLoader, int count)
    {
        Console.WriteLine("[2/6] 正在获取章节名称...");
        var chapterNames = await storyLoader.GetChapterNamesAsync();
        var chapterList = chapterNames.Take(count).ToList();

        if (chapterList.Count == 0)
        {
            Console.WriteLine("❌ 该活动没有章节。");
            return [];
        }

        for (int i = 0; i < chapterList.Count; i++)
            Console.WriteLine($"    第{i + 1}章：{chapterList[i]}");

        return chapterList;
    }
}
