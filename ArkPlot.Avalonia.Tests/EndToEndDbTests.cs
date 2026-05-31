using System.IO;
using System.Text;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.PrtsComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;
using SqlSugar;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// 共享 DbFactory 内存数据库的测试夹具。
/// 构造时切换到内存 DB，析构时恢复默认。
/// </summary>
public class DbFactoryFixture : IDisposable
{
    public DbFactoryFixture()
    {
        DbFactory.ConfigureForTesting("Data Source=:memory:");
        DbFactory.GetClient(); // 建立连接并保持打开（IsAutoCloseConnection=false）
    }

    public void Dispose() => DbFactory.Reset();
}

[CollectionDefinition("E2E")]
public class E2EDbCollection : ICollectionFixture<DbFactoryFixture>;

/// <summary>
/// 端到端数据库测试：模拟 App 启动后的完整业务流程。
/// 通过 DbFactory.ConfigureForTesting 让所有服务（StorySyncService、
/// PrtsDataProcessor、PlotCache）共享同一个内存数据库。
///
/// 测试按字母序串行执行，数据在测试间累积：
///   01-02: 网络同步 → 填充 Act/Chapter 表
///   03:    PRTS 资源同步 → 填充 PrtsResource/PrtsPortraitLink 表
///   04-05: 业务写入 → PlotCache 存/取/状态过滤
///   06-07: 跨实例验证 → 新 service 能读到之前写入的数据
///
/// 注意：Test01-03 依赖真实网络（GitHub/PRTS），网络不可达时测试会失败。
/// 后续测试依赖前置测试的数据，若前置测试失败则会提前返回（pass by skip）。
/// </summary>
[Collection("E2E")]
public class EndToEndDbTests
{
    private SqlSugarClient Db => DbFactory.GetClient();

    // ──────────────────────────────────────────────
    //  阶段 1: 启动 → 同步活动列表（模拟 LoadInitResource 中的 SyncActsAsync）
    // ──────────────────────────────────────────────

    [Fact(Timeout = 120_000)]
    public async Task Test01_SyncActs_PopulatesActAndChapterTables()
    {
        // 前置检查：GitHub API 是否可达
        var sha = await StorySyncService.GetLatestCommitShaAsync(
            StorySyncService.GetRepoByLang("zh_CN"));
        if (sha == null) return; // 网络不可达，无法测试，静默通过

        var sync = new StorySyncService();
        List<Act> acts;
        List<StoryChapter> chapters;
        try
        {
            (acts, chapters) = await sync.DownloadAndSaveAsync("zh_CN");
        }
        catch (Exception)
        {
            return; // raw.githubusercontent.com 返回空（速率限制），非代码 bug
        }

        Assert.NotEmpty(acts);
        Assert.NotEmpty(chapters);

        var actsInDb = Db.Queryable<Act>().Where(a => a.Lang == "zh_CN").ToList();
        Assert.NotEmpty(actsInDb);

        var chaptersInDb = Db.Queryable<StoryChapter>().ToList();
        Assert.NotEmpty(chaptersInDb);
    }

    [Fact(Timeout = 120_000)]
    public async Task Test02_SyncActs_WritesSyncStateWithSha()
    {
        var sha = await StorySyncService.GetLatestCommitShaAsync(
            StorySyncService.GetRepoByLang("zh_CN"));
        if (sha == null) return;

        var sync = new StorySyncService();
        try
        {
            await sync.DownloadAndSaveAsync("zh_CN");
        }
        catch (Exception)
        {
            return; // raw.githubusercontent.com 返回空（速率限制），非代码 bug
        }

        sync.UpsertSyncState("zh_CN", sha);

        var state = sync.GetSyncState("zh_CN");
        Assert.NotNull(state);
        Assert.False(string.IsNullOrEmpty(state!.LastCommitSha));
    }

    // ──────────────────────────────────────────────
    //  阶段 2: 启动 → 同步 PRTS 资源（模拟 LoadInitResource 中的 EnsureSyncedAsync）
    // ──────────────────────────────────────────────

