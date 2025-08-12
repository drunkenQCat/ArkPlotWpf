using System;
using System.Collections.Generic;
using System.Text;
using ArkPlotWpf.Data.Repositories;
using ArkPlotWpf.Model;
using SqlSugar;
using Xunit;

namespace ArkPlotWpf.DbTests;

/// <summary>
/// 仓储测试类
/// </summary>
public class RepositoryTests : IDisposable
{
    private readonly SqlSugarClient _testDb;
    private readonly PlotRepository _plotRepo;
    private readonly FormattedTextEntryRepository _textEntryRepo;
    private readonly PrtsDataRepository _prtsDataRepo;

    public RepositoryTests()
    {
        // 使用内存数据库进行测试
        _testDb = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        // 初始化测试表
        _testDb.CodeFirst.InitTables(
            typeof(Plot),
            typeof(FormattedTextEntry),
            typeof(PrtsData)
        );

        // 创建仓储实例
        _plotRepo = new PlotRepository(_testDb);
        _textEntryRepo = new FormattedTextEntryRepository(_testDb);
        _prtsDataRepo = new PrtsDataRepository(_testDb);
    }

    [Fact]
    public void PlotRepository_ShouldAddAndRetrievePlot()
    {
        // Arrange
        var plot = new Plot("测试标题", new StringBuilder("测试内容"));

        // Act
        var result = _plotRepo.Add(plot);
        var retrieved = _plotRepo.GetById(plot.Id);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(retrieved);
        Assert.Equal("测试标题", retrieved.Title);
        Assert.Equal("测试内容", retrieved.Content.ToString());
    }

    [Fact]
    public void FormattedTextEntryRepository_ShouldAddAndRetrieveEntry()
    {
        // Arrange
        var entry = new FormattedTextEntry
        {
            Index = 1,
            Type = "对话",
            CharacterName = "测试角色",
            OriginalText = "原始文本",
            Dialog = "对话内容"
        };

        // Act
        var result = _textEntryRepo.Add(entry);
        var retrieved = _textEntryRepo.GetById(result);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(retrieved);
        Assert.Equal("对话", retrieved.Type);
        Assert.Equal("测试角色", retrieved.CharacterName);
    }

    [Fact]
    public void PrtsDataRepository_ShouldAddAndRetrieveData()
    {
        // Arrange
        var prtsData = new PrtsData("测试标签");
        prtsData.Data["测试键"] = "测试值";

        // Act
        var result = _prtsDataRepo.Add(prtsData);
        var retrieved = _prtsDataRepo.GetById(prtsData.Id);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(retrieved);
        Assert.Equal("测试标签", retrieved.Tag);
        Assert.Equal("测试值", retrieved.Data["测试键"]);
    }

    [Fact]
    public void PlotRepository_ShouldUpdatePlot()
    {
        // Arrange
        var plot = new Plot("原始标题", new StringBuilder("原始内容"));
        _plotRepo.Add(plot);

        // Act
        var result = _plotRepo.UpdateTitle(plot.Id, "更新标题");
        var retrieved = _plotRepo.GetById(plot.Id);

        // Assert
        Assert.True(result);
        Assert.Equal("更新标题", retrieved.Title);
    }

    [Fact]
    public void FormattedTextEntryRepository_ShouldUpdateEntry()
    {
        // Arrange
        var entry = new FormattedTextEntry
        {
            Index = 1,
            Type = "对话",
            CharacterName = "原始角色",
            OriginalText = "原始文本"
        };
        _textEntryRepo.Add(entry);

        // Act
        entry.CharacterName = "更新角色";
        var result = _textEntryRepo.Update(entry);
        var retrieved = _textEntryRepo.GetById(entry.Id);

        // Assert
        Assert.True(result);
        Assert.Equal("更新角色", retrieved.CharacterName);
    }

