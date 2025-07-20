using ArkPlotWpf.Data.Repositories;
using ArkPlotWpf.Model;
using System.Text.Json;
using Xunit;

namespace ArkPlotWpf.DbTests.Database.Integration;

public class DatabaseIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PrtsDataRepository _prtsDataRepository;
    private readonly PrtsAssetsRepository _prtsAssetsRepository;

    public DatabaseIntegrationTests()
    {
        _testDbPath = Path.GetTempFileName();
        _prtsDataRepository = new PrtsDataRepository($"Data Source={_testDbPath}");
        _prtsAssetsRepository = new PrtsAssetsRepository($"Data Source={_testDbPath}");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // 忽略删除失败的情况
        }
    }

    [Fact]
    public void FullCrudWorkflow_ShouldWorkCorrectly()
    {
        // Arrange
        var prtsData = new PrtsData("IntegrationTest");
        prtsData.Data["testKey"] = "testValue";
        prtsData.Data["nested"] = "{\"level1\":\"value1\",\"level2\":\"value2\"}";

        // Act - Create
        var id = _prtsDataRepository.AddPrtsData(prtsData);
        Assert.True(id > 0);

        // Act - Read
        var retrieved = _prtsDataRepository.GetPrtsDataByTag("IntegrationTest");
        Assert.NotNull(retrieved);
        Assert.Equal("testValue", retrieved.Data["testKey"]);

        // Act - Update
        retrieved.Data["updatedKey"] = "updatedValue";
        var updateResult = _prtsDataRepository.UpdatePrtsData(retrieved);
        Assert.True(updateResult);

        // Act - Read Updated
        var updated = _prtsDataRepository.GetPrtsDataByTag("IntegrationTest");
        Assert.NotNull(updated);
        Assert.Equal("updatedValue", updated.Data["updatedKey"]);

        // Act - Delete
        var deleteResult = _prtsDataRepository.DeletePrtsData("IntegrationTest");
        Assert.True(deleteResult);

        // Act - Verify Deletion
        var deleted = _prtsDataRepository.GetPrtsDataByTag("IntegrationTest");
        Assert.Null(deleted);
    }

    [Fact]
    public void DataMapping_ShouldBeCorrect()
    {
        // Arrange
        var originalData = new PrtsData("MappingTest");
        originalData.Data["stringValue"] = "test";
        originalData.Data["numberValue"] = "42";
        originalData.Data["boolValue"] = "true";
        originalData.Data["arrayValue"] = "[1,2,3]";
        originalData.Data["objectValue"] = "{\"name\":\"test\",\"value\":123}";

        // Act
        var id = _prtsDataRepository.AddPrtsData(originalData);
        var retrieved = _prtsDataRepository.GetPrtsDataByTag("MappingTest");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test", retrieved.Data["stringValue"]);
        Assert.Equal("42", retrieved.Data["numberValue"]);
        Assert.Equal("true", retrieved.Data["boolValue"]);
        Assert.Equal("[1,2,3]", retrieved.Data["arrayValue"]);
    }

    [Fact]
    public void TransactionHandling_ShouldWorkCorrectly()
    {
        // Arrange
        var data1 = new PrtsData("TransactionTest1");
        data1.Data["key"] = "value1";
        var data2 = new PrtsData("TransactionTest2");
        data2.Data["key"] = "value2";

        // Act - Add both records
        var id1 = _prtsDataRepository.AddPrtsData(data1);
        var id2 = _prtsDataRepository.AddPrtsData(data2);

        // Assert - Both should be added successfully
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);

        var retrieved1 = _prtsDataRepository.GetPrtsDataByTag("TransactionTest1");
        var retrieved2 = _prtsDataRepository.GetPrtsDataByTag("TransactionTest2");

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.Equal("value1", retrieved1.Data["key"]);
        Assert.Equal("value2", retrieved2.Data["key"]);
    }

    [Fact]
    public async Task Concurrency_ShouldHandleMultipleOperations()
    {
        // Arrange
        var tasks = new List<Task<long>>();

        // Act - Create multiple tasks to add data concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var data = new PrtsData($"ConcurrencyTest{index}");
                data.Data["index"] = index.ToString();
                return _prtsDataRepository.AddPrtsData(data);
            }));
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Assert - All tasks should complete successfully
        foreach (var task in tasks)
        {
            Assert.True(task.Result > 0);
        }

        // Verify all data was saved
        var allData = _prtsDataRepository.GetAllPrtsData();
        var concurrencyData = allData.Where(d => d.Tag.StartsWith("ConcurrencyTest")).ToList();
        Assert.Equal(10, concurrencyData.Count);
    }

    [Fact]
    public void LargeDataSet_ShouldHandleCorrectly()
    {
        // Arrange
        var largeData = new PrtsData("LargeDataSet");
        for (int i = 0; i < 100; i++)
        {
            largeData.Data[$"key{i}"] = $"value{i}";
        }

        // Act
        var id = _prtsDataRepository.AddPrtsData(largeData);
        var retrieved = _prtsDataRepository.GetPrtsDataByTag("LargeDataSet");

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(retrieved);
        Assert.Equal(100, retrieved.Data.Count);
        Assert.Equal("value50", retrieved.Data["key50"]);
    }

    [Fact]
    public void SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var specialData = new PrtsData("SpecialChars");
        specialData.Data["unicode"] = "测试中文";
        specialData.Data["symbols"] = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
        specialData.Data["newlines"] = "line1\nline2\r\nline3";

        // Act
        var id = _prtsDataRepository.AddPrtsData(specialData);
        var retrieved = _prtsDataRepository.GetPrtsDataByTag("SpecialChars");

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(retrieved);
        Assert.Equal("测试中文", retrieved.Data["unicode"]);
        Assert.Equal("!@#$%^&*()_+-=[]{}|;':\",./<>?", retrieved.Data["symbols"]);
        Assert.Equal("line1\nline2\r\nline3", retrieved.Data["newlines"]);
    }

    [Fact]
    public void DataIntegrity_ShouldBeMaintained()
    {
        // Arrange
        var originalData = new PrtsData("IntegrityTest");
        originalData.Data["original"] = "originalValue";

        // Act
        var id = _prtsDataRepository.AddPrtsData(originalData);
        var retrieved = _prtsDataRepository.GetPrtsDataByTag("IntegrityTest");

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(retrieved);
        Assert.Equal("IntegrityTest", retrieved.Tag);
        Assert.Equal("originalValue", retrieved.Data["original"]);

        // Verify data hasn't been corrupted
        var allData = _prtsDataRepository.GetAllPrtsData();
        var integrityData = allData.FirstOrDefault(d => d.Tag == "IntegrityTest");
        Assert.NotNull(integrityData);
        Assert.Equal("originalValue", integrityData.Data["original"]);
    }

    [Fact]
    public void ErrorHandling_ShouldWorkCorrectly()
    {
        // Act & Assert - Try to get non-existing data
        var nonExisting = _prtsDataRepository.GetPrtsDataByTag("NonExistingTag");
        Assert.Null(nonExisting);

        // Act & Assert - Try to update non-existing data
        var nonExistingData = new PrtsData("NonExistingTag");
        nonExistingData.Data["key"] = "value";
        var updateResult = _prtsDataRepository.UpdatePrtsData(nonExistingData);
        Assert.False(updateResult);

        // Act & Assert - Try to delete non-existing data
        var deleteResult = _prtsDataRepository.DeletePrtsData("NonExistingTag");
        Assert.False(deleteResult);
    }

    [Fact]
    public void RepositoryLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        var data = new PrtsData("LifecycleTest");
        data.Data["key"] = "value";

        // Act - Add data
        var id = _prtsDataRepository.AddPrtsData(data);
        Assert.True(id > 0);

        // Act - Verify data exists
        var exists = _prtsDataRepository.Exists("LifecycleTest");
        Assert.True(exists);

        // Act - Get by ID
        var byId = _prtsDataRepository.GetById(id);
        Assert.NotNull(byId);
        Assert.Equal("LifecycleTest", byId.Tag);

        // Act - Delete by ID
        var deleteById = _prtsDataRepository.Delete(id);
        Assert.True(deleteById);

        // Act - Verify deletion
        var deletedById = _prtsDataRepository.GetById(id);
        Assert.Null(deletedById);
    }
} 