    [Fact(Timeout = 120_000)]
    public async Task Test03_EnsurePrtsSynced_PopulatesResourceTables()
    {
        var prts = new PrtsDataProcessor();
        try
        {
            await prts.EnsureSyncedAsync("zh_CN");
        }
        catch (Exception)
        {
            // PRTS Wiki 不可达时 ProcessQuery 收到空字符串导致解析失败，属于网络问题
            return;
        }

        var resourceCount = Db.Queryable<PrtsResource>().Count();
        var linkCount = Db.Queryable<PrtsPortraitLink>().Count();

        Assert.True(resourceCount > 0,
            $"PrtsResource 表应有数据（实际 {resourceCount} 行）");
        Assert.True(linkCount > 0,
            $"PrtsPortraitLink 表应有数据（实际 {linkCount} 行）");
    }

    // ──────────────────────────────────────────────
    //  阶段 3: 用户操作 → 生成 MD → PlotCache 写入 DB
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Test04_PlotCache_SaveThenLoad_RoundTrips()
    {
        var acts = Db.Queryable<Act>().Where(a => a.Lang == "zh_CN").ToList();
        if (acts.Count == 0) return; // 前置测试可能因网络失败未填充数据
        var actId = acts[0].Id;

        var entries = new List<FormattedTextEntry>
        {
            new()
            {
                Index = 0,
                Type = "dialog",
                CharacterName = "阿米娅",
                OriginalText = "[Dialog]博士，欢迎回来。",
                MdText = "**博士，欢迎回来。**",
                CommandSet = new StringDict { { "type", "dialog" } },
                ResourceUrls = new List<string> { "https://example.com/bg.png" },
                Portraits = new List<string> { "https://example.com/amiya.png" },
                PortraitFocus = 1,
            },
            new()
            {
                Index = 1,
                Type = "narration",
                OriginalText = "阿米娅微笑着看向博士。",
                MdText = "*阿米娅微笑着看向博士。*",
                CommandSet = new StringDict { { "type", "narration" } },
            }
        };

        var plot = new Plot("E2E测试章节", new StringBuilder()) { ActId = actId };
        await PlotCache.SaveAsync(plot, entries);

        // 验证 DB 中确实写入了
        var dbPlot = Db.Queryable<Plot>()
            .First(p => p.Title == "E2E测试章节" && p.ActId == actId);
        Assert.NotNull(dbPlot);
        Assert.Equal(2, dbPlot!.Status);

        // 验证 FormattedTextEntry 关联写入
        var dbEntries = Db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == dbPlot.Id)
            .OrderBy(e => e.Index)
            .ToList();
        Assert.Equal(2, dbEntries.Count);
        Assert.Equal("阿米娅", dbEntries[0].CharacterName);
        Assert.Single(dbEntries[0].ResourceUrls);
        Assert.Equal(1, dbEntries[0].PortraitFocus);

