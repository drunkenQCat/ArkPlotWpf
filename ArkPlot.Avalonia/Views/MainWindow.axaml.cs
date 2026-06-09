using Avalonia.Input;
using SukiUI.Controls;

namespace ArkPlot.Avalonia.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BannerImage_Tapped(object? sender, TappedEventArgs e)
    {
        var testWindow = new TestWindow();
        testWindow.Show();
    }
}
