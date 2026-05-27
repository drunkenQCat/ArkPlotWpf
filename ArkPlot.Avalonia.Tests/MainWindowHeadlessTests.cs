using ArkPlot.Avalonia.ViewModels;
using ArkPlot.Avalonia.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using System.Linq;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// MainWindow headless 测试：验证窗口创建、可视化树结构和数据绑定。
/// 仅保留少量关键测试以避免 SukiUI 初始化导致的长耗时。
/// </summary>
public class MainWindowHeadlessTests
{
    [AvaloniaFact]
    public void MainWindow_CanBeCreatedAndShown()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel()
        };
        window.Show();

        Assert.True(window.IsVisible);
        Assert.Equal("ArkPlot", window.Title);
    }

    [AvaloniaFact]
    public void MainWindow_ContainsStoryTypeAndLanguageRadioButtons()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel()
        };
        window.Show();

        var radioButtons = window.GetVisualDescendants()
            .OfType<RadioButton>()
            .Select(r => r.Content?.ToString())
            .ToList();

        Assert.Contains("插曲", radioButtons);
        Assert.Contains("故事集", radioButtons);
        Assert.Contains("主题曲", radioButtons);
        Assert.Contains("简中", radioButtons);
        Assert.Contains("Eng", radioButtons);
    }

    [AvaloniaFact]
    public void MainWindow_ConsoleOutputBinding_WorksInHeadless()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow
        {
            DataContext = vm
        };
        window.Show();

        vm.ConsoleOutput = "headless测试输出";

        var readOnlyTextBox = window.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(t => t.IsReadOnly);

        Assert.NotNull(readOnlyTextBox);
        Assert.Equal("headless测试输出", readOnlyTextBox!.Text);
    }

    [AvaloniaFact]
    public void MainWindow_SelectAndDeselectAllChapters_WorksInHeadless()
    {
        var vm = new MainWindowViewModel();
        vm.Chapters.Add(new ChapterSelectionViewModel("ch1", false));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch2", false));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch3", true));

        var window = new MainWindow
        {
            DataContext = vm
        };
        window.Show();

        vm.SelectAllChaptersCommand.Execute(null);
        Assert.All(vm.Chapters, c => Assert.True(c.IsSelected));

        vm.DeselectAllChaptersCommand.Execute(null);
        Assert.All(vm.Chapters, c => Assert.False(c.IsSelected));
    }
}
