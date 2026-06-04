using System;
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
    [ObservableProperty] private string[] _providerOptions = NovelizerSettings.BuiltInProviders.Keys.ToArray();
    [ObservableProperty] private string[] _modelOptions = NovelizerSettings.BuiltInProviders["DeepSeek"].Models;
    [ObservableProperty] private string _saveFeedbackText = "";

    // 小说化自定义 Provider
    [ObservableProperty] private ObservableCollection<ProviderConfig> _customProviderList = new();
    [ObservableProperty] private string _editingProviderName = "";
    [ObservableProperty] private string _editingProviderUrl = "";
    [ObservableProperty] private string _editingProviderKey = "";
    [ObservableProperty] private string _editingProviderModelsText = "";
    [ObservableProperty] private int _selectedCustomProviderIndex = -1;

    // --- Tab 3: 图片描述设置 ---
    [ObservableProperty] private string _visionSystemPrompt = VisionSettings.DefaultSystemPrompt;
    [ObservableProperty] private string _visionSelectedProvider = "百炼";
    [ObservableProperty] private string _visionSelectedModel = "qwen3-vl-flash";
    [ObservableProperty] private string _visionOllamaBaseUrl = "http://localhost:11434";
    [ObservableProperty] private string[] _visionProviderOptions = VisionSettings.BuiltInModels.Keys.ToArray();
    [ObservableProperty] private string[] _visionModelOptions = VisionSettings.BuiltInModels["百炼"];
    [ObservableProperty] private bool _isOllamaProvider;
    [ObservableProperty] private string _visionSaveFeedbackText = "";

    // 图片描述自定义 Provider
    [ObservableProperty] private ObservableCollection<ProviderConfig> _visionCustomProviderList = new();
    [ObservableProperty] private string _visionEditingProviderName = "";
    [ObservableProperty] private string _visionEditingProviderUrl = "";
    [ObservableProperty] private string _visionEditingProviderKey = "";
    [ObservableProperty] private string _visionEditingProviderModelsText = "";
    [ObservableProperty] private int _visionSelectedCustomProviderIndex = -1;

    public SettingsViewModel(string tagsJsonPath)
    {
        _tagsJsonPath = tagsJsonPath;
    }

    public SettingsViewModel() : this("tags.json")
    {
    }

    [RelayCommand]
    private void LoadSettings()
    {
        LoadTagJson();
        LoadNovelizerSettings();
        LoadVisionSettings();
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

    partial void OnSelectedProviderChanged(string value)
    {
        var settings = AppSettings.Load();
        var novelizer = settings.Novelizer with { CustomProviders = CustomProviderList.ToArray() };
        ModelOptions = novelizer.GetModelsForProvider(value);
        if (ModelOptions.Length > 0 && !ModelOptions.Contains(SelectedModel))
            SelectedModel = ModelOptions[0];
    }

    private void LoadNovelizerSettings()
    {
        var settings = AppSettings.Load();
        var novelizer = settings.Novelizer;

        SystemPromptText = novelizer.SystemPrompt;

        // 自定义 Provider 列表
        CustomProviderList = new ObservableCollection<ProviderConfig>(novelizer.CustomProviders ?? []);
        RefreshNovelizerProviderOptions();

        SelectedProvider = novelizer.SelectedProvider;
        ModelOptions = novelizer.GetModelsForProvider(SelectedProvider);
        SelectedModel = ModelOptions.Contains(novelizer.SelectedModel) ? novelizer.SelectedModel : (ModelOptions.Length > 0 ? ModelOptions[0] : "");

        DeepSeekApiKeyText = settings.GetApiKey("DeepSeek");
        BailianApiKeyText = settings.GetApiKey("百炼");
    }

    private void RefreshNovelizerProviderOptions()
    {
        var settings = AppSettings.Load();
        var novelizer = settings.Novelizer with { CustomProviders = CustomProviderList.ToArray() };
        ProviderOptions = novelizer.AllProviderNames;
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
            },
            CustomProviders = CustomProviderList.ToArray()
        };
        settings = settings with { Novelizer = novelizer };
        settings.Save();

        SaveFeedbackText = "✅ 已保存";
        _ = Task.Delay(2000).ContinueWith(_ => SaveFeedbackText = "");
    }

    [RelayCommand]
    private void RestoreDefaultPrompt()
    {
        SystemPromptText = NovelizerSettings.DefaultSystemPrompt;
    }

    // --- 小说化自定义 Provider CRUD ---

    [RelayCommand]
    private void AddCustomProvider()
    {
        if (string.IsNullOrWhiteSpace(EditingProviderName)) return;
        var models = EditingProviderModelsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var config = new ProviderConfig(EditingProviderName.Trim(), EditingProviderUrl.Trim(), EditingProviderKey.Trim(), models);
        CustomProviderList.Add(config);
        RefreshNovelizerProviderOptions();
        ClearEditingFields();
    }

    [RelayCommand]
    private void UpdateCustomProvider()
    {
        if (SelectedCustomProviderIndex < 0 || SelectedCustomProviderIndex >= CustomProviderList.Count) return;
        var models = EditingProviderModelsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        CustomProviderList[SelectedCustomProviderIndex] = new ProviderConfig(
            EditingProviderName.Trim(), EditingProviderUrl.Trim(), EditingProviderKey.Trim(), models);
        RefreshNovelizerProviderOptions();
    }

    [RelayCommand]
    private void DeleteCustomProvider()
    {
        if (SelectedCustomProviderIndex < 0 || SelectedCustomProviderIndex >= CustomProviderList.Count) return;
        var deletedName = CustomProviderList[SelectedCustomProviderIndex].Name;
        CustomProviderList.RemoveAt(SelectedCustomProviderIndex);
        SelectedCustomProviderIndex = -1;
        // 如果删除的是当前选中的平台，重置为第一个预设平台
        if (SelectedProvider == deletedName)
            SelectedProvider = NovelizerSettings.BuiltInProviders.Keys.First();
        RefreshNovelizerProviderOptions();
        ClearEditingFields();
    }

    partial void OnSelectedCustomProviderIndexChanged(int value)
    {
        if (value >= 0 && value < CustomProviderList.Count)
        {
            var p = CustomProviderList[value];
            EditingProviderName = p.Name;
            EditingProviderUrl = p.BaseUrl;
            EditingProviderKey = p.ApiKey;
            EditingProviderModelsText = string.Join(", ", p.Models);
        }
    }

    private void ClearEditingFields()
    {
        EditingProviderName = "";
        EditingProviderUrl = "";
        EditingProviderKey = "";
        EditingProviderModelsText = "";
    }

    // ==================== Tab 3: 图片描述设置 ====================

    partial void OnVisionSelectedProviderChanged(string value)
    {
        var vision = GetCurrentVisionSettings();
        VisionModelOptions = vision.GetModelsForProvider(value);
        if (VisionModelOptions.Length > 0 && !VisionModelOptions.Contains(VisionSelectedModel))
            VisionSelectedModel = VisionModelOptions[0];
        IsOllamaProvider = value == "Ollama";
    }

    private VisionSettings GetCurrentVisionSettings()
    {
        return new VisionSettings(
            IsPicDescEnabled: false,
            SelectedProvider: VisionSelectedProvider,
            SelectedModel: VisionSelectedModel,
            SystemPrompt: VisionSystemPrompt,
            OllamaBaseUrl: VisionOllamaBaseUrl,
            CustomProviders: VisionCustomProviderList.ToArray()
        );
    }

    private void LoadVisionSettings()
    {
        var settings = AppSettings.Load();
        var vision = settings.Vision ?? VisionSettings.CreateDefaults();

        VisionSystemPrompt = string.IsNullOrEmpty(vision.SystemPrompt) ? VisionSettings.DefaultSystemPrompt : vision.SystemPrompt;
        VisionOllamaBaseUrl = vision.OllamaBaseUrl;

        // 自定义 Provider 列表
        VisionCustomProviderList = new ObservableCollection<ProviderConfig>(vision.CustomProviders ?? []);
        RefreshVisionProviderOptions();

        VisionSelectedProvider = vision.SelectedProvider;
        VisionModelOptions = vision.GetModelsForProvider(VisionSelectedProvider);
        VisionSelectedModel = VisionModelOptions.Contains(vision.SelectedModel) ? vision.SelectedModel : (VisionModelOptions.Length > 0 ? VisionModelOptions[0] : "");
        IsOllamaProvider = VisionSelectedProvider == "Ollama";
    }

    private void RefreshVisionProviderOptions()
    {
        var vision = GetCurrentVisionSettings();
        VisionProviderOptions = vision.AllProviderNames;
    }

    [RelayCommand]
    private void SaveVisionSettings()
    {
        var settings = AppSettings.Load();
        var vision = new VisionSettings(
            IsPicDescEnabled: settings.Vision?.IsPicDescEnabled ?? false,
            SelectedProvider: VisionSelectedProvider,
            SelectedModel: VisionSelectedModel,
            SystemPrompt: VisionSystemPrompt,
            OllamaBaseUrl: VisionOllamaBaseUrl,
            CustomProviders: VisionCustomProviderList.ToArray()
        );
        settings = settings with { Vision = vision };
        settings.Save();

        VisionSaveFeedbackText = "✅ 已保存";
        _ = Task.Delay(2000).ContinueWith(_ => VisionSaveFeedbackText = "");
    }

    [RelayCommand]
    private void RestoreDefaultVisionPrompt()
    {
        VisionSystemPrompt = VisionSettings.DefaultSystemPrompt;
    }

    // --- 图片描述自定义 Provider CRUD ---

    [RelayCommand]
    private void AddVisionCustomProvider()
    {
        if (string.IsNullOrWhiteSpace(VisionEditingProviderName)) return;
        var models = VisionEditingProviderModelsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var config = new ProviderConfig(VisionEditingProviderName.Trim(), VisionEditingProviderUrl.Trim(), VisionEditingProviderKey.Trim(), models);
        VisionCustomProviderList.Add(config);
        RefreshVisionProviderOptions();
        ClearVisionEditingFields();
    }

    [RelayCommand]
    private void UpdateVisionCustomProvider()
    {
        if (VisionSelectedCustomProviderIndex < 0 || VisionSelectedCustomProviderIndex >= VisionCustomProviderList.Count) return;
        var models = VisionEditingProviderModelsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        VisionCustomProviderList[VisionSelectedCustomProviderIndex] = new ProviderConfig(
            VisionEditingProviderName.Trim(), VisionEditingProviderUrl.Trim(), VisionEditingProviderKey.Trim(), models);
        RefreshVisionProviderOptions();
    }

    [RelayCommand]
    private void DeleteVisionCustomProvider()
    {
        if (VisionSelectedCustomProviderIndex < 0 || VisionSelectedCustomProviderIndex >= VisionCustomProviderList.Count) return;
        var deletedName = VisionCustomProviderList[VisionSelectedCustomProviderIndex].Name;
        VisionCustomProviderList.RemoveAt(VisionSelectedCustomProviderIndex);
        VisionSelectedCustomProviderIndex = -1;
        // 如果删除的是当前选中的平台，重置为第一个预设平台
        if (VisionSelectedProvider == deletedName)
            VisionSelectedProvider = VisionSettings.BuiltInModels.Keys.First();
        RefreshVisionProviderOptions();
        ClearVisionEditingFields();
    }

    partial void OnVisionSelectedCustomProviderIndexChanged(int value)
    {
        if (value >= 0 && value < VisionCustomProviderList.Count)
        {
            var p = VisionCustomProviderList[value];
            VisionEditingProviderName = p.Name;
            VisionEditingProviderUrl = p.BaseUrl;
            VisionEditingProviderKey = p.ApiKey;
            VisionEditingProviderModelsText = string.Join(", ", p.Models);
        }
    }

    private void ClearVisionEditingFields()
    {
        VisionEditingProviderName = "";
        VisionEditingProviderUrl = "";
        VisionEditingProviderKey = "";
        VisionEditingProviderModelsText = "";
    }

    // ==================== Tab 4: 数据管理 ====================

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
        catch (Exception ex)
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
        catch (Exception ex)
        {
            DataManagementStatus = $"❌ 清空失败：{ex.Message}";
        }
        finally
        {
            IsDataOperationRunning = false;
        }
    }
}
