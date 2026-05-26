using System.Collections.ObjectModel;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArkPlot.Avalonia.Models;
using ArkPlot.Core.Model;

namespace ArkPlot.Avalonia.Tests;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testTagsPath;
    private readonly string _originalSettingsPath;

    public SettingsViewModelTests()
    {
        // Create a temporary directory for test files
        _testDir = Path.Combine(Path.GetTempPath(), $"arkplot-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _testTagsPath = Path.Combine(_testDir, "tags.json");

        // Create a minimal tags.json for testing
        var tags = new Dictionary<string, string>
        {
            ["tag1"] = "replacement1",
            ["tag1_reg"] = ".*",
            ["tag2"] = "replacement2",
            ["tag2_reg"] = "\\d+"
        };
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
        File.WriteAllText(_testTagsPath, JsonSerializer.Serialize(tags, options));

        // Backup original settings.json if exists, to avoid polluting real config
        _originalSettingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    }

    public void Dispose()
    {
        // Cleanup test files
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);

        // Restore original settings.json if it was backed up
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (File.Exists(settingsPath))
        {
            // Reset to defaults to avoid polluting real config
            var defaults = AppSettings.Load();
            var reset = defaults with
            {
                Novelizer = defaults.Novelizer with
                {
                    SystemPrompt = NovelizerSettings.DefaultSystemPrompt,
                    SelectedProvider = "DeepSeek",
                    SelectedModel = "deepseek-v4-pro",
                    ApiKeys = new Dictionary<string, string>
                    {
                        ["DeepSeek"] = "",
                        ["百炼"] = ""
                    }
                }
            };
            reset.Save();
        }
    }

    [Fact]
    public void Constructor_WithTagsPath_SetsCorrectPath()
    {
        // Act
        var vm = new ViewModels.SettingsViewModel(_testTagsPath);

        // Assert - we can verify by loading settings
        vm.LoadSettingsCommand.Execute(null);
        Assert.Equal(2, vm.DataGrid.Count);
    }

    [Fact]
    public void LoadSettings_LoadsTagsFromFile()
    {
        // Arrange
        var vm = new ViewModels.SettingsViewModel(_testTagsPath);

        // Act
        vm.LoadSettingsCommand.Execute(null);

        // Assert
        Assert.Equal(2, vm.DataGrid.Count);
        Assert.Equal("tag1", vm.DataGrid[0].Tag);
        Assert.Equal("replacement1", vm.DataGrid[0].NewTag);
        Assert.Equal(".*", vm.DataGrid[0].Reg);
    }

    [Fact]
    public void LoadSettings_LoadsNovelizerDefaults()
    {
        // Arrange - reset settings.json to defaults first
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (File.Exists(settingsPath))
            File.Delete(settingsPath);

        var vm = new ViewModels.SettingsViewModel(_testTagsPath);

        // Act
        vm.LoadSettingsCommand.Execute(null);

        // Assert
        Assert.Equal(NovelizerSettings.DefaultSystemPrompt, vm.SystemPromptText);
        Assert.Equal("DeepSeek", vm.SelectedProvider);
        Assert.Equal("deepseek-v4-pro", vm.SelectedModel);
    }

    [Fact]
    public void AddItem_AddsNewItemToGrid()
    {
        // Arrange
        var vm = new ViewModels.SettingsViewModel(_testTagsPath);
        vm.LoadSettingsCommand.Execute(null);
        var initialCount = vm.DataGrid.Count;

        // Act
        vm.AddItemCommand.Execute(null);

        // Assert
        Assert.Equal(initialCount + 1, vm.DataGrid.Count);
        Assert.Equal(0, vm.SelectedIndex);
        Assert.Contains("NewItem", vm.DataGrid[0].Tag);
    }

    [Fact]
    public void RemoveTag_RemovesTagFromGrid()
    {
        // Arrange
        var vm = new ViewModels.SettingsViewModel(_testTagsPath);
        vm.LoadSettingsCommand.Execute(null);
        var tagToRemove = vm.DataGrid[0];
        var initialCount = vm.DataGrid.Count;

        // Act
        vm.RemoveTagCommand.Execute(tagToRemove);

        // Assert
        Assert.Equal(initialCount - 1, vm.DataGrid.Count);
        Assert.DoesNotContain(tagToRemove, vm.DataGrid);
    }

    [Fact]
    public void RestoreDefaultPrompt_SetsDefaultPrompt()
    {
        // Arrange
        var vm = new ViewModels.SettingsViewModel(_testTagsPath);
        vm.LoadSettingsCommand.Execute(null);
        vm.SystemPromptText = "custom prompt that is not default";

        // Act
        vm.RestoreDefaultPromptCommand.Execute(null);

        // Assert
        Assert.Equal(NovelizerSettings.DefaultSystemPrompt, vm.SystemPromptText);
    }

    [Fact]
    public void ProviderOptions_ReturnsExpectedValues()
    {
        // Arrange
        var vm = new ViewModels.SettingsViewModel(_testTagsPath);

        // Assert
        Assert.Equal(["DeepSeek", "百炼"], vm.ProviderOptions);
    }

    [Fact]
    public void ModelOptions_ReturnsExpectedValues()
    {
        // Arrange
        var vm = new ViewModels.SettingsViewModel(_testTagsPath);

        // Assert
        Assert.Equal(["deepseek-v4-pro", "deepseek-v4-flash"], vm.ModelOptions);
    }

    [Fact]
    public void SaveNovelizerSettings_PersistsToSettingsFile()
    {
        // Arrange
        var vm = new ViewModels.SettingsViewModel(_testTagsPath);
        vm.LoadSettingsCommand.Execute(null);

        // Modify values
        vm.SystemPromptText = "test prompt";
        vm.SelectedProvider = "百炼";
        vm.SelectedModel = "deepseek-v4-flash";
        vm.DeepSeekApiKeyText = "test-ds-key";
        vm.BailianApiKeyText = "test-bailian-key";

        // Act
        vm.SaveNovelizerSettingsCommand.Execute(null);

        // Assert - reload and verify
        var reloaded = AppSettings.Load();
        Assert.Equal("test prompt", reloaded.Novelizer.SystemPrompt);
        Assert.Equal("百炼", reloaded.Novelizer.SelectedProvider);
        Assert.Equal("deepseek-v4-flash", reloaded.Novelizer.SelectedModel);
        Assert.Equal("test-ds-key", reloaded.Novelizer.ApiKeys["DeepSeek"]);
        Assert.Equal("test-bailian-key", reloaded.Novelizer.ApiKeys["百炼"]);
    }
}