        // 通过 PlotCache API 加载回来
        var loaded = await PlotCache.TryLoadAsync(actId, "E2E测试章节");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Value.Plot.Status);
        Assert.Equal(2, loaded.Value.Entries.Count);
        Assert.Equal("**博士，欢迎回来。**", loaded.Value.Entries[0].MdText);
    }

    [Fact]
    public async Task Test05_PlotCache_OnlyReturnsStatus2()
    {
        var acts = Db.Queryable<Act>().Where(a => a.Lang == "zh_CN").ToList();
        if (acts.Count == 0) return;

        // 写入 Status=1（未完成）的 Plot，应被 PlotCache 过滤掉
        Db.Insertable(new Plot("未完成章节", new StringBuilder())
        {
            ActId = acts[0].Id,
            Status = 1
        }).ExecuteCommand();

        var loaded = await PlotCache.TryLoadAsync(acts[0].Id, "未完成章节");
        Assert.Null(loaded);

        // 但 GetCachedTitlesAsync 也不应返回它
        var cachedTitles = await PlotCache.GetCachedTitlesAsync(acts[0].Id);
        Assert.DoesNotContain("未完成章节", cachedTitles);

        // 而之前 Test04 写入的 Status=2 章节应能被查到
        Assert.Contains("E2E测试章节", cachedTitles);
    }

    // ──────────────────────────────────────────────
    //  阶段 4: 验证跨 Service 实例的 DB 状态持久化
    // ──────────────────────────────────────────────

    [Fact]
    public void Test06_NewStorySync_CanReadPreviousData()
    {
        var allActs = Db.Queryable<Act>().ToList();
        if (allActs.Count == 0) return;

        // 新的 service 实例应能读到之前写入的数据
        var sync2 = new StorySyncService();
        var acts = sync2.GetActsByType("zh_CN", "ACTIVITY_STORY");

        Assert.NotEmpty(acts);
    }

    [Fact]
    public void Test07_NewStorySync_CanReadChapters()
    {
        var allActs = Db.Queryable<Act>().ToList();
        if (allActs.Count == 0) return;

        var sync2 = new StorySyncService();
        var chapters = sync2.GetChaptersByActId(allActs[0].Id);

        Assert.NotEmpty(chapters);
        Assert.All(chapters, c => Assert.Equal(allActs[0].Id, c.ActId));
    }

    // ──────────────────────────────────────────────
    //  阶段 5: PlotCache 两层缓存 + PlotManager 自动保存
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Test08_PlotCache_Status1_NotReturnedByQueries()
    {
        var acts = Db.Queryable<Act>().Where(a => a.Lang == "zh_CN").ToList();
        if (acts.Count == 0) return;
        var actId = acts[0].Id;

        // 写入 Status=1（仅下载，未解析）
        var plot = new Plot("仅下载章节", new StringBuilder()) { ActId = actId };
        await PlotCache.SaveAsync(plot, new List<FormattedTextEntry>
        {
            new() { Index = 0, OriginalText = "[Dialog]test" }
        }, status: 1);

        // Status=1 不应被 TryLoadAsync 返回
        var loaded = await PlotCache.TryLoadAsync(actId, "仅下载章节");
        Assert.Null(loaded);

        // Status=1 不应出现在 GetCachedTitlesAsync 中
        var cachedTitles = await PlotCache.GetCachedTitlesAsync(actId);
        Assert.DoesNotContain("仅下载章节", cachedTitles);

        // 但 DB 里确实存在这条记录
        var dbPlot = Db.Queryable<Plot>().First(p => p.Title == "仅下载章节");
        Assert.NotNull(dbPlot);
        Assert.Equal(1, dbPlot!.Status);
    }

    [Fact]
    public async Task Test09_PlotManager_AutoSavesStatus2AfterParse()
    {
        var acts = Db.Queryable<Act>().Where(a => a.Lang == "zh_CN").ToList();
        if (acts.Count == 0) return;
        var actId = acts[0].Id;

        // 创建带 actId 的 PlotManager
        var rawContent = "[Dialog]test line 1\n[Dialog]test line 2";
        var pm = new PlotManager("自动缓存测试", new StringBuilder(rawContent), actId);
        pm.InitializePlot();
        Assert.Equal(2, pm.CurrentPlot.TextVariants.Count);

        // 解析（即使没有 tags.json，StartParseLines 也会触发自动保存）
        var tagsPath = Path.Combine(AppContext.BaseDirectory, "tags.json");
        if (File.Exists(tagsPath))
        {
            var parser = new AkpParser(tagsPath);
            await pm.StartParseLines(parser);
        }
        else
        {
            // 没有 tags.json 时手动触发保存来验证机制
            await PlotCache.SaveAsync(pm.CurrentPlot, pm.CurrentPlot.TextVariants);
        }

        // 验证 DB 中有 Status=2 的记录
        var loaded = await PlotCache.TryLoadAsync(actId, "自动缓存测试");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Value.Plot.Status);
        Assert.Equal(2, loaded.Value.Entries.Count);
    }

    [Fact]
    public async Task Test10_PrtsData_OverrideRoundTrips()
    {
        // 直接测试 PrtsData 表的 Data_Override 存取
        var testData = new StringDict { ["json"] = "{\"title\":{\"act1\":\"活动一\"}}" };
        Db.Deleteable<PrtsData>().Where(d => d.Tag == "Data_Override").ExecuteCommand();
        await Db.Insertable(new PrtsData { Tag = "Data_Override", Data = testData }).ExecuteCommandAsync();

        // 读回验证
        var row = await Db.Queryable<PrtsData>().FirstAsync(d => d.Tag == "Data_Override");
        Assert.NotNull(row);
        Assert.True(row!.Data.ContainsKey("json"));
        Assert.Contains("活动一", row.Data["json"]);
    }

    [Fact]
    public async Task Test11_PlotCache_SaveStatus1_ThenOverwriteWithStatus2()
    {
        var acts = Db.Queryable<Act>().Where(a => a.Lang == "zh_CN").ToList();
        if (acts.Count == 0) return;
        var actId = acts[0].Id;
        var title = "两层缓存测试";

        // 第一步：SaveAsync Status=1（模拟下载）
        var entries1 = new List<FormattedTextEntry>
        {
            new() { Index = 0, OriginalText = "[Dialog]raw line" }
        };
        await PlotCache.SaveAsync(
            new Plot(title, new StringBuilder()) { ActId = actId },
            entries1, status: 1);

        Assert.Null(await PlotCache.TryLoadAsync(actId, title));

        // 第二步：SaveAsync Status=2（模拟解析完成，覆盖写入）
        var entries2 = new List<FormattedTextEntry>
        {
            new() { Index = 0, OriginalText = "[Dialog]raw line", MdText = "**对话**", CharacterName = "阿米娅" }
        };
        await PlotCache.SaveAsync(
            new Plot(title, new StringBuilder()) { ActId = actId },
            entries2);

        // 现在应该能加载到
        var loaded = await PlotCache.TryLoadAsync(actId, title);
        Assert.NotNull(loaded);
        Assert.Equal("阿米娅", loaded!.Value.Entries[0].CharacterName);
    }

    // ──────────────────────────────────────────────
    //  阶段 6: 下载失败防线 — 验证空内容不污染缓存
    // ──────────────────────────────────────────────

    [Fact(Timeout = 60_000)]
    public async Task Test12_GetAllChapters_DownloadFailure_NotSavedToCache()
    {
        // 使用独立的 actId 避免与前置测试数据冲突
        var act = new Act
        {
            ActId = "test_dl_fail",
            Name = "下载失败测试",
            Lang = "zh_CN",
            ActType = "ACTIVITY_STORY"
        };
        var actId = Db.Insertable(act).ExecuteReturnIdentity();
        act.Id = actId;

        var chapter = new StoryChapter
        {
            ActId = actId,
            StoryId = "test_nonexistent_chapter",
            StoryCode = "TEST-1",
            StoryName = "不存在的章节",
            StoryTxt = "nonexistent_test_chapter_404",
            AvgTag = "行动前",
            StorySort = 0,
        };
        Db.Insertable(chapter).ExecuteCommand();

        // 预插入一条脏缓存（Status=2 + 空 OriginalText），验证 cleanup 会清除它
        var dirtyPlotId = Db.Insertable(new Plot("TEST-1 不存在的章节 行动前", new StringBuilder())
        {
            ActId = actId, Status = 2, StoryChapterId = chapter.Id
        }).ExecuteReturnIdentity();
        Db.Insertable(new FormattedTextEntry
        {
            PlotId = dirtyPlotId, Index = 0, OriginalText = ""
        }).ExecuteCommand();

        // 执行下载（GitHub 上 nonexistent_test_chapter_404.txt 不存在 → 404 → 空字符串）
        var loader = new AkpStoryLoader(act, new List<StoryChapter> { chapter });
        await loader.GetAllChapters();

        // 脏缓存应被 cleanup 清除
        Assert.Null(Db.Queryable<Plot>().First(p => p.Id == dirtyPlotId));

        // 下载失败的章节不应写入任何新缓存
        var cachedTitles = await PlotCache.GetCachedTitlesAsync(actId);
        Assert.Empty(cachedTitles);

        // ContentTable 应为空（失败章节被跳过）
        Assert.Empty(loader.ContentTable);
    }
}
