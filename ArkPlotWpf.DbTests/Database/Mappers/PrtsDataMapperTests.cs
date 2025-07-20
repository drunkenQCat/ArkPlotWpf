using ArkPlotWpf.Data.Entities;
using ArkPlotWpf.Data.Mappers;
using ArkPlotWpf.Model;
using System.Text.Json;
using Xunit;

namespace ArkPlotWpf.DbTests.Database.Mappers;

public class PrtsDataMapperTests
{
    private readonly PrtsDataMapper _mapper;

    public PrtsDataMapperTests()
    {
        _mapper = new PrtsDataMapper();
    }

    [Fact]
    public void ToEntity_ShouldMapCorrectly()
    {
        // Arrange
        var prtsData = new PrtsData("TestTag");
        prtsData.Data["key1"] = "value1";
        prtsData.Data["key2"] = "value2";

        // Act
        var entity = _mapper.ToEntity(prtsData);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("TestTag", entity.Tag);
        Assert.NotNull(entity.DataJson);
        Assert.Contains("key1", entity.DataJson);
        Assert.Contains("value1", entity.DataJson);
        Assert.Contains("key2", entity.DataJson);
        Assert.Contains("value2", entity.DataJson);
    }


    [Fact]
    public void ToModel_WithEmptyJson_ShouldReturnEmptyData()
    {
        // Arrange
        var entity = new PrtsDataEntity
        {
            Id = 1,
            Tag = "EmptyTag",
            DataJson = "{}"
        };

        // Act
        var model = _mapper.ToModel(entity);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("EmptyTag", model.Tag);
        Assert.Empty(model.Data);
    }

    [Fact]
    public void ToModel_WithNullJson_ShouldReturnEmptyData()
    {
        // Arrange
        var entity = new PrtsDataEntity
        {
            Id = 1,
            Tag = "NullTag",
            DataJson = null
        };

        // Act
        var model = _mapper.ToModel(entity);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("NullTag", model.Tag);
        Assert.Empty(model.Data);
    }

    [Fact]
    public void ToModel_WithInvalidJson_ShouldReturnEmptyData()
    {
        // Arrange
        var entity = new PrtsDataEntity
        {
            Id = 1,
            Tag = "InvalidTag",
            DataJson = "invalid json string"
        };

        // Act
        var model = _mapper.ToModel(entity);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("InvalidTag", model.Tag);
        Assert.Empty(model.Data);
    }

    [Fact]
    public void ToEntity_WithComplexData_ShouldSerializeCorrectly()
    {
        // Arrange
        var prtsData = new PrtsData("ComplexTag");
        prtsData.Data["simple"] = "value";
        prtsData.Data["array"] = JsonSerializer.Serialize(new[] { 1, 2, 3 });
        prtsData.Data["object"] = JsonSerializer.Serialize(new { key = "value", nested = new { deep = "data" } });

        // Act
        var entity = _mapper.ToEntity(prtsData);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("ComplexTag", entity.Tag);
        Assert.NotNull(entity.DataJson);
        Assert.Contains("simple", entity.DataJson);
        Assert.Contains("value", entity.DataJson);
        Assert.Contains("array", entity.DataJson);
        Assert.Contains("[1,2,3]", entity.DataJson);
        Assert.Contains("object", entity.DataJson);
        Assert.Contains("key", entity.DataJson);
        Assert.Contains("nested", entity.DataJson);
    }

    [Fact]
    public void ToModel_WithComplexJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var entity = new PrtsDataEntity
        {
            Id = 1,
            Tag = "ComplexTag",
            DataJson = "{\"simple\":\"value\",\"array\":\"[1,2,3]\",\"object\":\"{\\\"key\\\":\\\"value\\\",\\\"nested\\\":{\\\"deep\\\":\\\"data\\\"}}\"}"
        };

