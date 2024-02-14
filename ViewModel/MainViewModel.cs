using ArkPlotWpf.Model;
using ArkPlotWpf.Utilities;
using ArkPlotWpf.View;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using AkGetter = ArkPlotWpf.Utilities.AkpGetter;

namespace ArkPlotWpf.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    string jsonPath = Environment.CurrentDirectory + @"\tags.json";
    [ObservableProperty]
    string consoleOutput = string.Format("这是一个生成明日方舟剧情markdown/html文件的生成器，使用时有以下注意事项\n\n" +
                                         "* 因为下载剧情文本需要连接GitHub的服务器，所以在使用时务必先科学上网；\n" +
                                         "* 如果遇到报错【出错的句子:****】，如过于影响阅读体验，需要结合报错信息填写相应正则表达式来规整，请点击“编辑Tags”按钮，添加相应tag的项目；\n" +
                                         "* 如果有任何改进意见，欢迎Pr。\n");

    [ObservableProperty]
    string outputPath = Environment.CurrentDirectory + @"\output";

    [ObservableProperty]
    ICollectionView? storiesNames = CollectionViewSource.GetDefaultView(new[]{ "加载中，请稍等..."});

    [ObservableProperty]
    int selectedIndex;

    [ObservableProperty]
    bool isInitialized;

    ActInfo CurrentAct => currentActInfos[SelectedIndex];

    readonly NotificationBlock notiBlock = NotificationBlock.Instance;
    readonly ReviewTableParser actsTable = new();

    string language = "zh_CN";
    string storyType = "ACTIVITY_STORY";

    List<ActInfo> currentActInfos = new();
    readonly ResourceCsv resourceCsv = ResourceCsv.Instance;

    [RelayCommand]
    async Task LoadMd()
    {
        IsInitialized = false;
        ClearConsoleOutput();
        var content = new AkGetter(CurrentAct);
        var activeTitle = CurrentAct.Tokens["name"]?.ToString();

        //大工程，把所有的章节都下载下来
        await content.GetAllChapters();
        var allPlots = content.ContentTable;
        notiBlock.RaiseCommonEvent("正在处理文本....");
        string exportMd = await ExportPlots(allPlots);
        var mdWithTitle = "# " + activeTitle + "\r\n\r\n" + exportMd;
        if (Directory.Exists(outputPath) == false)
        {
            Directory.CreateDirectory(outputPath);
        }
        var markdown = new Plot(activeTitle!, new(mdWithTitle));
        AkpProcessor.WriteMd(outputPath, markdown);
        AkpProcessor.WriteHtml(outputPath, markdown);
        var result = MessageBox.Show("生成完成。是否打开文件夹？", "markdown/html文件生成完成！", MessageBoxButton.OKCancel);
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
        IsInitialized = true;
    }


    public async Task<string> LoadSingleMd()
    {
        List<Plot> allPlots;
        FileInfo plotsJsonFile = new FileInfo("C:\\TechnicalProjects\\ArkPlot\\ArkPlotWpf\\all_plots.json");
        if (!plotsJsonFile.Exists)
        {
            var content = new AkGetter(currentActInfos[0]); // 假设currentActInfos[0]是合法的参数
            await content.GetAllChapters();
            allPlots = content.ContentTable;
            string plotJson = JsonConvert.SerializeObject(allPlots, Formatting.Indented); // 使用Newtonsoft.Json进行序列化

            // 将序列化的JSON字符串写入文件
            await File.WriteAllTextAsync(plotsJsonFile.FullName, plotJson);
        }
        else
        {
            // 从文件中读取JSON字符串并反序列化
            string plotJson = await File.ReadAllTextAsync(plotsJsonFile.FullName);
            allPlots = JsonConvert.DeserializeObject<List<Plot>>(plotJson)!; // 使用Newtonsoft.Json进行反序列化
        }

        var testPlot = allPlots.First();
        var title = testPlot.Title;
        return title + "\n" + testPlot.Content;
    }

    private void ClearConsoleOutput()
    {
        ConsoleOutput = ""; //先清空这片区域
    }

    private async Task<string> ExportPlots(List<Plot> allPlots)
    {
        var output = await Task.Run(() => AkpProcessor.ExportPlots(allPlots, jsonPath));
        return output;
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
             let name = act["name"]!.ToString()
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
    async Task LoadInitResource()
    {
        SubscribeAll();
        await LoadResourceTable();
        await LoadLangTable(language);
        IsInitialized = true;
    }

    private async Task LoadResourceTable()
    {
        try
        {
            await resourceCsv.GetAllCsv();
            notiBlock.RaiseCommonEvent("【prts资源索引文件加载完成\r\n】");
        }
        catch (Exception)
        {
            var s = "\r\n网络错误，无法加载资源文件。\r\n";
            notiBlock.RaiseCommonEvent(s);
            MessageBox.Show(s);
        }
    }

    [RelayCommand]
    private async Task LoadLangTable(string lang)
    {

        try
        {
            language = lang;
            await Task.Run(() => actsTable.Lang = lang);
            notiBlock.RaiseCommonEvent("【剧情索引文件加载完成】\r\n");
            LoadActs(storyType);
        }
        catch (Exception)
        {
            var s = "\r\n索引文件加载出错！请检查网络代理。\r\n";
            notiBlock.RaiseCommonEvent(s);
            MessageBox.Show(s);
        }
    }

    [RelayCommand]
    void OpenTagEditor()
    {
        var editorView = new TagEditor();
        var editorViewModel = new TagEditorViewModel(jsonPath, editorView.Close);
        editorView.DataContext = editorViewModel;
        editorView.Show();
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
