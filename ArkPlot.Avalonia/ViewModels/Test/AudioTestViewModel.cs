using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArkPlot.Avalonia.ViewModels.Test;

public partial class AudioTestViewModel : ViewModelBase, IDisposable
{
    private readonly string _mp3Dir;

    [ObservableProperty] private ObservableCollection<string> _mp3Files = [];
    [ObservableProperty] private string? _selectedFile;
    [ObservableProperty] private string _statusText = "选择一个 MP3 文件开始播放";

    public AudioPlayerViewModel Player { get; }

    public AudioTestViewModel() : this(Path.Combine(
        AppContext.BaseDirectory, "output", "水晶箭行动"))
    { }

    public AudioTestViewModel(string mp3Dir, IAudioPlayer? player = null)
    {
        _mp3Dir = mp3Dir;
        Player = new AudioPlayerViewModel(player);
        ScanFiles();
    }

    partial void OnSelectedFileChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        Player.LoadFile(value);
        StatusText = $"已加载: {Path.GetFileName(value)}";
    }

    private void ScanFiles()
    {
        if (!Directory.Exists(_mp3Dir))
        {
            StatusText = $"目录不存在: {_mp3Dir}";
            return;
        }

        var files = Directory.GetFiles(_mp3Dir, "*.mp3")
            .OrderBy(f => f)
            .ToList();

        Mp3Files = new ObservableCollection<string>(files);

        if (files.Count == 0)
            StatusText = "未找到 MP3 文件";
        else
            StatusText = $"找到 {files.Count} 个 MP3 文件";
    }

    public void Dispose()
    {
        Player.Dispose();
    }
}
