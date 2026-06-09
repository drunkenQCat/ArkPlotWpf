using CommunityToolkit.Mvvm.ComponentModel;

namespace ArkPlot.Avalonia.ViewModels;

/// <summary>
/// PortraitPanel 的独立 ViewModel。
/// 只接受输入属性，不关心数据从哪来。
/// </summary>
public partial class PortraitPanelViewModel : ViewModelBase
{
    /// <summary>当前立绘图片 URL。</summary>
    [ObservableProperty] private string? _portraitUrl;

    /// <summary>当前说话人名称。</summary>
    [ObservableProperty] private string _speakerName = "";

    /// <summary>是否有立绘图片。</summary>
    public bool HasPortrait => !string.IsNullOrEmpty(PortraitUrl);

    partial void OnPortraitUrlChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPortrait));
    }

    /// <summary>
    /// 更新立绘（供外部调用）。
    /// </summary>
    public void Update(string? portraitUrl, string speakerName)
    {
        PortraitUrl = portraitUrl;
        SpeakerName = speakerName;
    }

    /// <summary>
    /// 清空立绘。
    /// </summary>
    public void Clear()
    {
        PortraitUrl = null;
        SpeakerName = "";
    }
}
