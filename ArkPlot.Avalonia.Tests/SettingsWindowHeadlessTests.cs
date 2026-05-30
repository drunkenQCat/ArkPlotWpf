using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArkPlot.Avalonia.Models;
using ArkPlot.Avalonia.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// SettingsWindow headless 测试：验证窗口创建和可视化树。
/// ViewModel 逻辑测试见 SettingsViewModelTests.cs。
/// </summary>
public class SettingsWindowHeadlessTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testTagsPath;

    public SettingsWindowHeadlessTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"arkplot-headless-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _testTagsPath = Path.Combine(_testDir, "tags.json");

        var tags = new Dictionary<string, string>
        {
            ["doctor"] = "博士",
            ["doctor_reg"] = ".*",
            ["amiya"] = "阿米娅",
            ["amiya_reg"] = "\\w+"
        };
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
        File.WriteAllText(_testTagsPath, JsonSerializer.Serialize(tags, options));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [AvaloniaFact]
    public void SettingsWindow_CanBeCreatedWithTabControl()
    {
        var vm = new SettingsViewModel(_testTagsPath);
        var window = new SettingsWindow
        {
            DataContext = vm
        };
        window.Show();

        Assert.True(window.IsVisible);
        Assert.Equal("设置", window.Title);

        var tabControl = window.GetVisualDescendants()
            .OfType<TabControl>()
            .FirstOrDefault();
        Assert.NotNull(tabControl);
        Assert.Equal(3, tabControl!.ItemCount);
    }

    [AvaloniaFact]
    public void SettingsWindow_LoadSettings_PopulatesDataGridAndFields()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (File.Exists(settingsPath))
            File.Delete(settingsPath);

        var vm = new SettingsViewModel(_testTagsPath);
        var window = new SettingsWindow
        {
            DataContext = vm
        };
        window.Show();

        vm.LoadSettingsCommand.Execute(null);

        Assert.Equal(2, vm.DataGrid.Count);
        Assert.Equal(NovelizerSettings.DefaultSystemPrompt, vm.SystemPromptText);
        Assert.Equal("DeepSeek", vm.SelectedProvider);
    }
}