        // Act
        var model = _mapper.ToModel(entity);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("ComplexTag", model.Tag);
        Assert.Equal("value", model.Data["simple"]);
        Assert.Equal("[1,2,3]", model.Data["array"]);
        Assert.Contains("key", model.Data["object"].ToString());
        Assert.Contains("value", model.Data["object"].ToString());
    }

    [Fact]
    public void ToEntity_WithUnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var prtsData = new PrtsData("UnicodeTag");
        prtsData.Data["chinese"] = "中文测试";
        prtsData.Data["symbols"] = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
        prtsData.Data["newlines"] = "line1\nline2\r\nline3";

        // Act
        var entity = _mapper.ToEntity(prtsData);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("UnicodeTag", entity.Tag);
        Assert.NotNull(entity.DataJson);
        Assert.Contains("中文测试", entity.DataJson);
        Assert.Contains("!@#$%^&*()_+-=[]{}|;':\\\",./<>?", entity.DataJson);
        Assert.Contains("line1\\nline2\\r\\nline3", entity.DataJson);
    }

    [Fact]
    public void ToModel_WithUnicodeJson_ShouldHandleCorrectly()
    {
        // Arrange
        var entity = new PrtsDataEntity
        {
            Id = 1,
            Tag = "UnicodeTag",
            DataJson = "{\"chinese\":\"中文测试\",\"symbols\":\"!@#$%^&*()_+-=[]{}|;':\\\",./<>?\",\"newlines\":\"line1\\nline2\\r\\nline3\"}"
        };

        // Act
        var model = _mapper.ToModel(entity);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("UnicodeTag", model.Tag);
        Assert.Equal("中文测试", model.Data["chinese"]);
        Assert.Equal("!@#$%^&*()_+-=[]{}|;':\",./<>?", model.Data["symbols"]);
        Assert.Equal("line1\nline2\r\nline3", model.Data["newlines"]);
    }

    [Fact]
    public void ToEntity_WithEmptyData_ShouldCreateEmptyJson()
    {
        // Arrange
        var prtsData = new PrtsData("EmptyDataTag");

        // Act
        var entity = _mapper.ToEntity(prtsData);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("EmptyDataTag", entity.Tag);
        Assert.Equal("{}", entity.DataJson);
    }

    [Fact]
    public void ToModel_WithEmptyJson_ShouldCreateEmptyData()
    {
        // Arrange
        var entity = new PrtsDataEntity
        {
            Id = 1,
            Tag = "EmptyJsonTag",
            DataJson = "{}"
        };

        // Act
        var model = _mapper.ToModel(entity);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("EmptyJsonTag", model.Tag);
        Assert.Empty(model.Data);
    }

    [Fact]
    public void ToEntity_WithNullData_ShouldCreateEmptyJson()
    {
        // Arrange
        var prtsData = new PrtsData("NullDataTag");

        // Act
        var entity = _mapper.ToEntity(prtsData);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("NullDataTag", entity.Tag);
        Assert.Equal("{}", entity.DataJson);
    }

    [Fact]
    public void ToModel_WithNullEntity_ShouldReturnNull()
    {
        // Act
        var model = _mapper.ToModel(null);

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public void ToEntity_WithNullModel_ShouldReturnNull()
    {
        // Act
        var entity = _mapper.ToEntity(null);

        // Assert
        Assert.Null(entity);
    }

    [Fact]
    public void ToEntity_WithLargeData_ShouldSerializeCorrectly()
    {
        // Arrange
        var prtsData = new PrtsData("LargeDataTag");
        for (int i = 0; i < 100; i++)
        {
            prtsData.Data[$"key_{i}"] = $"value_{i}";
        }

        // Act
        var entity = _mapper.ToEntity(prtsData);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("LargeDataTag", entity.Tag);
        Assert.NotNull(entity.DataJson);
        Assert.Contains("key_0", entity.DataJson);
        Assert.Contains("value_0", entity.DataJson);
        Assert.Contains("key_99", entity.DataJson);
        Assert.Contains("value_99", entity.DataJson);
    }

    [Fact]
    public void ToModel_WithLargeJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var jsonBuilder = new System.Text.StringBuilder();
        jsonBuilder.Append("{");
        for (int i = 0; i < 100; i++)
        {
            if (i > 0) jsonBuilder.Append(",");
            jsonBuilder.Append($"\"key_{i}\":\"value_{i}\"");
        }
        jsonBuilder.Append("}");

        var entity = new PrtsDataEntity
        {
            Id = 1,
            Tag = "LargeJsonTag",
            DataJson = jsonBuilder.ToString()
        };

        // Act
        var model = _mapper.ToModel(entity);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("LargeJsonTag", model.Tag);
        Assert.Equal(100, model.Data.Count);
        Assert.Equal("value_0", model.Data["key_0"]);
        Assert.Equal("value_99", model.Data["key_99"]);
    }
} 