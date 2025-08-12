using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArkPlotWpf.Data.Repositories;
using ArkPlotWpf.Model;
using SqlSugar;
using Xunit;

namespace ArkPlotWpf.DbTests;

/// <summary>
/// 仓储工厂测试类
/// </summary>
public class RepositoryFactoryTests : IDisposable
{
    private readonly SqlSugarClient _testDb;
    private readonly PlotRepository _plotRepo;
    private readonly FormattedTextEntryRepository _textEntryRepo;
    private readonly PrtsDataRepository _prtsDataRepo;

    public RepositoryFactoryTests()
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

        _testDb.Aop.OnError = ex =>
        {
            Console.WriteLine("SQL Error: " + ex.Message);
        };

        // 创建本地仓储实例
        _plotRepo = new PlotRepository(_testDb);
        _textEntryRepo = new FormattedTextEntryRepository(_testDb);
        _prtsDataRepo = new PrtsDataRepository(_testDb);
    }
    

    [Fact]
    public void PlotRepository_ShouldWorkCorrectly()
    {
        // Arrange
        var plot = new Plot { Title = "测试标题", Content = new System.Text.StringBuilder("测试内容") };

        // Act
        var result = _plotRepo.Add(plot);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void FormattedTextEntryRepository_ShouldWorkCorrectly()
    {
        // Arrange
        var textEntry = new FormattedTextEntry 
        { 
            Type = "对话", 
            CharacterName = "测试角色", 
            OriginalText = "原始文本", 
            Dialog = "对话内容", 
            Index = 1 
        };

        // Act
        var returnedId = _textEntryRepo.Add(textEntry);

        // Assert
        Assert.Equal(1, returnedId);
    }

    [Fact]
    public void PrtsDataRepository_ShouldWorkCorrectly()
    {
        // Arrange
        var prtsData = new PrtsData("测试标签");
        prtsData.Data["测试键"] = "测试值";

        // Act
        var result = _prtsDataRepo.Add(prtsData);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void UseTransaction_ShouldExecuteTransactionSuccessfully()
    {
        // Arrange
        var plot = new Plot { Title = "测试标题", Content = new System.Text.StringBuilder("测试内容") };
        var textEntry = new FormattedTextEntry 
        { 
            Type = "对话", 
            CharacterName = "测试角色", 
            OriginalText = "原始文本", 
            Dialog = "对话内容", 
            Index = 1 
        };

        // Act
        var result = _testDb.Ado.UseTran(() =>
        {
            _plotRepo.Add(plot);
            _textEntryRepo.Add(textEntry);
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, _plotRepo.Count());
        Assert.Equal(1, _textEntryRepo.Count());
    }

    [Fact]
    public async Task UseTransactionAsync_ShouldExecuteTransactionSuccessfully()
    {
        // Arrange
        var plot = new Plot { Title = "测试标题", Content = new System.Text.StringBuilder("测试内容") };
        var textEntry = new FormattedTextEntry 
        { 
            Type = "对话", 
            CharacterName = "测试角色", 
            OriginalText = "原始文本", 
            Dialog = "对话内容", 
            Index = 1 
        };

        // Act
        var result = await _testDb.Ado.UseTranAsync(async () =>
        {
            await _plotRepo.AddAsync(plot);
            await _textEntryRepo.AddAsync(textEntry);
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, await _plotRepo.CountAsync());
        Assert.Equal(1, await _textEntryRepo.CountAsync());
    }

    [Fact]
    public void RepositoryInstances_ShouldBeIndependent()
    {
        // Arrange
        var plot = new Plot { Title = "测试标题", Content = new System.Text.StringBuilder("测试内容") };
        var textEntry = new FormattedTextEntry 
        { 
            Type = "对话", 
            CharacterName = "测试角色", 
            OriginalText = "原始文本", 
            Dialog = "对话内容", 
            Index = 1 
        };

        // Act
        _plotRepo.Add(plot);
        _textEntryRepo.Add(textEntry);

        // Assert
        Assert.Equal(1, _plotRepo.Count());
        Assert.Equal(1, _textEntryRepo.Count());
        Assert.Equal(0, _prtsDataRepo.Count());
    }

    [Fact]
    public void RepositoryInstances_ShouldUseSameDatabase()
    {
        // Arrange
        var plot = new Plot { Title = "测试标题", Content = new System.Text.StringBuilder("测试内容") };

        // Act
        var id = _plotRepo.Add(plot);
        var retrievedPlot = _plotRepo.GetById(id);

        // Assert
        Assert.NotNull(retrievedPlot);
        Assert.Equal(plot.Title, retrievedPlot.Title);
    }

    public void Dispose()
    {
        _testDb?.Dispose();
    }
} 