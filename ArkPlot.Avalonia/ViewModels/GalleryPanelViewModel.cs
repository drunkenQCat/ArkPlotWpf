using CommunityToolkit.Mvvm.ComponentModel;

namespace ArkPlot.Avalonia.ViewModels;

/// <summary>
/// GalleryPanel 的独立 ViewModel。
/// 只接受输入属性，不关心数据从哪来。
/// </summary>
public partial class GalleryPanelViewModel : ViewModelBase
{
    /// <summary>当前背景图 URL。</summary>
    [ObservableProperty] private string? _currentBackground;

    /// <summary>上一张背景图 URL。</summary>
    [ObservableProperty] private string? _prevBackground;

    /// <summary>下一张背景图 URL。</summary>
    [ObservableProperty] private string? _nextBackground;

    /// <summary>当前背景图描述（PicDesc）。</summary>
    [ObservableProperty] private string _currentPicDescription = "";

    /// <summary>上文第一句。</summary>
    [ObservableProperty] private string _upperContext1 = "";

    /// <summary>上文第二句。</summary>
    [ObservableProperty] private string _upperContext2 = "";

    /// <summary>下文第一句。</summary>
    [ObservableProperty] private string _lowerContext1 = "";

    /// <summary>下文第二句。</summary>
    [ObservableProperty] private string _lowerContext2 = "";

    /// <summary>是否有当前背景图。</summary>
    public bool HasCurrentBackground => !string.IsNullOrEmpty(CurrentBackground);

    /// <summary>是否有上一张背景图。</summary>
    public bool HasPrevBackground => !string.IsNullOrEmpty(PrevBackground);

    /// <summary>是否有下一张背景图。</summary>
    public bool HasNextBackground => !string.IsNullOrEmpty(NextBackground);

    partial void OnCurrentBackgroundChanged(string? value) => OnPropertyChanged(nameof(HasCurrentBackground));
    partial void OnPrevBackgroundChanged(string? value) => OnPropertyChanged(nameof(HasPrevBackground));
    partial void OnNextBackgroundChanged(string? value) => OnPropertyChanged(nameof(HasNextBackground));

    /// <summary>
    /// 更新 Gallery（供外部调用）。
    /// </summary>
    public void Update(
        string? currentBackground,
        string? prevBackground,
        string? nextBackground,
        string currentPicDescription,
        string upperContext1,
        string upperContext2,
        string lowerContext1,
        string lowerContext2)
    {
        CurrentBackground = currentBackground;
        PrevBackground = prevBackground;
        NextBackground = nextBackground;
        CurrentPicDescription = currentPicDescription;
        UpperContext1 = upperContext1;
        UpperContext2 = upperContext2;
        LowerContext1 = lowerContext1;
        LowerContext2 = lowerContext2;
    }

    /// <summary>
    /// 清空 Gallery。
    /// </summary>
    public void Clear()
    {
        CurrentBackground = null;
        PrevBackground = null;
        NextBackground = null;
        CurrentPicDescription = "";
        UpperContext1 = "";
        UpperContext2 = "";
        LowerContext1 = "";
        LowerContext2 = "";
    }
}
