using System;
using System.IO;
using ArkPlotWpf.Data;
using Xunit;

namespace ArkPlotWpf.DbTests;

/// <summary>
/// DatabaseService 测试类
/// </summary>
public class DatabaseServiceTests : IDisposable
{
    private readonly string _testDbPath = "test_arkplot.db";

    public DatabaseServiceTests()
    {
        // 清理可能存在的测试数据库文件
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public void Initialize_ShouldCreateDatabase()
    {
        // Act
        DatabaseService.Instance.Initialize();

        // Assert
        Assert.True(File.Exists("arkplot.db"));
        Assert.True(DatabaseService.Instance.CheckConnection());
    }

    [Fact]
    public void GetStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        DatabaseService.Instance.Initialize();

        // Act
        var stats = DatabaseService.Instance.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.LastUpdated > DateTime.MinValue);
        Assert.True(stats.TotalRecords >= 0);
    }

    [Fact]
    public void CheckConnection_ShouldReturnTrue_WhenDatabaseExists()
    {
        // Arrange
        DatabaseService.Instance.Initialize();

        // Act
        var isConnected = DatabaseService.Instance.CheckConnection();

        // Assert
        Assert.True(isConnected);
    }

    [Fact]
    public void GetDatabaseSize_ShouldReturnValidSize()
    {
        // Arrange
        DatabaseService.Instance.Initialize();

        // Act
        var size = DatabaseService.Instance.GetDatabaseSize();

        // Assert
        Assert.True(size > 0);
    }

    public void Dispose()
    {
        // 清理测试数据库文件
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }
} 