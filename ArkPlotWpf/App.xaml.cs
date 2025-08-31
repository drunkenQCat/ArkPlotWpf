using System.Windows;
using ArkPlot.Core.Services;
using ArkPlotWpf.View;
using ArkPlotWpf.ViewModel;
using CommunityToolkit.Mvvm.Messaging;

namespace ArkPlotWpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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
    }

}
