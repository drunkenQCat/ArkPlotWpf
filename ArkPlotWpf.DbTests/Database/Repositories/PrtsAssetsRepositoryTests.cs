using ArkPlotWpf.Data.Repositories;
using ArkPlotWpf.Model;
using System.Text.Json;
using Xunit;

namespace ArkPlotWpf.DbTests.Database.Repositories;

public class PrtsAssetsRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PrtsAssetsRepository _repository;
    private readonly PrtsAssets _prtsAssets;

    public PrtsAssetsRepositoryTests()
    {
        _testDbPath = Path.GetTempFileName();
        _repository = new PrtsAssetsRepository($"Data Source={_testDbPath}");
        _prtsAssets = PrtsAssets.Instance;
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
    public void SavePrtsAssets_ShouldSaveAllData()
    {
        // Arrange
        _prtsAssets.DataAudio["audio1"] = "audio_url_1";
        _prtsAssets.DataChar["char1"] = "char_url_1";
        _prtsAssets.DataImage["image1"] = "image_url_1";
        _prtsAssets.PreLoaded["preload1"] = "preload_url_1";
        _prtsAssets.DataOverrideDocument = JsonDocument.Parse("{\"override\": \"data\"}");
        _prtsAssets.PortraitLinkDocument = JsonDocument.Parse("{\"portrait\": \"link\"}");

        // Act
        _repository.SavePrtsAssets(_prtsAssets);

        // Assert
        var audioData = _repository.GetPrtsDataByTag("Data_Audio");
        var charData = _repository.GetPrtsDataByTag("Data_Char");
        var imageData = _repository.GetPrtsDataByTag("Data_Image");
        var preloadData = _repository.GetPrtsDataByTag("Data_PreLoaded");
        var overrideData = _repository.GetPrtsDataByTag("Data_Override");
        var linkData = _repository.GetPrtsDataByTag("Data_Link");

        Assert.NotNull(audioData);
        Assert.NotNull(charData);
        Assert.NotNull(imageData);
        Assert.NotNull(preloadData);
        Assert.NotNull(overrideData);
        Assert.NotNull(linkData);

        Assert.Equal("audio_url_1", audioData.Data["audio1"]);
        Assert.Equal("char_url_1", charData.Data["char1"]);
        Assert.Equal("image_url_1", imageData.Data["image1"]);
        Assert.Equal("preload_url_1", preloadData.Data["preload1"]);
        Assert.Equal("{\"override\": \"data\"}", overrideData.Data["OverrideDocument"]);
        Assert.Equal("{\"portrait\": \"link\"}", linkData.Data["PortraitLinkDocument"]);
    }

    [Fact]
    public void LoadPrtsAssets_ShouldLoadAllData()
    {
        // Arrange
        _prtsAssets.DataAudio["audio1"] = "audio_url_1";
        _prtsAssets.DataChar["char1"] = "char_url_1";
        _prtsAssets.DataImage["image1"] = "image_url_1";
        _prtsAssets.PreLoaded["preload1"] = "preload_url_1";
        _prtsAssets.DataOverrideDocument = JsonDocument.Parse("{\"override\": \"data\"}");
        _prtsAssets.PortraitLinkDocument = JsonDocument.Parse("{\"portrait\": \"link\"}");

        _repository.SavePrtsAssets(_prtsAssets);

        // 清理PrtsAssets实例
        _prtsAssets.DataAudio.Clear();
        _prtsAssets.DataChar.Clear();
        _prtsAssets.DataImage.Clear();
        _prtsAssets.PreLoaded.Clear();
        _prtsAssets.DataOverrideDocument = JsonDocument.Parse("{}");
        _prtsAssets.PortraitLinkDocument = JsonDocument.Parse("{}");

        // Act
        var loadedAssets = _repository.LoadPrtsAssets();

        // Assert
        Assert.Equal("audio_url_1", loadedAssets.DataAudio["audio1"]);
        Assert.Equal("char_url_1", loadedAssets.DataChar["char1"]);
        Assert.Equal("image_url_1", loadedAssets.DataImage["image1"]);
        Assert.Equal("preload_url_1", loadedAssets.PreLoaded["preload1"]);
        Assert.Equal("{\"override\": \"data\"}", loadedAssets.DataOverrideDocument.RootElement.ToString());
        Assert.Equal("{\"portrait\": \"link\"}", loadedAssets.PortraitLinkDocument.RootElement.ToString());
    }

    [Fact]
    public void GetPrtsDataByTag_ShouldReturnCorrectData()
    {
        // Arrange
        var testData = new PrtsData("TestTag");
        testData.Data["key1"] = "value1";
        testData.Data["key2"] = "value2";
        _repository.UpdatePrtsData(testData);

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
    public void UpdatePrtsData_ShouldUpdateExistingData()
    {
        // Arrange
        var originalData = new PrtsData("UpdateTag");
        originalData.Data["originalKey"] = "originalValue";
        _repository.UpdatePrtsData(originalData);

        var updatedData = new PrtsData("UpdateTag");
        updatedData.Data["newKey"] = "newValue";

        // Act
        _repository.UpdatePrtsData(updatedData);

        // Assert
        var result = _repository.GetPrtsDataByTag("UpdateTag");
        Assert.NotNull(result);
        Assert.Equal("newValue", result.Data["newKey"]);
        Assert.False(result.Data.ContainsKey("originalKey"));
    }

    [Fact]
    public void DeletePrtsData_ShouldDeleteData()
    {
        // Arrange
        var testData = new PrtsData("DeleteTag");
        testData.Data["key"] = "value";
        _repository.UpdatePrtsData(testData);

        // Act
        _repository.DeletePrtsData("DeleteTag");

        // Assert
        var result = _repository.GetPrtsDataByTag("DeleteTag");
        Assert.Null(result);
    }

    [Fact]
    public void SaveAndLoadPrtsAssets_WithEmptyData_ShouldHandleCorrectly()
    {
        // Arrange - 确保PrtsAssets是空的
        _prtsAssets.DataAudio.Clear();
        _prtsAssets.DataChar.Clear();
        _prtsAssets.DataImage.Clear();
        _prtsAssets.PreLoaded.Clear();
        _prtsAssets.DataOverrideDocument = JsonDocument.Parse("{}");
        _prtsAssets.PortraitLinkDocument = JsonDocument.Parse("{}");

        // Act
        _repository.SavePrtsAssets(_prtsAssets);
        var loadedAssets = _repository.LoadPrtsAssets();

        // Assert
        Assert.Empty(loadedAssets.DataAudio);
        Assert.Empty(loadedAssets.DataChar);
        Assert.Empty(loadedAssets.DataImage);
        Assert.Empty(loadedAssets.PreLoaded);
    }

    [Fact]
    public void SaveAndLoadPrtsAssets_WithLargeData_ShouldHandleCorrectly()
    {
        // Arrange
        for (int i = 0; i < 50; i++)
        {
            _prtsAssets.DataAudio[$"audio_{i}"] = $"audio_url_{i}";
            _prtsAssets.DataChar[$"char_{i}"] = $"char_url_{i}";
            _prtsAssets.DataImage[$"image_{i}"] = $"image_url_{i}";
            _prtsAssets.PreLoaded[$"preload_{i}"] = $"preload_url_{i}";
        }

        var largeOverride = JsonDocument.Parse("{\"large\": \"data\", \"items\": [1, 2, 3, 4, 5]}");
        var largePortrait = JsonDocument.Parse("{\"portrait\": \"large\", \"details\": {\"width\": 1920, \"height\": 1080}}");
        _prtsAssets.DataOverrideDocument = largeOverride;
        _prtsAssets.PortraitLinkDocument = largePortrait;

        // Act
        _repository.SavePrtsAssets(_prtsAssets);

        // 清理PrtsAssets实例
        _prtsAssets.DataAudio.Clear();
        _prtsAssets.DataChar.Clear();
        _prtsAssets.DataImage.Clear();
        _prtsAssets.PreLoaded.Clear();
        _prtsAssets.DataOverrideDocument = JsonDocument.Parse("{}");
        _prtsAssets.PortraitLinkDocument = JsonDocument.Parse("{}");

        var loadedAssets = _repository.LoadPrtsAssets();

        // Assert
        Assert.Equal(50, loadedAssets.DataAudio.Count);
        Assert.Equal(50, loadedAssets.DataChar.Count);
        Assert.Equal(50, loadedAssets.DataImage.Count);
        Assert.Equal(50, loadedAssets.PreLoaded.Count);

        Assert.Equal("audio_url_0", loadedAssets.DataAudio["audio_0"]);
        Assert.Equal("char_url_25", loadedAssets.DataChar["char_25"]);
        Assert.Equal("image_url_49", loadedAssets.DataImage["image_49"]);
        Assert.Equal("preload_url_10", loadedAssets.PreLoaded["preload_10"]);

        Assert.Contains("large", loadedAssets.DataOverrideDocument.RootElement.ToString());
        Assert.Contains("portrait", loadedAssets.PortraitLinkDocument.RootElement.ToString());
    }

    [Fact]
    public void SaveAndLoadPrtsAssets_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        _prtsAssets.DataAudio["unicode"] = "中文音频";
        _prtsAssets.DataChar["symbols"] = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
        _prtsAssets.DataImage["newlines"] = "line1\nline2\r\nline3";
        _prtsAssets.PreLoaded["special"] = "特殊字符测试";

        var specialOverride = JsonDocument.Parse("{\"unicode\": \"中文测试\", \"symbols\": \"!@#$%^&*()\"}");
        var specialPortrait = JsonDocument.Parse("{\"portrait\": \"特殊\", \"details\": {\"chars\": \"中文符号!@#\"}}");
        _prtsAssets.DataOverrideDocument = specialOverride;
        _prtsAssets.PortraitLinkDocument = specialPortrait;

        // Act
        _repository.SavePrtsAssets(_prtsAssets);

        // 清理PrtsAssets实例
        _prtsAssets.DataAudio.Clear();
        _prtsAssets.DataChar.Clear();
        _prtsAssets.DataImage.Clear();
        _prtsAssets.PreLoaded.Clear();
        _prtsAssets.DataOverrideDocument = JsonDocument.Parse("{}");
        _prtsAssets.PortraitLinkDocument = JsonDocument.Parse("{}");

        var loadedAssets = _repository.LoadPrtsAssets();

        // Assert
        Assert.Equal("中文音频", loadedAssets.DataAudio["unicode"]);
        Assert.Equal("!@#$%^&*()_+-=[]{}|;':\",./<>?", loadedAssets.DataChar["symbols"]);
        Assert.Equal("line1\nline2\r\nline3", loadedAssets.DataImage["newlines"]);
        Assert.Equal("特殊字符测试", loadedAssets.PreLoaded["special"]);

        Assert.Contains("中文测试", loadedAssets.DataOverrideDocument.RootElement.ToString());
        Assert.Contains("特殊", loadedAssets.PortraitLinkDocument.RootElement.ToString());
    }

    [Fact]
    public void SaveAndLoadPrtsAssets_WithComplexJson_ShouldHandleCorrectly()
    {
        // Arrange
        var complexOverride = JsonDocument.Parse("{\"override\": {\"key\": \"value\"}}");
        var complexPortrait = JsonDocument.Parse("{\"portrait\": {\"link\": \"url\"}}");
        _prtsAssets.DataOverrideDocument = complexOverride;
        _prtsAssets.PortraitLinkDocument = complexPortrait;

        // Act
        _repository.SavePrtsAssets(_prtsAssets);

        // 清理PrtsAssets实例
        _prtsAssets.DataOverrideDocument = JsonDocument.Parse("{}");
        _prtsAssets.PortraitLinkDocument = JsonDocument.Parse("{}");

        var loadedAssets = _repository.LoadPrtsAssets();

        // Assert
        Assert.Equal(complexOverride.RootElement.ToString(), loadedAssets.DataOverrideDocument.RootElement.ToString());
        Assert.Equal(complexPortrait.RootElement.ToString(), loadedAssets.PortraitLinkDocument.RootElement.ToString());
    }
} 