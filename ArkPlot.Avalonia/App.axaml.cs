using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Controls;

using ArkPlot.Avalonia.ViewModels;
using ArkPlot.Avalonia.Views;
using ArkPlot.Avalonia.Services;
using ArkPlot.Core.Services;

namespace ArkPlot.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        var messenger = WeakReferenceMessenger.Default;
        messenger.Register<OpenWindowMessage>(this, (recipient, message) =>
                {
                    // 根据消息中的WindowName打开相应的窗口
                    if (message.WindowName == "TagEditor")
                    {
                        var editorView = new TagEditor();
                        var editorViewModel = new TagEditorViewModel(message.JsonPath);
                        editorView.DataContext = editorViewModel;
                        editorView.Show();
                    }
                });

    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            GlobalStorageProvider.StorageProvider = topLevel!.StorageProvider;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
