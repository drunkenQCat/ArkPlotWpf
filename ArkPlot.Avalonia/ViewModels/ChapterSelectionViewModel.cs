using CommunityToolkit.Mvvm.ComponentModel;

namespace ArkPlot.Avalonia.ViewModels;

public partial class ChapterSelectionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _chapterName;

    public ChapterSelectionViewModel(string chapterName, bool isSelected = true)
    {
        _chapterName = chapterName;
        _isSelected = isSelected;
    }
}
