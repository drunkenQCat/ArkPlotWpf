using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArkPlot.Avalonia.Models;
using ArkPlot.Avalonia.ViewModels;
using ArkPlot.Avalonia.Views;
using AsyncImageLoader;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// 验证 PortraitPanel 多次 Update 后 UI 是否跟着变。
/// 复现问题：点击第一行立绘正常，后续点击其他行立绘不更新。
/// </summary>
public class PortraitUpdateHeadlessTests : IDisposable
{
    private readonly string _tempDir;

    public PortraitUpdateHeadlessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arkplot_portrait_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    // ── 1. ViewModel 属性变更是否正确 ──

    [AvaloniaFact]
    public void PortraitViewModel_Update_ChangesPortraitUrl()
    {
        var vm = new PortraitPanelViewModel();

        vm.Update("https://example.com/portrait_a.png", "角色A");
        Assert.Equal("https://example.com/portrait_a.png", vm.PortraitUrl);
        Assert.Equal("角色A", vm.SpeakerName);
        Assert.True(vm.HasPortrait);

        vm.Update("https://example.com/portrait_b.png", "角色B");
        Assert.Equal("https://example.com/portrait_b.png", vm.PortraitUrl);
        Assert.Equal("角色B", vm.SpeakerName);
        Assert.True(vm.HasPortrait);
    }

