using ArkPlotWpf.Data.Repositories;
using ArkPlotWpf.Model;
using System.Text.Json;
using Xunit;

namespace ArkPlotWpf.DbTests.Database.Repositories;

public class PrtsDataRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PrtsDataRepository _repository;

    public PrtsDataRepositoryTests()
    {
        _testDbPath = Path.GetTempFileName();
        _repository = new PrtsDataRepository($"Data Source={_testDbPath}");
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
    public void AddPrtsData_ShouldReturnValidId()
    {
        // Arrange
        var prtsData = new PrtsData("TestTag");
        prtsData.Data["key1"] = "value1";
        prtsData.Data["key2"] = "value2";

        // Act
        var id = _repository.AddPrtsData(prtsData);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public void GetPrtsDataByTag_ShouldReturnCorrectData()
    {
        // Arrange
        var prtsData = new PrtsData("TestTag");
        prtsData.Data["key1"] = "value1";
        prtsData.Data["key2"] = "value2";
        _repository.AddPrtsData(prtsData);

        // Act
        var result = _repository.GetPrtsDataByTag("TestTag");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestTag", result.Tag);
        Assert.Equal("value1", result.Data["key1"]);
        Assert.Equal("value2", result.Data["key2"]);
    }

    [Fact]
    public void GetPrtsDataByTag_WithNonExistingTag_ShouldReturnNull()
    {
        // Act
        var result = _repository.GetPrtsDataByTag("NonExistingTag");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAllPrtsData_ShouldReturnAllData()
    {
        // Arrange
        var prtsData1 = new PrtsData("Tag1");
        prtsData1.Data["key1"] = "value1";
        _repository.AddPrtsData(prtsData1);

        var prtsData2 = new PrtsData("Tag2");
        prtsData2.Data["key2"] = "value2";
        _repository.AddPrtsData(prtsData2);

        // Act
        var allData = _repository.GetAllPrtsData();

        // Assert
        Assert.NotNull(allData);
        Assert.True(allData.Count >= 2);
        Assert.Contains(allData, d => d.Tag == "Tag1");
        Assert.Contains(allData, d => d.Tag == "Tag2");
    }

    [Fact]
    public void UpdatePrtsData_ShouldUpdateExistingData()
    {
        // Arrange
        var originalData = new PrtsData("UpdateTag");
        originalData.Data["originalKey"] = "originalValue";
        _repository.AddPrtsData(originalData);

        var updatedData = new PrtsData("UpdateTag");
        updatedData.Data["newKey"] = "newValue";

        // Act
        var result = _repository.UpdatePrtsData(updatedData);

        // Assert
        Assert.True(result);

        var retrieved = _repository.GetPrtsDataByTag("UpdateTag");
        Assert.NotNull(retrieved);
        Assert.Equal("newValue", retrieved.Data["newKey"]);
        Assert.False(retrieved.Data.ContainsKey("originalKey"));
    }

    [Fact]
    public void UpdatePrtsData_WithNonExistingTag_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentData = new PrtsData("NonExistentTag");
        nonExistentData.Data["key"] = "value";

        // Act
        var result = _repository.UpdatePrtsData(nonExistentData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DeletePrtsData_ShouldDeleteData()
    {
        // Arrange
        var prtsData = new PrtsData("DeleteTag");
        prtsData.Data["key"] = "value";
        _repository.AddPrtsData(prtsData);

        // Act
        var result = _repository.DeletePrtsData("DeleteTag");

        // Assert
        Assert.True(result);

        var retrieved = _repository.GetPrtsDataByTag("DeleteTag");
        Assert.Null(retrieved);
    }

    [Fact]
    public void DeletePrtsData_WithNonExistingTag_ShouldReturnFalse()
    {
        // Act
        var result = _repository.DeletePrtsData("NonExistentTag");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AddOrUpdatePrtsData_ShouldAddNewData()
    {
        // Arrange
        var prtsData = new PrtsData("AddOrUpdateTag");
        prtsData.Data["key"] = "value";

        // Act
        _repository.AddOrUpdatePrtsData(prtsData);

        // Assert
        var retrieved = _repository.GetPrtsDataByTag("AddOrUpdateTag");
        Assert.NotNull(retrieved);
        Assert.Equal("value", retrieved.Data["key"]);
    }

    [Fact]
    public void AddOrUpdatePrtsData_ShouldUpdateExistingData()
    {
        // Arrange
        var originalData = new PrtsData("AddOrUpdateTag");
        originalData.Data["originalKey"] = "originalValue";
        _repository.AddPrtsData(originalData);

        var updatedData = new PrtsData("AddOrUpdateTag");
        updatedData.Data["newKey"] = "newValue";

        // Act
        _repository.AddOrUpdatePrtsData(updatedData);

        // Assert
        var retrieved = _repository.GetPrtsDataByTag("AddOrUpdateTag");
        Assert.NotNull(retrieved);
        Assert.Equal("newValue", retrieved.Data["newKey"]);
        Assert.False(retrieved.Data.ContainsKey("originalKey"));
    }

    [Fact]
    public void Exists_ShouldReturnTrueForExistingTag()
    {
        // Arrange
        var prtsData = new PrtsData("ExistsTag");
        prtsData.Data["key"] = "value";
        _repository.AddPrtsData(prtsData);

        // Act
        var exists = _repository.Exists("ExistsTag");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void Exists_ShouldReturnFalseForNonExistingTag()
    {
        // Act
        var exists = _repository.Exists("NonExistingTag");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void GetById_ShouldReturnCorrectData()
    {
        // Arrange
        var prtsData = new PrtsData("GetByIdTag");
        prtsData.Data["key"] = "value";
        var id = _repository.AddPrtsData(prtsData);

        // Act
        var result = _repository.GetById(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GetByIdTag", result.Tag);
        Assert.Equal("value", result.Data["key"]);
    }

    [Fact]
    public void GetById_WithNonExistingId_ShouldReturnNull()
    {
        // Act
        var result = _repository.GetById(999999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Delete_ShouldDeleteById()
    {
        // Arrange
        var prtsData = new PrtsData("DeleteByIdTag");
        prtsData.Data["key"] = "value";
        var id = _repository.AddPrtsData(prtsData);

        // Act
        var result = _repository.Delete(id);

        // Assert
        Assert.True(result);

        var retrieved = _repository.GetById(id);
        Assert.Null(retrieved);
    }

    [Fact]
    public void Delete_WithNonExistingId_ShouldReturnFalse()
    {
        // Act
        var result = _repository.Delete(999999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetAll_ShouldReturnAllData()
    {
        // Arrange
        var prtsData1 = new PrtsData("GetAllTag1");
        prtsData1.Data["key1"] = "value1";
        _repository.AddPrtsData(prtsData1);

        var prtsData2 = new PrtsData("GetAllTag2");
        prtsData2.Data["key2"] = "value2";
        _repository.AddPrtsData(prtsData2);

        // Act
        var allData = _repository.GetAll();

        // Assert
        Assert.NotNull(allData);
        Assert.True(allData.Count() >= 2);
        Assert.Contains(allData, d => d.Tag == "GetAllTag1");
        Assert.Contains(allData, d => d.Tag == "GetAllTag2");
    }

    [Fact]
    public void Add_ShouldReturnValidId()
    {
        // Arrange
        var prtsData = new PrtsData("AddTag");
        prtsData.Data["key"] = "value";

        // Act
        var id = _repository.Add(prtsData);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public void Update_ShouldReturnTrueForExistingData()
    {
        // Arrange
        var prtsData = new PrtsData("UpdateByIdTag");
        prtsData.Data["originalKey"] = "originalValue";
        var id = _repository.AddPrtsData(prtsData);

        var updatedData = new PrtsData("UpdateByIdTag");
        updatedData.Data["newKey"] = "newValue";

        // Act
        var result = _repository.Update(updatedData);

        // Assert
        Assert.True(result);

        var retrieved = _repository.GetById(id);
        Assert.NotNull(retrieved);
        Assert.Equal("newValue", retrieved.Data["newKey"]);
    }

    [Fact]
    public void Update_WithNonExistingData_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentData = new PrtsData("NonExistentUpdateTag");
        nonExistentData.Data["key"] = "value";

        // Act
        var result = _repository.Update(nonExistentData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AddOrUpdate_ShouldAddNewData()
    {
        // Arrange
        var prtsData = new PrtsData("AddOrUpdateByIdTag");
        prtsData.Data["key"] = "value";

        // Act
        _repository.AddOrUpdate(prtsData);

        // Assert
        var retrieved = _repository.GetPrtsDataByTag("AddOrUpdateByIdTag");
        Assert.NotNull(retrieved);
        Assert.Equal("value", retrieved.Data["key"]);
    }

    [Fact]
    public void AddOrUpdate_ShouldUpdateExistingData()
    {
        // Arrange
        var originalData = new PrtsData("AddOrUpdateByIdTag");
        originalData.Data["originalKey"] = "originalValue";
        _repository.AddPrtsData(originalData);

        var updatedData = new PrtsData("AddOrUpdateByIdTag");
        updatedData.Data["newKey"] = "newValue";

        // Act
        _repository.AddOrUpdate(updatedData);

        // Assert
        var retrieved = _repository.GetPrtsDataByTag("AddOrUpdateByIdTag");
        Assert.NotNull(retrieved);
        Assert.Equal("newValue", retrieved.Data["newKey"]);
        Assert.False(retrieved.Data.ContainsKey("originalKey"));
    }

    [Fact]
    public void DeleteByTag_ShouldDeleteData()
    {
        // Arrange
        var prtsData = new PrtsData("DeleteByTagTag");
        prtsData.Data["key"] = "value";
        _repository.AddPrtsData(prtsData);

        // Act
        var result = _repository.DeleteByTag("DeleteByTagTag");

        // Assert
        Assert.True(result);

        var retrieved = _repository.GetPrtsDataByTag("DeleteByTagTag");
        Assert.Null(retrieved);
    }

    [Fact]
    public void DeleteByTag_WithNonExistingTag_ShouldReturnFalse()
    {
        // Act
        var result = _repository.DeleteByTag("NonExistingDeleteTag");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void LargeDataSet_ShouldHandleCorrectly()
    {
        // Arrange
        var largeData = new PrtsData("LargeDataSet");
        
        // 创建大量数据（减少数量以避免超过10KB限制）
        for (int i = 0; i < 100; i++)
        {
            largeData.Data[$"key_{i}"] = $"value_{i}";
        }

        // Act
        var id = _repository.AddPrtsData(largeData);
        var retrieved = _repository.GetPrtsDataByTag("LargeDataSet");

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(retrieved);
        Assert.Equal(100, retrieved.Data.Count);

        // 验证部分数据
        Assert.Equal("value_0", retrieved.Data["key_0"]);
        Assert.Equal("value_50", retrieved.Data["key_50"]);
        Assert.Equal("value_99", retrieved.Data["key_99"]);
    }

    [Fact]
    public void EmptyData_ShouldHandleCorrectly()
    {
        // Arrange
        var emptyData = new PrtsData("EmptyTag");

        // Act
        var id = _repository.AddPrtsData(emptyData);
        var retrieved = _repository.GetPrtsDataByTag("EmptyTag");

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(retrieved);
        Assert.Equal(0, retrieved.Data.Count);
    }

    [Fact]
    public void SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var specialData = new PrtsData("SpecialCharsTag");
        specialData.Data["unicode"] = "中文测试";
        specialData.Data["symbols"] = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
        specialData.Data["newlines"] = "line1\nline2\r\nline3";

        // Act
        var id = _repository.AddPrtsData(specialData);
        var retrieved = _repository.GetPrtsDataByTag("SpecialCharsTag");

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(retrieved);
        Assert.Equal("中文测试", retrieved.Data["unicode"]);
        Assert.Equal("!@#$%^&*()_+-=[]{}|;':\",./<>?", retrieved.Data["symbols"]);
        Assert.Equal("line1\nline2\r\nline3", retrieved.Data["newlines"]);
    }
} 