using ArkPlot.Avalonia.Models;

namespace ArkPlot.Avalonia.Tests;

public class AppSettingsTests
{
    [Fact]
    public void CreateDefaults_ReturnsValidSettings()
    {
        // Act
        var settings = NovelizerSettings.CreateDefaults();

        // Assert
        Assert.Equal(NovelizerSettings.DefaultSystemPrompt, settings.SystemPrompt);
        Assert.Equal("DeepSeek", settings.SelectedProvider);
        Assert.Equal("deepseek-v4-pro", settings.SelectedModel);
        Assert.NotNull(settings.ApiKeys);
        Assert.Equal(2, settings.ApiKeys.Count);
        Assert.Equal("", settings.ApiKeys["DeepSeek"]);
        Assert.Equal("", settings.ApiKeys["百炼"]);
    }

    [Fact]
    public void ProviderOptions_ContainsExpectedProviders()
    {
        Assert.Equal(["DeepSeek", "百炼"], NovelizerSettings.ProviderOptions);
    }

    [Fact]
    public void ModelOptions_ContainsExpectedModels()
    {
        Assert.Equal(["deepseek-v4-pro", "deepseek-v4-flash"], NovelizerSettings.ModelOptions);
    }

    [Fact]
    public void GetApiKey_SettingsValueOverridesEnvironment()
    {
        // Arrange
        var settings = new AppSettings(new NovelizerSettings(
            SystemPrompt: "test",
            SelectedProvider: "DeepSeek",
            SelectedModel: "deepseek-v4-pro",
            ApiKeys: new Dictionary<string, string>
            {
                ["DeepSeek"] = "my-secret-key",
                ["百炼"] = ""
            }
        ));

        // Act
        var deepSeekKey = settings.GetApiKey("DeepSeek");
        var bailianKey = settings.GetApiKey("百炼");

        // Assert
        Assert.Equal("my-secret-key", deepSeekKey);
        // 百炼 is empty in settings, so it falls back to env var (likely empty in test)
        Assert.Equal(Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? "", bailianKey);
    }

    [Fact]
    public void GetApiKey_FallsBackToEnvironmentWhenSettingsEmpty()
    {
        // Arrange
        var settings = new AppSettings(new NovelizerSettings(
            SystemPrompt: "test",
            SelectedProvider: "DeepSeek",
            SelectedModel: "deepseek-v4-pro",
            ApiKeys: new Dictionary<string, string>
            {
                ["DeepSeek"] = "",
                ["百炼"] = ""
            }
        ));

        // Act
        var deepSeekKey = settings.GetApiKey("DeepSeek");
        var bailianKey = settings.GetApiKey("百炼");

        // Assert - falls back to env vars
        Assert.Equal(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "", deepSeekKey);
        Assert.Equal(Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? "", bailianKey);
    }

    [Fact]
    public void GetApiKey_UnknownProviderReturnsEmpty()
    {
        // Arrange
        var settings = AppSettings.Load();

        // Act
        var result = settings.GetApiKey("UnknownProvider");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void Load_CreatesDefaultFileWhenNotExists()
    {
        // This tests the Load() method behavior.
        // Since the file path is hardcoded to AppContext.BaseDirectory,
        // we test that it returns valid defaults.

        // Reset to ensure defaults are created fresh
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (File.Exists(settingsPath))
            File.Delete(settingsPath);

        var settings = AppSettings.Load();

        Assert.NotNull(settings);
        Assert.NotNull(settings.Novelizer);
        Assert.Equal(NovelizerSettings.DefaultSystemPrompt, settings.Novelizer.SystemPrompt);
    }

    [Fact]
    public void SaveAndLoad_RoundTripPreservesValues()
    {
        // Arrange - load current settings and modify
        var original = AppSettings.Load();
        var modified = original with
        {
            Novelizer = original.Novelizer with
            {
                SystemPrompt = "custom prompt",
                SelectedModel = "deepseek-v4-flash",
                ApiKeys = new Dictionary<string, string>
                {
                    ["DeepSeek"] = "test-key-123",
                    ["百炼"] = "test-key-456"
                }
            }
        };

        // Act
        modified.Save();
        var reloaded = AppSettings.Load();

        // Assert
        Assert.Equal("custom prompt", reloaded.Novelizer.SystemPrompt);
        Assert.Equal("deepseek-v4-flash", reloaded.Novelizer.SelectedModel);
        Assert.Equal("test-key-123", reloaded.Novelizer.ApiKeys["DeepSeek"]);
        Assert.Equal("test-key-456", reloaded.Novelizer.ApiKeys["百炼"]);
    }
}
