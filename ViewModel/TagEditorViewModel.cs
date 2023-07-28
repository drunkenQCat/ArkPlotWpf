using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArkPlotWpf.Model;
using System;

namespace ArkPlotWpf.ViewModel;

public partial class TagEditorViewModel : ObservableObject
{
    [ObservableProperty] 
    private ObservableCollection<TagReg> dataGrid = new();
    [ObservableProperty]
    int selectedIndex = 0;

    private readonly string jsonPath;

    public Action CloseAction { get; internal set; }

    public TagEditorViewModel(string path, Action close)
    {
        jsonPath = path;
        CloseAction = close;
    }

    public TagEditorViewModel()
    {
        jsonPath= "tags.json";
        CloseAction = LoadTagJson;
    }


    [RelayCommand]
    void LoadTagJson()
    {
        var jsonContent = File.ReadAllText(jsonPath);
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
        var tagsAndRegs = from pair in data
            where !pair.Key.EndsWith("_reg")
            let reg = data[pair.Key + "_reg"]
            select new TagReg(pair.Key, reg, pair.Value);
        DataGrid = new(tagsAndRegs);
    }

    [RelayCommand]
    void SaveTagJson()
    {
        var data =
            (from item in dataGrid
                let tag = (item.Tag, item.NewTag)
                let tagReg = ($"{item.Tag}_reg", item.Reg)
                from pair in new[]{tag, tagReg} 
                orderby pair.Item1
                select pair)
            .ToDictionary(x=>x.Item1, x=>x.Item2);
        var jsonContent = JsonSerializer.Serialize(data);
        File.WriteAllText("tags.json", jsonContent);
        CloseAction!();
    }

    [RelayCommand]
    void AddItem()
    {
        var maxNum = FindMaxIndexOfNewItem();
        var newItem = new TagReg($"NewItem {maxNum + 1}", "", "");
        DataGrid.Insert(0, newItem);
        SelectedIndex = 0;
    }

    [RelayCommand]
    void CloseWindow() => CloseAction!();

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