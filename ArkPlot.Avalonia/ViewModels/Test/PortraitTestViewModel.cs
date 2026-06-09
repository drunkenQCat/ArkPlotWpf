using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArkPlot.Avalonia.ViewModels.Test;

/// <summary>
/// PortraitPanel 测试用 ViewModel。
/// 内部持有 PortraitPanelViewModel 实例，测试控制直接操作它。
/// </summary>
public partial class PortraitTestViewModel : ViewModelBase
{
    /// <summary>被测试的组件 ViewModel（直接绑定到 PortraitPanel）。</summary>
    public PortraitPanelViewModel Panel { get; } = new();

    // ── 测试控制命令 ──

    private static readonly string[] SamplePortraits =
    [
        "https://media.prts.wiki/8/82/Avg_avg_npc_892_1-2%241.png",
        "https://media.prts.wiki/f/f9/Avg_avg_npc_536_1-2%241.png",
        "https://media.prts.wiki/1/1e/Avg_char_136_hsguma_ns_1.png",
        "https://media.prts.wiki/1/15/Avg_avg_npc_134.png",
        "https://media.prts.wiki/0/08/Avg_avg_npc_892_1-4%241.png",
    ];

    private static readonly string[] SampleNames =
    [
        "小贾斯汀",
        "缪尔赛思",
        "星熊",
        "avg_npc_134",
        "avg_npc_892",
    ];

    private int _sampleIndex;

    [RelayCommand]
    private void LoadSample()
    {
        Panel.Update(
            SamplePortraits[_sampleIndex % SamplePortraits.Length],
            SampleNames[_sampleIndex % SampleNames.Length]);
        _sampleIndex++;
    }

    [RelayCommand]
    private void Clear()
    {
        Panel.Clear();
    }
}
