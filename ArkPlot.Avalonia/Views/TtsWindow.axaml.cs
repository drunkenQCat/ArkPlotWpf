using Avalonia.Controls;
using ArkPlot.Avalonia.ViewModels;
using System;

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
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
}
