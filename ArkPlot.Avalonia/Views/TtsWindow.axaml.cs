using Avalonia.Controls;
using ArkPlot.Avalonia.Models;
using ArkPlot.Avalonia.ViewModels;
using System;
using System.Linq;

namespace ArkPlot.Avalonia.Views;

public partial class TtsWindow : Window
{
    public TtsWindow()
    {
        InitializeComponent();
    }

    public TtsWindow(TtsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        SegmentsDataGrid.SelectionChanged += SegmentsDataGrid_SelectionChanged;
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }

    private void SegmentsDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not TtsViewModel vm) return;
        vm.SelectedSegmentRows.Clear();
        foreach (var item in SegmentsDataGrid.SelectedItems.OfType<SegmentRow>())
            vm.SelectedSegmentRows.Add(item);
    }
}
