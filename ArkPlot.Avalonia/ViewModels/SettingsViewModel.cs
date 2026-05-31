using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ArkPlot.Avalonia.Models;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.PrtsComponents;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArkPlot.Avalonia.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly string _tagsJsonPath;

    // --- Tab 1: 标签替换 ---
    [ObservableProperty] private ObservableCollection<TagReplacementRule> _dataGrid = new();
    [ObservableProperty] private int _selectedIndex;

    // --- Tab 2: 小说化设置 ---
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _systemPromptText = "";
    [ObservableProperty] private string _deepSeekApiKeyText = "";
    [ObservableProperty] private string _bailianApiKeyText = "";
    [ObservableProperty] private string _selectedProvider = "DeepSeek";
    [ObservableProperty] private string _selectedModel = "deepseek-v4-pro";
    [ObservableProperty] private string _saveFeedbackText = "";

    public string[] ProviderOptions => NovelizerSettings.ProviderOptions;
    public string[] ModelOptions => NovelizerSettings.ModelOptions;

    public SettingsViewModel(string tagsJsonPath)
    {
        _tagsJsonPath = tagsJsonPath;
    }

    public SettingsViewModel() : this("tags.json")
    {
    }

    /// <summary>
    /// 窗口加载时调用：同时加载标签和小说化设置
    /// </summary>
    [RelayCommand]
    private void LoadSettings()
    {
        LoadTagJson();
        LoadNovelizerSettings();
    }

    // ==================== Tab 1: 标签替换 ====================

    [RelayCommand]
    private void SaveTagJson()
    {
        var data =
            (from item in DataGrid
             let tag = (item.Tag, item.NewTag)
             let tagReg = ($"{item.Tag}_reg", item.Reg)
             from pair in new[] { tag, tagReg }
             orderby pair.Item1
             select pair)
            .ToDictionary(x => x.Item1, x => x.Item2);
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        var jsonContent = JsonSerializer.Serialize(data, options);
        File.WriteAllText("tags.json", jsonContent);
        LoadTagJson();
    }

    private void LoadTagJson()
    {
        try
        {
            if (!File.Exists(_tagsJsonPath)) return;
            var jsonContent = File.ReadAllText(_tagsJsonPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
            var tagsAndRegs = from pair in data
                              where !pair.Key.EndsWith("_reg")
                              let reg = data![pair.Key + "_reg"]
                              select new TagReplacementRule(pair.Key, reg, pair.Value);
            DataGrid = new ObservableCollection<TagReplacementRule>(tagsAndRegs);
        }
        catch
        {
            // 忽略加载错误
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        var maxNum = FindMaxIndexOfNewItem();
        var newItem = new TagReplacementRule($"NewItem {maxNum + 1}", "", "");
        DataGrid.Insert(0, newItem);
        SelectedIndex = 0;
    }

    [RelayCommand]
    private void CloseWindow(object? window)
    {
        if (window is Window w)
            w.Close();
    }

    [RelayCommand]
    private void RemoveTag(TagReplacementRule tag)
    {
        DataGrid.Remove(tag);
    }

    private int FindMaxIndexOfNewItem()
    {
        var maxItem =
            (from item in DataGrid
             where item.Tag.Contains("NewItem")
             orderby item.Tag descending
             select item.Tag).FirstOrDefault();
        if (maxItem == null) return 0;
        try
        {
            var maxNum = maxItem.Split(" ")[^1];
            return int.Parse(maxNum);
        }
        catch
        {
            return 0;
        }
    }

    // ==================== Tab 2: 小说化设置 ====================

    private void LoadNovelizerSettings()
    {
        var settings = AppSettings.Load();
        var novelizer = settings.Novelizer;

        SystemPromptText = novelizer.SystemPrompt;
        SelectedProvider = novelizer.SelectedProvider;
        SelectedModel = novelizer.SelectedModel;

        // 显示 settings.json 中的值，如为空则显示环境变量值（暗示用户当前生效的）
        DeepSeekApiKeyText = settings.GetApiKey("DeepSeek");
        BailianApiKeyText = settings.GetApiKey("百炼");
    }

    [RelayCommand]
    private void SaveNovelizerSettings()
    {
        var settings = AppSettings.Load();
        var novelizer = settings.Novelizer with
        {
            SystemPrompt = SystemPromptText,
            SelectedProvider = SelectedProvider,
            SelectedModel = SelectedModel,
            ApiKeys = new Dictionary<string, string>
            {
                ["DeepSeek"] = DeepSeekApiKeyText,
                ["百炼"] = BailianApiKeyText
            }
        };
        // Vision 设置由主窗口管理，此处保留原有值
        settings = settings with { Novelizer = novelizer };
        settings.Save();

        // 保存成功反馈
        SaveFeedbackText = "✅ 已保存";
        _ = System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ => SaveFeedbackText = "");
    }

    [RelayCommand]
    private void RestoreDefaultPrompt()
    {
        SystemPromptText = NovelizerSettings.DefaultSystemPrompt;
    }

    // ==================== Tab 3: 数据管理 ====================

    [ObservableProperty] private string _selectedLanguage = "zh_CN";
    [ObservableProperty] private string _dataManagementStatus = "";
    [ObservableProperty] private bool _isDataOperationRunning;

    public string[] LanguageOptions => ["zh_CN", "en_US"];

    [RelayCommand]
    private async Task ForceRefreshData()
    {
        if (IsDataOperationRunning) return;
        IsDataOperationRunning = true;

        try
        {
            var lang = SelectedLanguage;
            DataManagementStatus = $"正在从 GitHub 同步活动列表（{lang}）...";
            var sync = new StorySyncService();
            await sync.DownloadAndSaveAsync(lang);

            DataManagementStatus = "正在从 PRTS Wiki 刷新资源索引...";
            var prts = new PrtsDataProcessor();
            await prts.ForceRefreshAsync(lang);

            DataManagementStatus = "✅ 刷新完成！";
        }
        catch (System.Exception ex)
        {
            DataManagementStatus = $"❌ 刷新失败：{ex.Message}";
        }
        finally
        {
            IsDataOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task ResetAllData()
    {
        if (IsDataOperationRunning) return;
        IsDataOperationRunning = true;

        try
        {
            DataManagementStatus = "正在清空所有数据...";
            var db = DbFactory.GetClient();

            db.Deleteable<FormattedTextEntry>().ExecuteCommand();
            db.Deleteable<Plot>().ExecuteCommand();
            db.Deleteable<StoryChapter>().ExecuteCommand();
            db.Deleteable<Act>().ExecuteCommand();
            db.Deleteable<SyncState>().ExecuteCommand();
            db.Deleteable<PrtsResource>().ExecuteCommand();
            db.Deleteable<PrtsPortraitLink>().ExecuteCommand();
            db.Deleteable<PicDescription>().ExecuteCommand();
            db.Deleteable<PrtsData>().ExecuteCommand();

            DataManagementStatus = "✅ 已清空全部数据。建议重启程序。";
        }
        catch (System.Exception ex)
        {
            DataManagementStatus = $"❌ 清空失败：{ex.Message}";
        }
        finally
        {
            IsDataOperationRunning = false;
        }
    }
}