    [Fact]
    public void PrtsDataRepository_ShouldUpdateData()
    {
        // Arrange
        var prtsData = new PrtsData("原始标签");
        prtsData.Data["原始键"] = "原始值";
        _prtsDataRepo.Add(prtsData);

        // Act
        prtsData.Data["原始键"] = "更新值";
        var result = _prtsDataRepo.Update(prtsData);
    }

    [Fact]
    public void PlotRepository_ShouldDeletePlot()
    {
        // Arrange
        var plot = new Plot("测试标题", new StringBuilder("测试内容"));
        _plotRepo.Add(plot);

        // Act
        var result = _plotRepo.Delete(x => x.Id == plot.Id);
        var retrieved = _plotRepo.GetById(plot.Id);

        // Assert
        Assert.True(result);
        Assert.Null(retrieved);
    }

    [Fact]
    public void FormattedTextEntryRepository_ShouldDeleteEntry()
    {
        // Arrange
        var entry = new FormattedTextEntry
        {
            Index = 1,
            Type = "对话",
            CharacterName = "测试角色",
            OriginalText = "原始文本"
        };
        var resultId = _textEntryRepo.Add(entry);

        // Act
        var result = _textEntryRepo.Delete(x => x.Id == resultId);
        var retrieved = _textEntryRepo.GetById(entry.Id);

        // Assert
        Assert.True(result);
        Assert.Null(retrieved);
    }

    [Fact]
    public void PrtsDataRepository_ShouldDeleteData()
    {
        // Arrange
        var prtsData = new PrtsData("测试标签");
        prtsData.Data["测试键"] = "测试值";
        _prtsDataRepo.Add(prtsData);

        // Act
        var result = _prtsDataRepo.Delete(x => x.Id == prtsData.Id);
        var retrieved = _prtsDataRepo.GetById(prtsData.Id);

        // Assert
        Assert.True(result);
        Assert.Null(retrieved);
    }

    [Fact]
    public void PlotRepository_ShouldGetByTitle()
    {
        // Arrange
        var plots = new List<Plot>
        {
            new("第一章 开始", new StringBuilder("内容1")),
            new("第二章 发展", new StringBuilder("内容2")),
            new("其他章节", new StringBuilder("内容3"))
        };
        _plotRepo.AddRange(plots);

        // Act
        var result = _plotRepo.GetByTitle("第");

        // Assert
        Assert.Equal(2, result.Count);
        foreach (var p in result)
        {
            Assert.Contains("章", p.Title);
        }
    }

    [Fact]
    public void FormattedTextEntryRepository_ShouldGetByType()
    {
        // Arrange
        var entries = new List<FormattedTextEntry>
        {
            new() { Type = "对话", CharacterName = "角色1", OriginalText = "文本1" },
            new() { Type = "旁白", CharacterName = "角色2", OriginalText = "文本2" },
            new() { Type = "对话", CharacterName = "角色3", OriginalText = "文本3" }
        };
        _textEntryRepo.AddRange(entries);

        // Act
        var result = _textEntryRepo.GetByType("对话");

        // Assert
        Assert.Equal(2, result.Count);
        foreach (var e in result)
        {
            Assert.Equal("对话", e.Type);
        }
    }

    [Fact]
    public void PrtsDataRepository_ShouldGetByTag()
    {
        // Arrange
        var data1 = new PrtsData("标签1");
        data1.Data["键1"] = "值1";
        var data2 = new PrtsData("标签2");
        data2.Data["键2"] = "值2";
        var data3 = new PrtsData("标签1");
        data3.Data["键3"] = "值3";

        _prtsDataRepo.AddRange(new List<PrtsData> { data1, data2, data3 });

        // Act
        var result = _prtsDataRepo.GetByTagLike("标签1");

        // Assert
        Assert.Equal(2, result.Count);
        foreach (var d in result)
        {
            Assert.Equal("标签1", d.Tag);
        }
    }

    public void Dispose()
    {
        _testDb?.Dispose();
    }
} 