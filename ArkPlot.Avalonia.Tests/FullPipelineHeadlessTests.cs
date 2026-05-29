using System.Text;
using ArkPlot.Avalonia.ViewModels;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities;
using ArkPlot.Core.Utilities.PrtsComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;
using SqlSugar;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// 完整管线集成测试：用内存数据库验证 DB 层的改动能从头跑到尾。
/// 覆盖：模型 DB 读写、管线各阶段、ViewModel 层。
/// </summary>
public class FullPipelineHeadlessTests
{
    private static SqlSugarClient CreateMemoryDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                SerializeService = new SystemTextJsonSerializer()
            }
        });

        db.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(Plot),
            typeof(FormattedTextEntry),
            typeof(PrtsData),
            typeof(Act),
            typeof(PicDescription),
            typeof(StoryChapter),
            typeof(SyncState),
            typeof(PrtsResource),
            typeof(PrtsPortraitLink)
        );

        return db;
    }

    // ──────────────────────────────────────────────
    //  模型层：DB 读写 + IsJson 字段
    // ──────────────────────────────────────────────

    [Fact]
    public void Plot_WithStatus_CanBeSavedAndLoaded()
    {
        var db = CreateMemoryDb();

        var act = new Act { ActId = "test_act", Name = "测试活动", Lang = "zh_CN", ActType = "ACTIVITY_STORY" };
        var actId = db.Insertable(act).ExecuteReturnIdentity();

        var plot = new Plot("GT-1 测试章", new StringBuilder("原始内容"))
        {
            ActId = actId,
            Status = 2
        };
        var plotId = db.Insertable(plot).ExecuteReturnIdentity();

        var loaded = db.Queryable<Plot>().First(p => p.Id == plotId);
        Assert.Equal(2, loaded!.Status);

        // FormattedTextEntry 带 IsJson 字段
        var entry = new FormattedTextEntry
        {
            PlotId = plotId,
            Index = 0,
            Type = "dialog",
            CharacterName = "测试角色",
            Dialog = "测试台词",
            OriginalText = "[TEST] 测试台词",
            MdText = "**测试台词**",
            CommandSet = new StringDict { { "type", "dialog" } },
            ResourceUrls = ["https://example.com/pic1.png"],
            Portraits = ["https://example.com/portrait1.png"],
            PortraitFocus = 1,
        };
        var entryId = db.Insertable(entry).ExecuteReturnIdentity();

        var loadedEntry = db.Queryable<FormattedTextEntry>().First(e => e.Id == entryId);
        Assert.Single(loadedEntry!.ResourceUrls);
        Assert.Single(loadedEntry.Portraits);
        Assert.Equal(1, loadedEntry.PortraitFocus);
        Assert.Equal("dialog", loadedEntry.CommandSet["type"]);

        db.Dispose();
    }

    [Fact]
    public void ActsUpsert_PreservesId()
    {
        var db = CreateMemoryDb();

        var act = new Act { ActId = "1stact", Name = "骑兵与猎人", Lang = "zh_CN", ActType = "ACTIVITY_STORY" };
        var id = db.Insertable(act).ExecuteReturnIdentity();

        var existing = db.Queryable<Act>().First(a => a.ActId == "1stact" && a.Lang == "zh_CN");
        existing!.Name = "Ride to Hunt";
        db.Updateable(existing).WhereColumns(it => new { it.ActId, it.Lang }).ExecuteCommand();

        var reloaded = db.Queryable<Act>().First(a => a.Id == id);
        Assert.Equal("Ride to Hunt", reloaded!.Name);
        Assert.Equal(id, reloaded.Id);

        db.Dispose();
    }

    // ──────────────────────────────────────────────
    //  管线层：完整一章处理流程
    // ──────────────────────────────────────────────

    [Fact]
    public void FullChapter_Pipeline_FromDownloadToExport()
    {
        var db = CreateMemoryDb();

        // 造活动 + 章节
        var act = new Act { ActId = "test_act", Name = "测试活动", Lang = "zh_CN", ActType = "ACTIVITY_STORY" };
        var actId = db.Insertable(act).ExecuteReturnIdentity();

        db.Insertable(new StoryChapter
        {
            ActId = actId, StoryId = "test_01_beg", StoryCode = "TS-1",
            StoryName = "测试章节", StoryTxt = "test/01_beg", AvgTag = "行动前", StorySort = 1
        }).ExecuteCommand();

        // Prts 资源
        db.Insertable(new PrtsResource { ResourceType = "Char", ResourceKey = "char_293_thorns_1", ResourceUrl = "https://media.prts.wiki/d/d0/Avg_char_293_thorns_1.png" }).ExecuteCommand();
        db.Insertable(new PrtsResource { ResourceType = "Image", ResourceKey = "bg_01", ResourceUrl = "https://media.prts.wiki/8/8a/Avg_bg_bg_black.png" }).ExecuteCommand();
        db.Insertable(new PrtsPortraitLink { CharacterCode = "char_293_thorns_1", PortraitName = "char_293_thorns_1", SortOrder = 0 }).ExecuteCommand();

        // 填充 PrtsAssets 到内存（跳过 DB 网络路径）
        var assets = PrtsAssets.Instance;
        var allRes = db.Queryable<PrtsResource>().ToList();
        foreach (var r in allRes.Where(r => r.ResourceType == "Char")) assets.DataChar[r.ResourceKey] = r.ResourceUrl;
        foreach (var r in allRes.Where(r => r.ResourceType == "Image")) assets.DataImage[r.ResourceKey] = r.ResourceUrl;
        var allLinks = db.Queryable<PrtsPortraitLink>().ToList();
        using var ms = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var g in allLinks.GroupBy(l => l.CharacterCode))
            {
                writer.WriteStartObject(g.Key);
                writer.WriteStartArray("array");
                foreach (var link in g.OrderBy(l => l.SortOrder))
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", link.PortraitName);
                    if (!string.IsNullOrEmpty(link.Alias)) writer.WriteString("alias", link.Alias);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        ms.Position = 0;
        assets.PortraitLinkDocument = System.Text.Json.JsonDocument.Parse(ms);
        Assert.True(assets.DataChar.Count > 0);

        // 模拟 GitHub 下载的原始文本
        var rawTxt = """
[HEADER(key="title_test")]
第一关（前）
[Dialog]
测试对话第一行
[Character(name="char_293_thorns_1")]
[Dialog]
测试对话第二行
""";
        var plotMgr = new PlotManager("TS-1 测试章节 行动前", new StringBuilder(rawTxt.TrimStart()));
        plotMgr.InitializePlot();
        var entries = plotMgr.CurrentPlot.TextVariants;
        Assert.NotEmpty(entries);

        // 预加载
        var preloader = new PrtsPreloader(plotMgr);
        preloader.ParseAndCollectAssets();

        // 解析（如果有 tags.json）
        var tagsPath = Path.Combine(AppContext.BaseDirectory, "tags.json");
        if (File.Exists(tagsPath))
        {
            var parser = new AkpParser(tagsPath);
            plotMgr.StartParseLines(parser);
        }

        // 导出 + DB 写入
        var plotToSave = new Plot("TS-1 测试章节", new StringBuilder(rawTxt.TrimStart())) { ActId = actId, Status = 2 };
        var plotId = db.Insertable(plotToSave).ExecuteReturnIdentity();
        foreach (var entry in entries) entry.PlotId = plotId;
        db.Insertable(entries).ExecuteCommand();

        var savedEntries = db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plotId).OrderBy(e => e.Index).ToList();
        Assert.NotEmpty(savedEntries);
        Assert.Equal(entries.Count, savedEntries.Count);

        // IsJson 字段验证
        Assert.All(savedEntries, e =>
        {
            Assert.NotNull(e.CommandSet);
            Assert.NotNull(e.ResourceUrls);
            Assert.NotNull(e.Portraits);
        });

        db.Dispose();
    }

    [Fact]
    public void Pipeline_WithMinimalStoryText()
    {
        var db = CreateMemoryDb();

        var assets = PrtsAssets.Instance;
        assets.DataChar["char_293_thorns_1"] = "https://media.prts.wiki/d/d0/Avg_char_293_thorns_1.png";
        assets.PortraitLinkDocument = System.Text.Json.JsonDocument.Parse(
            """{"char_293_thorns_1":{"array":[{"name":"char_293_thorns_1"}]}}""");

        var storyText = """
[HEADER(key="title_test")]
开始
[Dialog]
对话
""";
        var plotMgr = new PlotManager("TS-1", new StringBuilder(storyText.TrimStart()));
        plotMgr.InitializePlot();

        var preloader = new PrtsPreloader(plotMgr);
        preloader.ParseAndCollectAssets();

        var tagsPath = Path.Combine(AppContext.BaseDirectory, "tags.json");
        if (File.Exists(tagsPath))
        {
            var parser = new AkpParser(tagsPath);
            plotMgr.StartParseLines(parser);
        }

        Assert.NotEmpty(plotMgr.CurrentPlot.TextVariants);
        Assert.Contains(plotMgr.CurrentPlot.TextVariants, e => !string.IsNullOrEmpty(e.OriginalText));
        db.Dispose();
    }

    // ──────────────────────────────────────────────
    //  ViewModel 层
    // ──────────────────────────────────────────────

    [Fact]
    public void SelectAllChapters_WorksCorrectly()
    {
        var vm = new MainWindowViewModel();
        vm.Chapters.Add(new ChapterSelectionViewModel("ch1", false));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch2", true));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch3", false));

        vm.SelectAllChaptersCommand.Execute(null);
        Assert.All(vm.Chapters, c => Assert.True(c.IsSelected));
    }

    [Fact]
    public void ChapterSelection_DefaultsToSelected()
    {
        var vm = new ChapterSelectionViewModel("test_chapter");
        Assert.True(vm.IsSelected);
    }
}
