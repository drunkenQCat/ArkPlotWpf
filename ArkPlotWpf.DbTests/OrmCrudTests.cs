using System.Text;
using ArkPlot.Core.Model;
using SqlSugar;
using Xunit;

namespace ArkPlotWpf.DbTests;

/// <summary>
/// 直接通过 SqlSugarClient 测试 ORM CRUD 操作
/// </summary>
public class OrmCrudTests : IDisposable
{
    private readonly SqlSugarClient _db;

    public OrmCrudTests()
    {
        _db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        _db.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(Plot),
            typeof(FormattedTextEntry),
            typeof(PrtsData),
            typeof(Act),
            typeof(PicDescription)
        );
    }

    [Fact]
    public void Should_InsertAndQueryPlot()
    {
        var plot = new Plot("测试标题", new StringBuilder("测试内容"))
        {
            ActId = 42,
            Status = 2
        };
        var id = _db.Insertable(plot).ExecuteReturnIdentity();

        var retrieved = _db.Queryable<Plot>().First(it => it.Id == id);

        Assert.NotNull(retrieved);
        Assert.Equal("测试标题", retrieved.Title);
        Assert.Equal(42, retrieved.ActId);
        Assert.Equal(2, retrieved.Status);
    }

    [Fact]
    public void Should_InsertAndQueryFormattedTextEntry()
    {
        var entry = new FormattedTextEntry
        {
            Index = 1,
            Type = "对话",
            CharacterName = "测试角色",
            OriginalText = "原始文本",
            Dialog = "对话内容"
        };

        var id = _db.Insertable(entry).ExecuteReturnIdentity();
        var retrieved = _db.Queryable<FormattedTextEntry>().First(it => it.Id == id);

        Assert.NotNull(retrieved);
        Assert.Equal("对话", retrieved.Type);
        Assert.Equal("测试角色", retrieved.CharacterName);
    }

    [Fact]
    public void Should_InsertAndQueryPrtsData()
    {
        var prtsData = new PrtsData("测试标签");
        prtsData.Data["测试键"] = "测试值";

        var id = _db.Insertable(prtsData).ExecuteReturnIdentity();
        var retrieved = _db.Queryable<PrtsData>().First(it => it.Id == id);

        Assert.NotNull(retrieved);
        Assert.Equal("测试标签", retrieved.Tag);
        Assert.Equal("测试值", retrieved.Data["测试键"]);
    }

    [Fact]
    public void Should_UpdatePlot()
    {
        var plot = new Plot("原始标题", new StringBuilder("原始内容"));
        var id = _db.Insertable(plot).ExecuteReturnIdentity();

        _db.Updateable<Plot>()
            .SetColumns(it => it.Title == "更新标题")
            .Where(it => it.Id == id)
            .ExecuteCommand();

        var retrieved = _db.Queryable<Plot>().First(it => it.Id == id);
        Assert.Equal("更新标题", retrieved!.Title);
    }

    [Fact]
    public void Should_DeletePlot()
    {
        var plot = new Plot("要删除的", new StringBuilder("内容"));
        var id = _db.Insertable(plot).ExecuteReturnIdentity();

        _db.Deleteable<Plot>().Where(it => it.Id == id).ExecuteCommand();
        var retrieved = _db.Queryable<Plot>().First(it => it.Id == id);

        Assert.Null(retrieved);
    }

    [Fact]
    public void Should_QueryByCondition()
    {
        _db.Insertable(new Plot("第一章 开始", new StringBuilder("内容1"))).ExecuteReturnIdentity();
        _db.Insertable(new Plot("第二章 发展", new StringBuilder("内容2"))).ExecuteReturnIdentity();
        _db.Insertable(new Plot("其他剧情", new StringBuilder("内容3"))).ExecuteReturnIdentity();

        var result = _db.Queryable<Plot>()
            .Where(it => it.Title.Contains("章"))
            .ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Should_InsertAndQueryAct()
    {
        var act = new Act { Name = "测试幕", ActId = "test", Lang = "zh_CN", ActType = "ACTIVITY_STORY" };
        var id = _db.Insertable(act).ExecuteReturnIdentity();

        var retrieved = _db.Queryable<Act>().First(it => it.Id == id);
        Assert.NotNull(retrieved);
        Assert.Equal("测试幕", retrieved.Name);
    }

    [Fact]
    public void Should_InsertAndQueryPicDescription()
    {
        var pic = new PicDescription
        {
            ImageUrl = "https://example.com/test.png",
            PicDesc = "一张测试图片",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var id = _db.Insertable(pic).ExecuteReturnIdentity();

        var retrieved = _db.Queryable<PicDescription>().First(it => it.Id == id);
        Assert.NotNull(retrieved);
        Assert.Equal("https://example.com/test.png", retrieved.ImageUrl);
        Assert.Equal("一张测试图片", retrieved.PicDesc);
    }

    [Fact]
    public void Should_ExecuteTransaction()
    {
        var result = _db.Ado.UseTran(() =>
        {
            _db.Insertable(new Plot("事务标题", new StringBuilder("内容"))).ExecuteReturnIdentity();
            _db.Insertable(new FormattedTextEntry
            {
                Type = "对话",
                CharacterName = "角色",
                OriginalText = "文本",
                Index = 1
            }).ExecuteReturnIdentity();
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _db.Queryable<Plot>().Count());
        Assert.Equal(1, _db.Queryable<FormattedTextEntry>().Count());
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
