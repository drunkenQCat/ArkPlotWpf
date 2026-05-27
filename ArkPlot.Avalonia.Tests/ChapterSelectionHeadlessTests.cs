using System.Collections.ObjectModel;
using ArkPlot.Avalonia.ViewModels;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// ChapterSelectionViewModel 纯 ViewModel 测试（无需 headless 平台）。
/// </summary>
public class ChapterSelectionHeadlessTests
{
    [Fact]
    public void ChapterSelectionViewModel_DefaultsToSelected()
    {
        var vm = new ChapterSelectionViewModel("test_chapter");
        Assert.True(vm.IsSelected);
        Assert.Equal("test_chapter", vm.ChapterName);
    }

    [Fact]
    public void ChapterSelectionViewModel_CanBeCreatedDeselected()
    {
        var vm = new ChapterSelectionViewModel("test_chapter", false);
        Assert.False(vm.IsSelected);
    }

    [Fact]
    public void ChapterSelectionViewModel_IsSelectedCanBeToggled()
    {
        var vm = new ChapterSelectionViewModel("test_chapter", true);
        Assert.True(vm.IsSelected);

        vm.IsSelected = false;
        Assert.False(vm.IsSelected);

        vm.IsSelected = true;
        Assert.True(vm.IsSelected);
    }

    [Fact]
    public void ChapterSelectionViewModel_PropertyChanged_RaisesNotification()
    {
        var vm = new ChapterSelectionViewModel("test");
        var propertyNames = new List<string>();
        vm.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName!);

        vm.IsSelected = false;
        vm.ChapterName = "updated";

        Assert.Contains("IsSelected", propertyNames);
        Assert.Contains("ChapterName", propertyNames);
    }

    [Fact]
    public void MainWindowViewModel_SelectAllChapters_SelectsAll()
    {
        var vm = new MainWindowViewModel();
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_1", false));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_2", false));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_3", true));

        vm.SelectAllChaptersCommand.Execute(null);

        Assert.All(vm.Chapters, c => Assert.True(c.IsSelected));
    }

    [Fact]
    public void MainWindowViewModel_DeselectAllChapters_DeselectsAll()
    {
        var vm = new MainWindowViewModel();
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_1", true));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_2", true));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_3", false));

        vm.DeselectAllChaptersCommand.Execute(null);

        Assert.All(vm.Chapters, c => Assert.False(c.IsSelected));
    }

    [Fact]
    public void MainWindowViewModel_AddingChapters_UpdatesCollection()
    {
        var vm = new MainWindowViewModel();
        Assert.Empty(vm.Chapters);

        vm.Chapters.Add(new ChapterSelectionViewModel("new_chapter_1"));
        vm.Chapters.Add(new ChapterSelectionViewModel("new_chapter_2"));

        Assert.Equal(2, vm.Chapters.Count);
        Assert.Equal("new_chapter_1", vm.Chapters[0].ChapterName);
        Assert.Equal("new_chapter_2", vm.Chapters[1].ChapterName);
    }

    [Fact]
    public void MainWindowViewModel_ClearingChapters_EmptiesCollection()
    {
        var vm = new MainWindowViewModel();
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_1"));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_2"));

        Assert.Equal(2, vm.Chapters.Count);
        vm.Chapters.Clear();
        Assert.Empty(vm.Chapters);
    }

    [Fact]
    public void MainWindowViewModel_SelectedIndexChange_TriggersChapterClear()
    {
        var vm = new MainWindowViewModel();
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_1"));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_2"));
        vm.Chapters.Add(new ChapterSelectionViewModel("ch_3"));

        Assert.Equal(3, vm.Chapters.Count);

        vm.SelectedIndex = 5;

        Assert.Empty(vm.Chapters);
    }

    [Fact]
    public void MainWindowViewModel_ChaptersCollection_IsObservable()
    {
        var vm = new MainWindowViewModel();
        var collectionChangedRaised = false;
        vm.Chapters.CollectionChanged += (_, _) => collectionChangedRaised = true;

        vm.Chapters.Add(new ChapterSelectionViewModel("test"));

        Assert.True(collectionChangedRaised);
    }
}
