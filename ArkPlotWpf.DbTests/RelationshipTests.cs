using System.Text;
using ArkPlot.Core.Model;
using SqlSugar;
using Xunit;

namespace ArkPlotWpf.DbTests;

/// <summary>
/// 演示 Plot → Act / FormattedTextEntry → Plot 关联关系。
///
/// ⛔ 当前不会编译 —— 因为模型上还没有 ActId / PlotId 字段。
///    这些测试定义了"改完后应该长什么样"，编译通过之日就是改动完成之时。
///
/// 需要的模型变更：
///   1. Plot.cs          → 加 [SugarColumn]  public long ActId { get; set; }
///   2. FormattedTextEntry.cs → 加 [SugarColumn]  public long PlotId { get; set; }
/// </summary>
public class RelationshipTests : IDisposable
{
    private readonly SqlSugarClient _db;

    public RelationshipTests()
    {
        _db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        _db.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(Act),
            typeof(Plot),
            typeof(FormattedTextEntry)
        );
    }

    // ──────────────────────────────────────────────
    //  1. Plot 关联到 Act
    // ──────────────────────────────────────────────

    [Fact]
    public void Plot_Should_Reference_Act()
    {
        // Arrange
        var act = new Act { Name = "骑兵与猎人", ActId = "1stact", Lang = "zh_CN", ActType = "ACTIVITY_STORY" };
        var actId = _db.Insertable(act).ExecuteReturnIdentity();

        var plot = new Plot("EP01", new StringBuilder("第一段剧情..."))
        {
            ActId = actId    // ← 需要 Plot.ActId 字段
        };

        // Act
        _db.Insertable(plot).ExecuteCommand();

        // Assert — 通过 ActId 查回 Plot
        var plotsUnderAct = _db.Queryable<Plot>()
            .Where(p => p.ActId == actId)   // ← 需要 Plot.ActId 字段
            .ToList();

        Assert.Single(plotsUnderAct);
        Assert.Equal("EP01", plotsUnderAct[0].Title);
    }

    [Fact]
    public void Should_Query_Plots_By_ActTitle()
    {
        // Arrange
        var act = new Act { Name = "骑兵与猎人", ActId = "1stact", Lang = "zh_CN", ActType = "ACTIVITY_STORY" };
        var actId = _db.Insertable(act).ExecuteReturnIdentity();

        _db.Insertable(new Plot("EP01", new StringBuilder("内容1"))
            { ActId = actId }).ExecuteCommand();
        _db.Insertable(new Plot("EP02", new StringBuilder("内容2"))
            { ActId = actId }).ExecuteCommand();

        // Act — 三表关联查询：FormattedTextEntry → Plot → Act
        var plots = _db.Queryable<Plot>()
            .LeftJoin<Act>((p, a) => p.ActId == a.Id)   // ← 需要 Plot.ActId
            .Where((p, a) => a.Name == "骑兵与猎人")
            .Select((p, a) => p)
            .ToList();

        // Assert
        Assert.Equal(2, plots.Count);
    }

    // ──────────────────────────────────────────────
    //  2. FormattedTextEntry 关联到 Plot
    // ──────────────────────────────────────────────

    [Fact]
    public void FormattedTextEntry_Should_Reference_Plot()
    {
        // Arrange
        var act = new Act { Name = "活动A", ActId = "actA", Lang = "zh_CN", ActType = "ACTIVITY_STORY" };
        var actId = _db.Insertable(act).ExecuteReturnIdentity();

        var plot = new Plot("EP01", new StringBuilder())
        {
            ActId = actId
        };
        var plotId = _db.Insertable(plot).ExecuteReturnIdentity();

        var entry = new FormattedTextEntry
        {
            PlotId = plotId,   // ← 需要 FormattedTextEntry.PlotId 字段
            Index = 1,
            Type = "对话",
            CharacterName = "阿米娅",
            OriginalText = "博士，起床了。"
        };

        // Act
        _db.Insertable(entry).ExecuteCommand();

        // Assert — 通过 PlotId 查回 Entry
        var entries = _db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plotId)   // ← 需要 FormattedTextEntry.PlotId
            .ToList();

        Assert.Single(entries);
        Assert.Equal("阿米娅", entries[0].CharacterName);
    }

    [Fact]
    public void Should_Cascade_Query_From_Act_To_Entries()
    {
        // Arrange
        var act = new Act { Name = "活动B", ActId = "actB", Lang = "zh_CN", ActType = "ACTIVITY_STORY" };
        var actId = _db.Insertable(act).ExecuteReturnIdentity();

        var plot = new Plot("EP01", new StringBuilder("内容"))
            { ActId = actId };
        var plotId = _db.Insertable(plot).ExecuteReturnIdentity();

        _db.Insertable(new FormattedTextEntry
            { PlotId = plotId, Index = 1, OriginalText = "行1", Type = "叙述" }).ExecuteCommand();
        _db.Insertable(new FormattedTextEntry
            { PlotId = plotId, Index = 2, OriginalText = "行2", Type = "对话" }).ExecuteCommand();

        // Act — 三表联查：Act → Plot → FormattedTextEntry
        var entries = _db.Queryable<Act>()
            .LeftJoin<Plot>((a, p) => p.ActId == a.Id)            // ← 需要 Plot.ActId
            .LeftJoin<FormattedTextEntry>((a, p, e) => e.PlotId == p.Id)  // ← 需要 Entry.PlotId
            .Where((a, p, e) => a.Name == "活动B")
            .Select((a, p, e) => e)
            .ToList();

        // Assert
        Assert.Equal(2, entries.Count);
    }

    // ──────────────────────────────────────────────
    //  3. 模型验证 — 关联字段的 null 安全性
    // ──────────────────────────────────────────────

    [Fact]
    public void Plot_ActId_Should_Be_Zero_When_Not_Set()
    {
        // 不设 ActId 时默认为 0，SqlSugar 存为 NULL（Sqlite 允许）
        var plot = new Plot("独立剧情", new StringBuilder("不关联活动的剧情"));

        var id = _db.Insertable(plot).ExecuteReturnIdentity();
        var retrieved = _db.Queryable<Plot>().First(p => p.Id == id);

        Assert.NotNull(retrieved);
        // ActId 为 0 表示"未关联活动"——这是合法的
        Assert.Equal(0, retrieved!.ActId);
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
