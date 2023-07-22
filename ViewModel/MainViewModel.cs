using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

using ArkPlotWpf.Model;
using System.Collections.Generic;
using System.Windows;
using AkGetter = ArkPlotWpf.Utilities.AkGetter;

namespace ArkPlotWpf.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    string actName = "照我以火";
    [ObservableProperty]
    string jsonPath = System.Environment.CurrentDirectory + @"\tags.json";
    [ObservableProperty]
    bool isGitee = false;
    [ObservableProperty]
    string consoleOutput = string.Format("这是一个生成明日方舟剧情markdown文件的生成器，使用时有以下注意事项\n\n" +
                                         "* 活动名那里一定要写中文名；\n" +
                                         "* 因为下载剧情文本需要连接GitHub的服务器，所以在使用时务必先科学上网；\n" +
                                         "* 国内源（Gitee）因为Gitee的审查制度，很多章节会被直接和谐，不建议勾选；\n" +
                                         "* 如果遇到报错\"出错的句子:****\"，请手动在tags.json里添加相应的tag的正则表达式；\n" +
                                         "* 如果有任何改进意见，欢迎Pr。\n");

    [ObservableProperty] 
    string outputPath = System.Environment.CurrentDirectory;

    // [ObservableProperty] 
    // ObservableCollection<ConsoleOut> consoleOuts;

    [RelayCommand]
    async Task LoadMd()
    {
        // ConsoleOutput = ""; //先清空这片区域
        var linker = new AkLinker(actName);
        var content = new AkGetter(linker.ActiveCode,isGitee);
        var activeTitle = linker.ActiveName;

        //大工程，把所有的章节都下载下来
        await content.GetAllChapters();
        var allContent = content.ContentTable;
        var linkedContent = linker.LinkStages(allContent);
        // 处理每一章，最后导出
        var exportMd = AkProcessor.ExportPlots(linkedContent, jsonPath);
        var finalMd = "# "+ activeTitle + "\r\n\r\n" + exportMd;
        AkProcessor.WriteMd(outputPath, activeTitle, finalMd);
        AkProcessor.WriteHtml(outputPath, activeTitle, finalMd);
        MessageBox.Show("生成完成！");
    }

    public void SelectFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog()
        {
            Filter = "|*.json"
        };
        if (dialog.ShowDialog() == true)
        {
            JsonPath = dialog.FileName;
        }
        
    }
}