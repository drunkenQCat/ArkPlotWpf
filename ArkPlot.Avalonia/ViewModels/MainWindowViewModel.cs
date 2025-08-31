using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArkPlot.Avalonia.Services;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities; // Added for AkpProcessor
using ArkPlot.Core.Utilities.ArknightsDbComponents;
using ArkPlot.Core.Utilities.PrtsComponents;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using SukiUI.Toasts;
using SukiUI.Controls;
using Avalonia.Controls.Notifications;


// ReSharper disable InconsistentNaming

namespace ArkPlot.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ReviewTableParser actsTable = new();

    private readonly NotificationBlock noticeBlock = NotificationBlock.Instance;
    private readonly PrtsDataProcessor prts = new();
    [ObservableProperty] private ISukiToastManager toastManager = new SukiToastManager();  // public, 只读属性

    [ObservableProperty] private string consoleOutput = @"这是一个生成明日方舟剧情markdown/html文件的生成器，使用时有以下注意事项:

        - 因为下载剧情文本需要连接GitHub的服务器，所以在使用时务必先科学上网；
            - 如果遇到报错【出错的句子:****】，如过于影响阅读体验，需要结合报错信息填写相应正则表达式来规整，请点击“编辑Tags”按钮，添加相应tag的项目；
            - 如果有任何改进意见，欢迎Pr。";

    private List<ActInfo> currentActInfos = new();

    [ObservableProperty] private bool isInitialized;

    [ObservableProperty] private bool isLocalResChecked;

    [ObservableProperty] private string jsonPath = AppContext.BaseDirectory + @"\tags.json";

    private string language = "zh_CN";

    [ObservableProperty] private string outputPath = Environment.CurrentDirectory + @"\output";

    private string outputPathOfCurrentStory => OutputPath + @"\" + activeTitle; // Changed outputPath to OutputPath

    [ObservableProperty] private int selectedIndex;

    [ObservableProperty]
    private System.Collections.Generic.IEnumerable<string>? storiesNames; // Changed ICollectionView to IEnumerable and removed CollectionViewSource.GetDefaultView
    [ObservableProperty]
    private string status = "准备中...";


    private string storyType = "ACTIVITY_STORY";
    private string? activeTitle;

    private ActInfo CurrentAct => currentActInfos[SelectedIndex];

    [ObservableProperty]
    private ObservableCollection<ChapterSelectionViewModel> _chapters = new();

    // 当用户切换主活动时，需要清空旧的章节列表，以便下次点击按钮时重新加载
    partial void OnSelectedIndexChanged(int value)
    {
        Chapters.Clear();
        
    }

    [RelayCommand]
    private async Task LoadChapters()
    {
        // 如果当前活动没有章节，则加载它们
        if (CurrentAct != null && Chapters.Count == 0)
        {
            await LoadChaptersForCurrentAct();
        }
    }

    private async Task LoadChaptersForCurrentAct()
    {
        Chapters.Clear();
        Status = "正在加载章节列表...";

        var storyLoader = new AkpStoryLoader(CurrentAct);
        var chapterNames = await storyLoader.GetChapterNamesAsync();

        foreach (var name in chapterNames)
        {
            Chapters.Add(new ChapterSelectionViewModel(name));
        }
        Status = "章节列表加载完成。";
    }

    [RelayCommand]
    private void SelectAllChapters()
    {
        foreach (var chapter in Chapters)
        {
            chapter.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAllChapters()
    {
        foreach (var chapter in Chapters)
        {
            chapter.IsSelected = false;
        }
    }


    [RelayCommand]
    private async Task LoadInitResource()
    {
        SubscribeAll();
        await Task.Yield();
        Status = $"正在加载Prts资源索引...";
        ToastManager.CreateToast()
            .WithTitle("初始化中")
            .WithContent(Status)
            .WithLoadingState(true)
            .Dismiss().After(TimeSpan.FromSeconds(7))
            .Queue();

        var sw = Stopwatch.StartNew();
        await LoadResourceTable();
        sw.Stop();
        Status = $"Prts资源索引加载完成，耗时：{sw.ElapsedMilliseconds / 1000} s";
        ToastManager.CreateToast()
            .WithTitle("初始化中")
            .OfType(NotificationType.Success)
            .WithContent(Status)
            .Dismiss().After(TimeSpan.FromSeconds(1))
            .Queue();

        Status = $"正在加载活动列表...";
        ToastManager.CreateToast()
            .WithTitle("初始化中")
            .WithContent(Status)
            .WithLoadingState(true)
            .Dismiss().After(TimeSpan.FromSeconds(2))
            .Queue();
        await LoadLangTable(language);
        Status = $"初始化已完成";
        ToastManager.CreateToast()
            .WithTitle("初始化中")
            .OfType(NotificationType.Success)
            .WithContent(Status)
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Queue();
        IsInitialized = true;
    }

    private void SubscribeAll()
    {
        SubscribeCommonNotification();
        SubscribeChapterLoadedNotification();
        SubscribeNetErrorNotification();
        SubscribeLineNoMatchNotification();
    }

    private async Task LoadResourceTable()
    {
        try
        {
            await prts.GetAllData();
            noticeBlock.RaiseCommonEvent("【prts资源索引文件加载完成】\n");
        }
        catch (Exception)
        {
            var s = "\n网络错误，无法加载资源文件。\n";
            noticeBlock.RaiseCommonEvent(s);
            // Removed MessageBox.Show(s);
            MessageBoxManager.GetMessageBoxStandard(
                    title: "网络异常",
                    text: s,
                    @enum: ButtonEnum.Ok,
                    icon: Icon.Error
                    );
        }
    }

    [RelayCommand]
    private async Task LoadLangTable(string lang)
    {
        try
        {
            language = lang;
            await Task.Run(() => actsTable.Lang = lang);
            noticeBlock.RaiseCommonEvent("【剧情索引文件加载完成】\n");
            LoadActs(storyType);
        }
        catch (Exception)
        {
            var s = "\n索引文件加载出错！请检查网络代理。\n";
            noticeBlock.RaiseCommonEvent(s);
            // Removed MessageBox.Show(s);
        }
    }

    [RelayCommand]
    private void LoadActs(string type)
    {
        storyType = type;
        var currentTokens = actsTable.GetStories(type);
        currentActInfos =
            (from act in currentTokens
             let name = act["name"]!.ToString()
             let info = new ActInfo(language, storyType, name, act)
             select info
            ).ToList();
        StoriesNames = // Changed StoriesNames to storiesNames (field)
            from info in currentActInfos
            select info.Name;
        SelectedIndex = 0;
    }

    [RelayCommand]
    private async Task LoadMd()
    {
        if (Chapters.Count == 0)
        {
            await LoadChapters();
        }
        var selectedChapters = Chapters.Where(c => c.IsSelected).Select(c => c.ChapterName).ToList();
        if (!selectedChapters.Any())
        {
            ToastManager
                .CreateToast()
                .OfType(NotificationType.Information)
                .WithTitle("选择出错")
                .WithContent("您没有选择任何章节")
                .Dismiss().After(TimeSpan.FromSeconds(7)).Queue();
            return;
        }

        PrepareLoading();
        var content = new AkpStoryLoader(CurrentAct);
        await LoadAllChapters(content, selectedChapters);
        await PreloadResources(content);
        await StartParseDocuments(content);
        await ExportDocuments(content);
        await CompleteLoading();
    }

    private void PrepareLoading()
    {
        IsInitialized = false;
        ClearConsoleOutput();
        noticeBlock.RaiseCommonEvent("初始化加载...");
    }

    private async Task LoadAllChapters(AkpStoryLoader contentLoader, List<string> chapterNames)
    {
        activeTitle = CurrentAct.Tokens["name"]?.ToString();
        await contentLoader.GetAllChapters(chapterNames);
        noticeBlock.RaiseCommonEvent("章节加载完成。");
    }

    private async Task PreloadResources(AkpStoryLoader contentLoader)
    {
        noticeBlock.RaiseCommonEvent("正在预加载资源....");
        if (IsLocalResChecked)
        {
            noticeBlock.RaiseCommonEvent("正在下载资源....");
            await contentLoader.PreloadAssetsForAllChapters();
        }
        else
        {
            await Task.Run(contentLoader.GetPreloadInfo);
        }
    }

    private async Task StartParseDocuments(AkpStoryLoader content)
    {
        noticeBlock.RaiseCommonEvent("正在解析文档....");
        await Task.Run(() => content.ParseAllDocuments(JsonPath)); // Changed jsonPath to JsonPath
    }


    private async Task ExportDocuments(AkpStoryLoader contentLoader)
    {
        noticeBlock.RaiseCommonEvent("正在导出文档....");
        var rawMd = await ExportPlots(contentLoader.ContentTable);
        var rawMdWithTitle = "# " + (activeTitle ?? "") + "\n\n" + rawMd;
        ExportMdAndHtmlFiles(rawMdWithTitle);
        if (IsLocalResChecked)
        {
            AkpProcessor.WriteTyp(outputPathOfCurrentStory, contentLoader);
        }
    }

    private void ExportMdAndHtmlFiles(string mdWithTitle)
    {
        if (!Directory.Exists(outputPathOfCurrentStory)) Directory.CreateDirectory(outputPathOfCurrentStory);
        var rawMarkdown = new Plot(activeTitle ?? "", new System.Text.StringBuilder(mdWithTitle));
        AkpProcessor.WriteMd(outputPathOfCurrentStory, rawMarkdown);
        if (IsLocalResChecked)
        {
            AkpProcessor.WriteHtmlWithLocalRes(outputPathOfCurrentStory, rawMarkdown);
        }
        else
            AkpProcessor.WriteHtml(outputPathOfCurrentStory, rawMarkdown);
    }


    [RelayCommand]
    private async Task PickTagFile()
    {
        var storageProvider = GlobalStorageProvider.StorageProvider;
        var resultFile = await storageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions()
                {
                    Title = "选取json文件",
                    FileTypeFilter = new[] {
                new FilePickerFileType("tag 文件")
                {
                Patterns = new[]{"*.json"}
                }
                }
                }
                );
        if (resultFile is null || resultFile.FirstOrDefault() is null) return;
        else JsonPath = resultFile.FirstOrDefault()!.Path.LocalPath;
    }

    [RelayCommand]
    private async Task PickOutputFolder()
    {
        var storageProvider = GlobalStorageProvider.StorageProvider;
        var resultFolder = await storageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions()
                {
                    Title = "选择输出文件夹",
                }
                );
        if (resultFolder is null || resultFolder.FirstOrDefault() is null) return;
        else OutputPath = resultFolder.FirstOrDefault()!.Path.LocalPath;

    }

    private async Task CompleteLoading()
    {
        var messageBox = MessageBoxManager
            .GetMessageBoxStandard(
                    title: "提示",
                    text: "生成完成。是否打开文件夹？",
                    @enum: ButtonEnum.OkCancel,
                    icon: Icon.Info);

        // 2. 以 Popup 形式展示，并等待用户点击结果
        var result = await messageBox.ShowAsync();

        // 3. 根据返回值执行后续逻辑
        if (result == ButtonResult.Ok)
        {
            OpenOutputFolder();
        }
        IsInitialized = true;
    }

    private void OpenOutputFolder()
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                Arguments = OutputPath, // Changed outputPath to OutputPath
                FileName = "explorer.exe",
                Verb = "runas"
            };
            Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception win32Exception)
        {
            noticeBlock.RaiseCommonEvent(win32Exception.Message); // Changed MessageBox.Show to noticeBlock.RaiseCommonEvent
        }
    }

    public async Task<string> LoadSingleMd()
    {
        List<PlotManager> allPlots;
        var plotsJsonFile = new FileInfo(Path.Combine(Environment.CurrentDirectory, "all_plots.json")); // Changed hardcoded path
        if (!plotsJsonFile.Exists)
        {
            var content = new AkpStoryLoader(currentActInfos[0]); // 假设currentActInfos[0]是合法的参数
            await content.GetAllChapters();
            allPlots = content.ContentTable;
            var plotJson = JsonConvert.SerializeObject(allPlots, Formatting.Indented); // 使用Newtonsoft.Json进行序列化

            // 将序列化的JSON字符串写入文件
            await File.WriteAllTextAsync(plotsJsonFile.FullName, plotJson);
        }
        else
        {
            // 从文件中读取JSON字符串并反序列化
            var plotJson = await File.ReadAllTextAsync(plotsJsonFile.FullName);
            allPlots = JsonConvert.DeserializeObject<List<PlotManager>>(plotJson)!; // 使用Newtonsoft.Json进行反序列化
        }

        var testPlot = allPlots.First();
        var title = testPlot.CurrentPlot.Title;
        return title + "\n" + testPlot.CurrentPlot.Content;
    }

    private void ClearConsoleOutput()
    {
        ConsoleOutput = ""; //先清空这片区域
    }

    private async Task<string> ExportPlots(List<PlotManager> allPlots)
    {
        var output = await Task.Run(() => AkpProcessor.ExportPlots(allPlots));
        return output;
    }


    [RelayCommand]
    private void OpenTagEditor()
    {
        var messenger = WeakReferenceMessenger.Default;
        messenger.Send(new OpenWindowMessage("TagEditor", JsonPath)); // Changed jsonPath to JsonPath
    }

    private void SubscribeCommonNotification()
    {
        noticeBlock.CommonEventHandler += (_, args) => ConsoleOutput += $"\n{args}";
    }



    private void SubscribeNetErrorNotification()
    {
        noticeBlock.NetErrorHappen += (_, args) =>
        {
            var s = $"\n网络错误：{args.Message}，请确认是否连接代理？";
            ConsoleOutput += s;
            // Removed MessageBox.Show(s);
        };
    }

    private void SubscribeLineNoMatchNotification()
    {
        noticeBlock.LineNoMatch += (_, args) =>
        {
            var s = $"\n警告：请检查tags.json中{args.Tag}是否存在？\n出错的句子:" + args.Line;
            ConsoleOutput += s;
        };
    }

    private void SubscribeChapterLoadedNotification()
    {
        noticeBlock.ChapterLoaded += (_, args) =>
        {
            var s = "\n" + args.Title.ToString() + "已加载";
            ConsoleOutput += s;
        };
    }


    public void SelectJsonFile(string path)
    {
        JsonPath = path;
    }

    public void SelectOutputFolder(string path)
    {
        OutputPath = path;
    }

    internal void DropJsonFile(string v)
    {
        JsonPath = v;
    }
}
