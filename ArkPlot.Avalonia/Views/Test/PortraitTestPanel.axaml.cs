using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ArkPlot.Avalonia.Views.Test;

public partial class PortraitTestPanel : UserControl
{
    public PortraitTestPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
