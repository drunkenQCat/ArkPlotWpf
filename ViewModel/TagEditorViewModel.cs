using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArkPlotWpf.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArkPlotWpf.ViewModel;

public partial class TagEditorViewModel : ObservableObject
{
    private readonly string jsonPath;

    [ObservableProperty] private ObservableCollection<TagReplacementRule> dataGrid = new();

    [ObservableProperty] private int selectedIndex;

    public TagEditorViewModel(string path, Action close)
    {
        jsonPath = path;
        CloseAction = close;
    }

    public TagEditorViewModel()
    {
        jsonPath = "tags.json";
        CloseAction = LoadTagJson;
    }

    public Action CloseAction { get; internal set; }


    [RelayCommand]
    private void LoadTagJson()
    {
        var jsonContent = File.ReadAllText(jsonPath);
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
        var tagsAndRegs = from pair in data
            where !pair.Key.EndsWith("_reg")
            let reg = data![pair.Key + "_reg"]
            select new TagReplacementRule(pair.Key, reg, pair.Value);
        DataGrid = new ObservableCollection<TagReplacementRule>(tagsAndRegs);
    }

    [RelayCommand]
    private void SaveTagJson()
    {
        var data =
            (from item in dataGrid
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
        CloseAction();
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
    private void CloseWindow()
    {
        CloseAction();
    }

    private int FindMaxIndexOfNewItem()
    {
        var maxItem =
            (from item in dataGrid
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
}