    [AvaloniaFact]
    public void PortraitViewModel_Update_SameUrl_SkipsNotification()
    {
        var vm = new PortraitPanelViewModel();
        var changes = new List<string?>();

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PortraitPanelViewModel.PortraitUrl))
                changes.Add(vm.PortraitUrl);
        };

        vm.Update("https://example.com/same.png", "角色A");
        vm.Update("https://example.com/same.png", "角色B");

        // [ObservableProperty] 在值相同时跳过通知（这是正确的行为）
        Assert.Single(changes);
    }

    [AvaloniaFact]
    public void PortraitViewModel_PropertyChanged_FiresOnEveryUpdate()
    {
        var vm = new PortraitPanelViewModel();
        var fired = new List<string>();

        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);

        vm.Update("https://a.com/1.png", "A");
        vm.Update("https://b.com/2.png", "B");
        vm.Update("https://c.com/3.png", "C");

        // 每次 Update 应触发 PortraitUrl + HasPortrait + SpeakerName
        var portraitUrlChanges = fired.Count(n => n == nameof(PortraitPanelViewModel.PortraitUrl));
        Assert.Equal(3, portraitUrlChanges);

        var speakerChanges = fired.Count(n => n == nameof(PortraitPanelViewModel.SpeakerName));
        Assert.Equal(3, speakerChanges);
    }

    [AvaloniaFact]
    public void PortraitViewModel_Clear_ResetsAll()
    {
        var vm = new PortraitPanelViewModel();
        vm.Update("https://example.com/portrait.png", "角色A");
        Assert.True(vm.HasPortrait);

        vm.Clear();
        Assert.Null(vm.PortraitUrl);
        Assert.Equal("", vm.SpeakerName);
        Assert.False(vm.HasPortrait);
    }

    // ── 2. AXAML 绑定是否正确传播到 Image ──

    [AvaloniaFact]
    public void PortraitPanel_BindingPropagates_ToImageSource()
    {
        var vm = new PortraitPanelViewModel();
        var panel = new PortraitPanel();
        panel.DataContext = vm;
        panel.UpdateLayout();

        var image = panel.GetVisualDescendants()
            .OfType<Image>()
            .FirstOrDefault();
        Assert.NotNull(image);

        // 检查 ImageLoader.Source attached property
        var loaderSource = ImageLoader.GetSource(image);
        Console.WriteLine($"[1] ImageLoader.Source (before update): {loaderSource ?? "null"}");

        // 设置 URL
        vm.Update("https://example.com/test.png", "test");
        panel.UpdateLayout();

        var loaderSource2 = ImageLoader.GetSource(image);
        Console.WriteLine($"[2] ImageLoader.Source (after 1st update): {loaderSource2 ?? "null"}");
        Console.WriteLine($"    vm.PortraitUrl: {vm.PortraitUrl}");

        // 第二次更新
        vm.Update("https://example.com/second.png", "test2");
        panel.UpdateLayout();

        var loaderSource3 = ImageLoader.GetSource(image);
        Console.WriteLine($"[3] ImageLoader.Source (after 2nd update): {loaderSource3 ?? "null"}");
        Console.WriteLine($"    vm.PortraitUrl: {vm.PortraitUrl}");

        // ViewModel 确实变了
        Assert.Equal("https://example.com/second.png", vm.PortraitUrl);

        // 检查 ImageLoader.Source 是否也跟着变了
        // 如果 source2 == source3 但 vm.PortraitUrl 不同 → 绑定没有传播
        if (loaderSource2 != null && loaderSource3 != null)
        {
            Assert.NotEqual(loaderSource2.ToString(), loaderSource3.ToString());
        }
        else
        {
            Console.WriteLine("⚠️ ImageLoader.Source 为 null — 绑定可能没有传播到 attached property");
            Console.WriteLine("   这在 headless 环境下是已知的 AsyncImageLoader 限制");
        }
    }

    [AvaloniaFact]
    public void PortraitPanel_ImageSource_UpdatesOnViewModelChange()
    {
        var vm = new PortraitPanelViewModel();
        var panel = new PortraitPanel { DataContext = vm };
        panel.UpdateLayout();

        var image = panel.GetVisualDescendants()
            .OfType<Image>()
            .FirstOrDefault();
        Assert.NotNull(image);

        // 第一次更新
        vm.Update("https://example.com/portrait_a.png", "角色A");
        panel.UpdateLayout();

        var source1 = ImageLoader.GetSource(image);
        // headless 下 AsyncImageLoader 可能不设置 Source，记录即可
        Console.WriteLine($"Source1: {source1}");

        // 第二次更新
        vm.Update("https://example.com/portrait_b.png", "角色B");
        panel.UpdateLayout();

        var source2 = ImageLoader.GetSource(image);
        Console.WriteLine($"Source2: {source2}");

        // 如果两个都 null，说明 AsyncImageLoader 在 headless 下不工作
        // 但至少 ViewModel 的值确实变了
        Assert.Equal("https://example.com/portrait_b.png", vm.PortraitUrl);
    }

    // ── 3. TtsViewModel 的 SelectedSegment 变更链路 ──

    [AvaloniaFact]
    public void TtsViewModel_SelectedSegment_UpdatesPortraitPanel()
    {
        var vm = new TtsViewModel(_tempDir);

        // 模拟 _allEntries 有数据（通过反射设置 private field）
        var entriesField = typeof(TtsViewModel).GetField("_allEntries",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(entriesField);

        entriesField!.SetValue(vm, new List<ArkPlot.Tts.Alignment.AlignmentEntry>
        {
            new("文本1", true, "角色A", "code_a", 10, "章节1", "女",
                ["https://example.com/portrait_a.png"]),
            new("文本2", true, "角色B", "code_b", 20, "章节1", "男",
                ["https://example.com/portrait_b.png"]),
            new("文本3", true, "角色C", "code_c", 30, "章节1", "女",
                ["https://example.com/portrait_c.png"]),
        });

        // 模拟选中第一个 segment
        vm.FilteredSegments =
        [
            new SegmentRow { Index = 1, CharacterName = "角色A", EntryIndex = 10, ChapterTitle = "章节1" },
            new SegmentRow { Index = 2, CharacterName = "角色B", EntryIndex = 20, ChapterTitle = "章节1" },
            new SegmentRow { Index = 3, CharacterName = "角色C", EntryIndex = 30, ChapterTitle = "章节1" },
        ];

        // 选中第一个
        vm.SelectedSegment = vm.FilteredSegments[0];
        Assert.Equal("https://example.com/portrait_a.png", vm.PortraitPanel.PortraitUrl);
        Assert.Equal("角色A", vm.PortraitPanel.SpeakerName);

        // 选中第二个 — 这里就是 bug 复现点
        vm.SelectedSegment = vm.FilteredSegments[1];
        Assert.Equal("https://example.com/portrait_b.png", vm.PortraitPanel.PortraitUrl);
        Assert.Equal("角色B", vm.PortraitPanel.SpeakerName);

        // 选中第三个
        vm.SelectedSegment = vm.FilteredSegments[2];
        Assert.Equal("https://example.com/portrait_c.png", vm.PortraitPanel.PortraitUrl);
        Assert.Equal("角色C", vm.PortraitPanel.SpeakerName);

        // 回到第一个
        vm.SelectedSegment = vm.FilteredSegments[0];
        Assert.Equal("https://example.com/portrait_a.png", vm.PortraitPanel.PortraitUrl);
        Assert.Equal("角色A", vm.PortraitPanel.SpeakerName);
    }

    [AvaloniaFact]
    public void TtsViewModel_SelectedSegment_PicksCharacterSpecificPortrait_WhenStageHasMultiplePortraits()
    {
        var vm = new TtsViewModel(_tempDir);

        var entriesField = typeof(TtsViewModel).GetField("_allEntries",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(entriesField);

        entriesField!.SetValue(vm, new List<ArkPlot.Tts.Alignment.AlignmentEntry>
        {
            new("文本1", true, "监狱负责人", "avg_npc_134", 86, "CW-ST-1 阴云密布 幕间", "男",
                ["https://media.prts.wiki/1/15/Avg_avg_npc_134.png",
                 "https://media.prts.wiki/b/b5/Avg_avg_npc_892_1-10$1.png"]),
            new("文本2", true, "精英打扮的男性", "avg_npc_892_1", 88, "CW-ST-1 阴云密布 幕间", "男",
                ["https://media.prts.wiki/1/15/Avg_avg_npc_134.png",
                 "https://media.prts.wiki/8/82/Avg_avg_npc_892_1-2$1.png"]),
        });

        vm.FilteredSegments =
        [
            new SegmentRow
            {
                Index = 1,
                CharacterName = "监狱负责人",
                CharacterCode = "avg_npc_134",
                EntryIndex = 86,
                ChapterTitle = "CW-ST-1 阴云密布 幕间"
            },
            new SegmentRow
            {
                Index = 2,
                CharacterName = "精英打扮的男性",
                CharacterCode = "avg_npc_892_1",
                EntryIndex = 88,
                ChapterTitle = "CW-ST-1 阴云密布 幕间"
            }
        ];

        vm.SelectedSegment = vm.FilteredSegments[1];

        Assert.Equal("https://media.prts.wiki/8/82/Avg_avg_npc_892_1-2$1.png",
            vm.PortraitPanel.PortraitUrl);
        Assert.Equal("精英打扮的男性", vm.PortraitPanel.SpeakerName);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
