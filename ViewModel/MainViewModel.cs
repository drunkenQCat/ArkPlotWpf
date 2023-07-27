using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows;
using ArkPlotWpf.Utilities;
using AkGetter = ArkPlotWpf.Utilities.AkGetter;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using ArkPlotWpf.Model;

namespace ArkPlotWpf.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    string actName = "照我以火";
    [ObservableProperty]
    string jsonPath = Environment.CurrentDirectory + @"\tags.json";
    [ObservableProperty]
    string consoleOutput = string.Format("这是一个生成明日方舟剧情markdown/html文件的生成器，使用时有以下注意事项\n\n" +
                                         "* 因为下载剧情文本需要连接GitHub的服务器，所以在使用时务必先科学上网；\n" +
                                         "* 如果遇到报错\"出错的句子:****\"，请手动在tags.json里添加相应的tag的正则表达式；\n" +
                                         "* 如果有任何改进意见，欢迎Pr。\n");

    [ObservableProperty] 
    string outputPath = Environment.CurrentDirectory + @"\output";

    [ObservableProperty] 
    ICollectionView storiesNames;

    [ObservableProperty] 
    int selectedIndex = 0;
    ActInfo CurrentAct => currentActInfos[SelectedIndex];
    
    NotificationBlock notiBlock = NotificationBlock.Instance;
    ReviewTableParser actsTable = new();
    string language = "zh_CN";
    string storyType = "ACTIVITY_STORY";
    List<ActInfo> currentActInfos = new();

    [RelayCommand]
    async Task LoadMd()
    {
        ConsoleOutput = ""; //先清空这片区域
        SubscribeAll();
        var content = new AkGetter(CurrentAct);
        var activeTitle = CurrentAct.Tokens["name"]?.ToString();

        //大工程，把所有的章节都下载下来
        await content.GetAllChapters();
        var allContent = content.ContentTable;
        var exportMd = AkProcessor.ExportPlots(allContent, jsonPath);
        var finalMd = "# " + activeTitle + "\r\n\r\n" + exportMd;
        if (Directory.Exists(outputPath) == false)
        {
            Directory.CreateDirectory(outputPath);
        }
        AkProcessor.WriteMd(outputPath, activeTitle!, finalMd);
        AkProcessor.WriteHtml(outputPath, activeTitle!, finalMd);
        var result = MessageBox.Show("生成完成。是否打开文件夹？", "markdown/html文件生成完成！",  MessageBoxButton.OKCancel);
        if (result == MessageBoxResult.OK)
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    Arguments = outputPath,
                    FileName = "explorer.exe",
                    Verb = "runas"
                };
                Process.Start(startInfo);
            }
            catch (Win32Exception win32Exception)
            {
                //The system cannot find the file specified...
                MessageBox.Show(win32Exception.Message);
            }
        }
    }

    private void SubscribeAll()
    {
        SubscribeCommonNotification();
        SubscribeChapterLoadedNotification();
        SubscribeNetErrorNotification();
        SubscribeLineNoMatchNotification();
    }

    [RelayCommand]
    void LoadActs(string type)
    {
        storyType = type;
        var currentTokens = actsTable.GetStories(type);
        currentActInfos =
            (from act in currentTokens
                let name = act["name"].ToString()
                let info = new ActInfo(language, storyType, name, act)
                select info
            ).ToList();
        StoriesNames = CollectionViewSource.GetDefaultView(
            from info in currentActInfos
            select info.Name
            );
        SelectedIndex = 0;
    }
    
    [RelayCommand]
    async Task LoadLangs(string lang)
    {
        language = lang;
        await Task.Run(()=>actsTable.Lang = lang);
        LoadActs(storyType);
    }

    private void SubscribeCommonNotification()
    {
        notiBlock.CommonEventHandler += (_, args) => ConsoleOutput += $"\r\n{args}";
    }

    private void SubscribeNetErrorNotification()
    {
        notiBlock.NetErrorHappen += (_, args) =>
        {
            var s = $"\r\n网络错误：{args.Message}，请确认是否连接代理？";
            ConsoleOutput += s;
            MessageBox.Show(s);
        };
    }

    private void SubscribeLineNoMatchNotification()
    {
        notiBlock.LineNoMatch += (_, args) =>
        {
            var s = $"\r\n警告：请检查tags.json中{args.Tag}是否存在？\r\n出错的句子：" + args.Line;
            ConsoleOutput += s;
        };
    }

    private void SubscribeChapterLoadedNotification()
    {
        notiBlock.ChapterLoaded += (_, args) =>
        {
            var s = "\r\n" + args.Title.ToString() + "已加载";
            ConsoleOutput += s;
        };
    }


    public void SelectJsonFile(string path) => JsonPath = path;

    public void SelectOutputFolder(string path) => OutputPath = path;

    internal void DropJsonFile(string v) => JsonPath = v;
}