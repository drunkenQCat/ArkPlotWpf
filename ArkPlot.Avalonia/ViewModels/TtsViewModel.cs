using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArkPlot.Avalonia.Models;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Tts;
using ArkPlot.Tts.Alignment;
using ArkPlot.Tts.Engines;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SqlSugar;

namespace ArkPlot.Avalonia.ViewModels;

public partial class TtsViewModel : ViewModelBase
{
    // ── 小说文件 ──
    [ObservableProperty] private ObservableCollection<NovelFileItem> _novelFiles = [];
    [ObservableProperty] private string _ttsOutputDir = "";

    // ── 音色配置 ──
    [ObservableProperty] private ObservableCollection<VoiceConfigItem> _voiceConfigs = [];

    // ── 章节 ──
    [ObservableProperty] private ObservableCollection<ChapterItem> _chapters = [];
    [ObservableProperty] private ChapterItem? _selectedChapter;
    [ObservableProperty] private string _searchText = "";

    // ── 片段表格 ──
    [ObservableProperty] private ObservableCollection<SegmentRow> _filteredSegments = [];
    [ObservableProperty] private SegmentRow? _selectedSegment;

    // ── 状态 ──
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _isPlaying;

    /// <summary>播放按钮文本（跟随 IsPlaying 状态切换）。</summary>
    public string PlayButtonText => IsPlaying ? "⏸ 暂停" : "▶ 从选中行开始连播";

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayButtonText));

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _progressText = "就绪";
    [ObservableProperty] private string _totalProgressText = "";

    // ── 立绘 ──
    [ObservableProperty] private string? _currentPortrait;
    [ObservableProperty] private string _currentSpeaker = "";

    // ── Gallery ──
    [ObservableProperty] private string? _currentBackground;
    [ObservableProperty] private string? _prevBackground;
    [ObservableProperty] private string? _nextBackground;
    [ObservableProperty] private string _currentPicDescription = "";
    [ObservableProperty] private string _upperContext1 = "";
    [ObservableProperty] private string _upperContext2 = "";
    [ObservableProperty] private string _lowerContext1 = "";
    [ObservableProperty] private string _lowerContext2 = "";

    // ── 日志 ──
    [ObservableProperty] private string _logText = "";

    // ── 内部 ──
    private CancellationTokenSource? _generateCts;
    private CancellationTokenSource? _playCts;
    private List<AlignmentEntry> _allEntries = [];
    private List<BackgroundItem> _backgrounds = [];
    private readonly VoiceManager _voiceManager = new();
    private readonly string _outputBaseDir;

    public TtsViewModel(string outputBaseDir)
    {
        _outputBaseDir = outputBaseDir;
        TtsOutputDir = Path.Combine(outputBaseDir, "tts");

        ScanNovelFiles();
    }

    // ════════════════════════════════════════════
    // 小说文件扫描
    // ════════════════════════════════════════════

    private void ScanNovelFiles()
    {
        if (!Directory.Exists(_outputBaseDir)) return;

        var files = Directory.GetFiles(_outputBaseDir, "*_novel_*.md");
        NovelFiles = new ObservableCollection<NovelFileItem>(
            files.Select(f => new NovelFileItem(f)));
    }

    // ════════════════════════════════════════════
    // 对齐 + 加载
    // ════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadAlignmentAsync()
    {
        var selectedFiles = NovelFiles.Where(f => f.IsSelected).ToList();
        if (selectedFiles.Count == 0) return;

        Log("正在对齐小说文件...");

        try
        {
            _allEntries = [];
            _backgrounds = [];
            var allChapters = new List<ChapterItem>();

            foreach (var file in selectedFiles)
            {
                var aligner = new NovelAligner();
                var (entries, stats) = await aligner.AlignByFileNameAsync(file.FilePath);

                Log($"{Path.GetFileName(file.FilePath)}: " +
                    $"{stats.AlignedDialogs}/{stats.TotalDialogs} 对话已对齐");

                _allEntries.AddRange(entries);

                // 提取章节
                var chapterTitles = entries
                    .Select(e => e.ChapterTitle)
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .ToList();

                foreach (var title in chapterTitles)
                {
                    if (!allChapters.Any(c => c.Title == title))
                        allChapters.Add(new ChapterItem(title, allChapters.Count));
                }

                // 提取背景图
                await LoadBackgroundsAsync(entries);
            }

            Chapters = new ObservableCollection<ChapterItem>(allChapters);
            if (Chapters.Count > 0)
            {
                SelectedChapter = Chapters[0];
                LoadSegmentsForChapter();
            }

            // 填充音色配置
            BuildVoiceConfigs();

            Log($"加载完成: {allChapters.Count} 章节, {_allEntries.Count} 片段");
        }
        catch (Exception ex)
        {
            Log($"❌ 对齐失败: {ex.Message}");
        }
    }

    private async Task LoadBackgroundsAsync(List<AlignmentEntry> entries)
    {
        try
        {
            var db = DbFactory.GetClient();
            var picDescs = await db.Queryable<PicDescription>().ToListAsync();
            var picDescMap = picDescs.ToDictionary(p => p.DedupKey ?? "", p => p.PicDesc ?? "");

            // 从 entries 的 CharacterCode 找到对应的背景图
            // 背景图在 FormattedTextEntry 的 ResourceUrls 中
            // 简化：从 PicDescription 中找 bg_ 开头的
            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.CharacterCode)))
            {
                var code = entry.CharacterCode!;
                if (picDescMap.TryGetValue(code, out var desc) && !string.IsNullOrEmpty(desc))
                {
                    // 尝试从 PrtsResource 获取图片 URL
                    var resource = await db.Queryable<PrtsResource>()
                        .Where(r => r.ResourceKey == code)
                        .FirstAsync();

                    if (resource != null && !string.IsNullOrEmpty(resource.ResourceUrl))
                    {
                        _backgrounds.Add(new BackgroundItem(
                            resource.ResourceUrl, desc, entry.EntryIndex, []));
                    }
                }
            }
        }
        catch
        {
            // 背景加载失败不影响核心功能
        }
    }

    private void BuildVoiceConfigs()
    {
        var configs = new List<VoiceConfigItem>();

        // 旁白
        var narratorVoice = _voiceManager.GetNarratorVoice();
        configs.Add(new VoiceConfigItem("(旁白)", "—", narratorVoice,
            [narratorVoice], null));

        // 收集所有角色
        var characters = _allEntries
            .Where(e => e.IsDialog && !string.IsNullOrEmpty(e.CharacterName))
            .GroupBy(e => e.CharacterName!)
            .Select(g => new
            {
                Name = g.Key,
                Gender = g.First().Gender ?? "?",
                Code = g.First().CharacterCode
            })
            .OrderByDescending(c =>
                _allEntries.Count(e => e.CharacterName == c.Name))
            .ToList();

        var allVoices = VoicePool.Female.Concat(VoicePool.Male)
            .Append(VoicePool.Narrator).ToList();

        foreach (var ch in characters)
        {
            var voice = _voiceManager.GetVoiceForCharacter(ch.Name, ch.Gender);
            configs.Add(new VoiceConfigItem(ch.Name, ch.Gender, voice, allVoices, ch.Code));
        }

        VoiceConfigs = new ObservableCollection<VoiceConfigItem>(configs);
    }

    // ════════════════════════════════════════════
    // 章节 + 搜索
    // ════════════════════════════════════════════

    partial void OnSelectedChapterChanged(ChapterItem? value)
    {
        LoadSegmentsForChapter();
    }

    partial void OnSearchTextChanged(string value)
    {
        LoadSegmentsForChapter();
    }

    private void LoadSegmentsForChapter()
    {
        if (SelectedChapter == null) return;

        var entries = _allEntries
            .Where(e => e.ChapterTitle == SelectedChapter.Title);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            entries = entries.Where(e =>
                (e.NovelText?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.CharacterName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var rows = entries.Select((e, i) => new SegmentRow
        {
            Index = i + 1,
            CharacterName = e.IsDialog ? (e.CharacterName ?? "?") : "(旁白)",
            SegmentType = e.IsDialog ? "对话" : "旁白",
            NovelText = e.NovelText ?? "",
            CharacterCode = e.CharacterCode,
            Gender = e.Gender,
            ChapterTitle = e.ChapterTitle,
            EntryIndex = e.EntryIndex,
            HasAudio = false,
            AudioOpacity = 0.3,
            AudioStatus = "— — — — —"
        }).ToList();

        FilteredSegments = new ObservableCollection<SegmentRow>(rows);
    }

    [RelayCommand]
    private void PrevChapter()
    {
        if (SelectedChapter == null || Chapters.Count == 0) return;
        var idx = SelectedChapter.Index;
        if (idx > 0) SelectedChapter = Chapters[idx - 1];
    }

    [RelayCommand]
    private void NextChapter()
    {
        if (SelectedChapter == null || Chapters.Count == 0) return;
        var idx = SelectedChapter.Index;
        if (idx < Chapters.Count - 1) SelectedChapter = Chapters[idx + 1];
    }

    // ════════════════════════════════════════════
    // TTS 生成
    // ════════════════════════════════════════════

    [RelayCommand]
    private async Task StartGenerateAsync()
    {
        var selectedFiles = NovelFiles.Where(f => f.IsSelected).Select(f => f.FilePath).ToList();
        if (selectedFiles.Count == 0)
        {
            Log("⚠️ 请选择至少一个小说文件");
            return;
        }

        if (_allEntries.Count == 0)
            await LoadAlignmentAsync();

        IsGenerating = true;
        _generateCts = new CancellationTokenSource();
        var ct = _generateCts.Token;

        try
        {
            Directory.CreateDirectory(TtsOutputDir);
            var cacheDir = Path.Combine(TtsOutputDir, "_tts_cache");

            var engine = new EdgeTtsEngine();
            var cache = new TtsCacheService(cacheDir);

            using var pipeline = new TtsPipeline(engine, _voiceManager, cache);

            foreach (var filePath in selectedFiles)
            {
                ct.ThrowIfCancellationRequested();

                var request = new TtsRequest(
                    TtsInputMode.NovelChapter,
                    filePath,
                    TtsOutputDir,
                    RequestDelayMs: 1000);

                Log($"🎵 开始生成: {Path.GetFileName(filePath)}");

                var progress = new Progress<string>(msg =>
                {
                    Log(msg);
                    // 简单解析进度
                    if (msg.Contains("完成"))
                        ProgressValue = 100;
                });

                var result = await pipeline.GenerateAsync(request, ct, progress);

                Log($"✅ 完成: {result.OutputFiles.Count} 个文件, {result.TotalSegments} 个片段");

                // 标记已生成的片段
                foreach (var seg in FilteredSegments)
                {
                    var mp3 = result.OutputFiles.FirstOrDefault(f =>
                        f.Contains(sanitized(seg.ChapterTitle)));
                    if (mp3 != null)
                    {
                        seg.HasAudio = true;
                        seg.AudioFilePath = mp3;
                        seg.AudioOpacity = 1.0;
                        seg.AudioStatus = "▂▃▅▆▇▅▃";
                    }
                }

                ProgressValue = 100;
                TotalProgressText = $"已生成 {result.OutputFiles.Count} 个章节 MP3";
            }
        }
        catch (OperationCanceledException)
        {
            Log("⚠️ 生成已取消");
        }
        catch (Exception ex)
        {
            Log($"❌ 生成失败: {ex.Message}");
        }
        finally
        {
            IsGenerating = false;
            _generateCts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _generateCts?.Cancel();
    }

    // ════════════════════════════════════════════
    // 播放
    // ════════════════════════════════════════════

    [RelayCommand]
    private async Task PlayFromSelectedAsync()
    {
        // 如果正在播放，暂停
        if (IsPlaying)
        {
            _playCts?.Cancel();
            IsPlaying = false;
            return;
        }

        if (SelectedSegment == null)
        {
            Log("⚠️ 请先选择一行");
            return;
        }

        var startIdx = SelectedSegment.Index;
        var segments = FilteredSegments.Where(s => s.Index >= startIdx && s.HasAudio).ToList();

        if (segments.Count == 0)
        {
            Log("⚠️ 从选中位置开始没有已生成的音频");
            return;
        }

        IsPlaying = true;
        _playCts = new CancellationTokenSource();
        var ct = _playCts.Token;

        try
        {
            foreach (var seg in segments)
            {
                ct.ThrowIfCancellationRequested();

                seg.IsPlaying = true;
                CurrentSpeaker = seg.CharacterName;
                SelectedSegment = seg;

                // 更新 Gallery
                UpdateGalleryForSegment(seg);

                // 播放音频
                await PlayAudioFile(seg.AudioFilePath, ct);

                seg.IsPlaying = false;
            }
        }
        catch (OperationCanceledException)
        {
            // 停止
        }
        finally
        {
            IsPlaying = false;
            foreach (var seg in FilteredSegments)
                seg.IsPlaying = false;
            _playCts = null;
        }
    }

    [RelayCommand]
    private void StopPlay()
    {
        _playCts?.Cancel();
    }

    private async Task PlayAudioFile(string filePath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        try
        {
            // 使用系统默认播放器
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process != null)
            {
                // 等待播放完成或取消
                // 简化：估算时长后等待
                var fileInfo = new FileInfo(filePath);
                var estimatedMs = (int)(fileInfo.Length / 2000); // 粗略估算
                estimatedMs = Math.Max(estimatedMs, 1000);
                estimatedMs = Math.Min(estimatedMs, 30000);

                await Task.Delay(estimatedMs, ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* 播放失败跳过 */ }
    }

    private void UpdateGalleryForSegment(SegmentRow seg)
    {
        if (_backgrounds.Count == 0) return;

        // 找到当前片段最近的背景图
        var bg = _backgrounds
            .Where(b => b.EntryIndex <= seg.EntryIndex)
            .OrderByDescending(b => b.EntryIndex)
            .FirstOrDefault();

        if (bg == null) bg = _backgrounds.FirstOrDefault();
        if (bg == null) return;

        var bgIdx = _backgrounds.IndexOf(bg);

        // 只在背景图切换时更新 Gallery
        if (CurrentBackground != bg.ImageUrl)
        {
            CurrentBackground = bg.ImageUrl;
            CurrentPicDescription = bg.PicDescription ?? "";
            PrevBackground = bgIdx > 0 ? _backgrounds[bgIdx - 1].ImageUrl : null;
            NextBackground = bgIdx < _backgrounds.Count - 1 ? _backgrounds[bgIdx + 1].ImageUrl : null;

            // 更新上下文
            UpdateContextTexts(bg);
        }
    }

    private void UpdateContextTexts(BackgroundItem bg)
    {
        // 从 FormattedTextEntry 获取上下文
        var nearbyEntries = _allEntries
            .Where(e => e.EntryIndex >= 0)
            .OrderBy(e => e.EntryIndex)
            .ToList();

        var upper = nearbyEntries
            .Where(e => e.EntryIndex < bg.EntryIndex && e.IsDialog)
            .OrderByDescending(e => e.EntryIndex)
            .Take(2)
            .Reverse()
            .Select(e => e.NovelText ?? "")
            .ToList();

        var lower = nearbyEntries
            .Where(e => e.EntryIndex > bg.EntryIndex && e.IsDialog)
            .Take(2)
            .Select(e => e.NovelText ?? "")
            .ToList();

        UpperContext2 = upper.Count > 1 ? upper[0] : "";
        UpperContext1 = upper.Count > 0 ? upper[^1] : "";
        LowerContext1 = lower.Count > 0 ? lower[0] : "";
        LowerContext2 = lower.Count > 1 ? lower[1] : "";
    }

    // ════════════════════════════════════════════
    // 导出
    // ════════════════════════════════════════════

    [RelayCommand]
    private void ExportCurrentChapter()
    {
        if (SelectedChapter == null) return;

        var pattern = $"*{sanitized(SelectedChapter.Title)}*.mp3";
        var files = Directory.Exists(TtsOutputDir)
            ? Directory.GetFiles(TtsOutputDir, pattern)
            : [];

        if (files.Length == 0)
        {
            Log("⚠️ 当前章节尚无已生成的 MP3");
            return;
        }

        Log($"📥 当前章节: {files.Length} 个 MP3 → {TtsOutputDir}");
    }

    [RelayCommand]
    private void ExportAllChapters()
    {
        var files = Directory.Exists(TtsOutputDir)
            ? Directory.GetFiles(TtsOutputDir, "*.mp3")
            : [];

        if (files.Length == 0)
        {
            Log("⚠️ 尚无已生成的 MP3");
            return;
        }

        Log($"📥 全部章节: {files.Length} 个 MP3 → {TtsOutputDir}");
    }

    // ════════════════════════════════════════════
    // 辅助
    // ════════════════════════════════════════════

    private void Log(string msg)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
    }

    private static string sanitized(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(text.Where(c => !invalid.Contains(c)));
    }
}
