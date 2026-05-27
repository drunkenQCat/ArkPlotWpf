using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.ArknightsDbComponents;
using ArkPlot.Core.Utilities.WorkFlow;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// Step 1-2: 加载活动列表 + 获取第一章名称。
/// </summary>
public static class ActivityLoader
{
    public static (ActInfo ActInfo, string ActName, AkpStoryLoader StoryLoader) LoadFirstActivity()
    {
        Console.WriteLine("[1/6] 正在加载活动列表...");
        var actsTable = new ReviewTableParser("zh_CN");
        var activities = actsTable.GetStories("ACTIVITY_STORY");

        if (activities.Count == 0)
            throw new InvalidOperationException("未找到任何 ACTIVITY_STORY 类型的活动。");

        var firstAct = activities[0];
        var actName = firstAct["name"]?.ToString() ?? "未知活动";
        Console.WriteLine($"    活动：{actName}");

        var actInfo = new ActInfo("zh_CN", "ACTIVITY_STORY", actName, firstAct);
        var storyLoader = new AkpStoryLoader(actInfo);
        return (actInfo, actName, storyLoader);
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
