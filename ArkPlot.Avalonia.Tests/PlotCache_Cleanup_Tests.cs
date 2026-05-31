using System.Text;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using SqlSugar;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// PlotCache.CleanupEmptyPlotsAsync 测试：验证第三道防线（启动时清理脏缓存）。
/// 每个测试使用独立内存 DB，可并行运行。
/// </summary>
public class PlotCache_Cleanup_Tests
{
    private static SqlSugarClient CreateMemoryDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false,
        });
        db.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(Act), typeof(Plot), typeof(FormattedTextEntry));
        return db;
    }

    [Fact]
    public async Task CleanupEmptyPlots_RemovesEmptyEntries()
    {
        var db = CreateMemoryDb();
        var actId = db.Insertable(new Act { ActId = "cleanup1", Name = "测试", Lang = "zh_CN", ActType = "ACTIVITY_STORY" }).ExecuteReturnIdentity();

        var plotId = db.Insertable(new Plot("空章节", new StringBuilder()) { ActId = actId, Status = 2 }).ExecuteReturnIdentity();
        db.Insertable(new List<FormattedTextEntry>
        {
            new() { PlotId = plotId, Index = 0, OriginalText = "" },
            new() { PlotId = plotId, Index = 1, OriginalText = "   " },
        }).ExecuteCommand();

        var count = await PlotCache.CleanupEmptyPlotsAsync(actId, db);

        Assert.Equal(1, count);
        Assert.Null(db.Queryable<Plot>().First(p => p.Id == plotId));
        Assert.Empty(db.Queryable<FormattedTextEntry>().Where(e => e.PlotId == plotId).ToList());

        db.Dispose();
    }

    [Fact]
    public async Task CleanupEmptyPlots_KeepsNonEmptyEntries()
    {
        var db = CreateMemoryDb();
        var actId = db.Insertable(new Act { ActId = "cleanup2", Name = "测试", Lang = "zh_CN", ActType = "ACTIVITY_STORY" }).ExecuteReturnIdentity();

        var plotId = db.Insertable(new Plot("有效章节", new StringBuilder()) { ActId = actId, Status = 2 }).ExecuteReturnIdentity();
        db.Insertable(new List<FormattedTextEntry>
        {
            new() { PlotId = plotId, Index = 0, OriginalText = "[Dialog]你好" },
            new() { PlotId = plotId, Index = 1, OriginalText = "有效内容" },
        }).ExecuteCommand();

        var count = await PlotCache.CleanupEmptyPlotsAsync(actId, db);

        Assert.Equal(0, count);
        Assert.NotNull(db.Queryable<Plot>().First(p => p.Id == plotId));
        Assert.Equal(2, db.Queryable<FormattedTextEntry>().Where(e => e.PlotId == plotId).Count());

        db.Dispose();
    }

    [Fact]
    public async Task CleanupEmptyPlots_MixedContent_OnlyRemovesEmpty()
    {
        var db = CreateMemoryDb();
        var actId = db.Insertable(new Act { ActId = "cleanup3", Name = "测试", Lang = "zh_CN", ActType = "ACTIVITY_STORY" }).ExecuteReturnIdentity();

        var emptyPlotId = db.Insertable(new Plot("空章节", new StringBuilder()) { ActId = actId, Status = 2 }).ExecuteReturnIdentity();
        db.Insertable(new FormattedTextEntry { PlotId = emptyPlotId, Index = 0, OriginalText = "" }).ExecuteCommand();

        var validPlotId = db.Insertable(new Plot("有效章节", new StringBuilder()) { ActId = actId, Status = 2 }).ExecuteReturnIdentity();
        db.Insertable(new FormattedTextEntry { PlotId = validPlotId, Index = 0, OriginalText = "[Dialog]有效" }).ExecuteCommand();

        var count = await PlotCache.CleanupEmptyPlotsAsync(actId, db);

        Assert.Equal(1, count);
        Assert.Null(db.Queryable<Plot>().First(p => p.Id == emptyPlotId));
        Assert.NotNull(db.Queryable<Plot>().First(p => p.Id == validPlotId));

        db.Dispose();
    }

    [Fact]
    public async Task CleanupEmptyPlots_NoFormattedTextEntries_RemovesPlot()
    {
        var db = CreateMemoryDb();
        var actId = db.Insertable(new Act { ActId = "cleanup4", Name = "测试", Lang = "zh_CN", ActType = "ACTIVITY_STORY" }).ExecuteReturnIdentity();

        var plotId = db.Insertable(new Plot("零条目章节", new StringBuilder()) { ActId = actId, Status = 2 }).ExecuteReturnIdentity();

        var count = await PlotCache.CleanupEmptyPlotsAsync(actId, db);

        Assert.Equal(1, count);
        Assert.Null(db.Queryable<Plot>().First(p => p.Id == plotId));

        db.Dispose();
    }

    [Fact]
    public async Task CleanupEmptyPlots_DoesNotAffectOtherActIds()
    {
        var db = CreateMemoryDb();
        var actId1 = db.Insertable(new Act { ActId = "cleanup5a", Name = "活动A", Lang = "zh_CN", ActType = "ACTIVITY_STORY" }).ExecuteReturnIdentity();
        var actId2 = db.Insertable(new Act { ActId = "cleanup5b", Name = "活动B", Lang = "zh_CN", ActType = "ACTIVITY_STORY" }).ExecuteReturnIdentity();

        var plotId1 = db.Insertable(new Plot("活动A空章节", new StringBuilder()) { ActId = actId1, Status = 2 }).ExecuteReturnIdentity();
        db.Insertable(new FormattedTextEntry { PlotId = plotId1, Index = 0, OriginalText = "" }).ExecuteCommand();

        var plotId2 = db.Insertable(new Plot("活动B空章节", new StringBuilder()) { ActId = actId2, Status = 2 }).ExecuteReturnIdentity();
        db.Insertable(new FormattedTextEntry { PlotId = plotId2, Index = 0, OriginalText = "" }).ExecuteCommand();

        var count = await PlotCache.CleanupEmptyPlotsAsync(actId1, db);

        Assert.Equal(1, count);
        Assert.Null(db.Queryable<Plot>().First(p => p.Id == plotId1));
        Assert.NotNull(db.Queryable<Plot>().First(p => p.Id == plotId2));

        db.Dispose();
    }
}
