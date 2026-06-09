using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArkPlot.Avalonia.Models;

/// <summary>小说文件选择项。</summary>
public partial class NovelFileItem : ObservableObject
{
    public string FilePath { get; }
    public string FileName { get; }

    [ObservableProperty] private bool _isSelected;

    public NovelFileItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }
}

/// <summary>音色配置项（per-character）。</summary>
public partial class VoiceConfigItem : ObservableObject
{
    public string CharacterName { get; }
    public string Gender { get; }
    public string? CharacterCode { get; }

    [ObservableProperty] private string _selectedVoice;

    public List<string> AvailableVoices { get; }

    public VoiceConfigItem(string characterName, string gender, string selectedVoice,
        List<string> availableVoices, string? characterCode = null)
    {
        CharacterName = characterName;
        Gender = gender;
        CharacterCode = characterCode;
        _selectedVoice = selectedVoice;
        AvailableVoices = availableVoices;
    }
}

/// <summary>章节项。</summary>
public partial class ChapterItem : ObservableObject
{
    public string Title { get; }
    public int Index { get; }
    public string DisplayText => $"{Index + 1}. {Title}";

    public ChapterItem(string title, int index)
    {
        Title = title;
        Index = index;
    }
}

/// <summary>片段行（表格中的一行）。</summary>
public partial class SegmentRow : ObservableObject
{
    public int Index { get; set; }
    public string CharacterName { get; set; } = "";
    public string SegmentType { get; set; } = "";
    public string NovelText { get; set; } = "";
    public string? CharacterCode { get; set; }
    public string? Gender { get; set; }
    public string ChapterTitle { get; set; } = "";

    /// <summary>对应的 FormattedTextEntry.Index，用于 Gallery 联动。</summary>
    public int EntryIndex { get; set; } = -1;

    [ObservableProperty] private bool _hasAudio;
    [ObservableProperty] private string _audioFilePath = "";
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private double _audioOpacity = 0.3;
    [ObservableProperty] private string _audioStatus = "— — — — —";
    [ObservableProperty] private bool _isPlaying;

    [RelayCommand]
    public void PlaySingle()
    {
        // 单段播放由 ViewModel 处理
    }
}

/// <summary>背景图项（Gallery 用）。</summary>
public record BackgroundItem(
    string ImageUrl,
    string? PicDescription,
    int EntryIndex,
    List<string> ContextDialogs
);
