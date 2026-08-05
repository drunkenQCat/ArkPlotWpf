using ArkPlot.Avalonia.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArkPlot.Avalonia.ViewModels.Test;

/// <summary>
/// 「开发选项」测试面板的 ViewModel。
/// 目前仅含一个开发开关：Mock小说化（Novelizer 不调真实 API）。
/// 开关状态持久化到 settings.json 的 Novelizer.UseMock。
/// </summary>
public partial class DevOptionsViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool useMockNovelizer;

    public DevOptionsViewModel()
    {
        UseMockNovelizer = AppSettings.Load().Novelizer.UseMock;
    }

    partial void OnUseMockNovelizerChanged(bool value)
    {
        var settings = AppSettings.Load();
        var novelizer = settings.Novelizer with { UseMock = value };
        settings = settings with { Novelizer = novelizer };
        settings.Save();
    }
}