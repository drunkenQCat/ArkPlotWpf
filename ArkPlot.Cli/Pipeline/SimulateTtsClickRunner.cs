using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Tts.Alignment;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// simulate-tts-click 命令：模拟点击某角色的某一行，输出立绘和 Gallery 应该收到的输入。
/// 用法: simulate-tts-click &lt;novel.md&gt; &lt;角色名&gt; [点击第几行，默认3]
/// </summary>
public static class SimulateTtsClickRunner
{
    public static async Task RunAsync(string novelPath, string characterName, int clickRow = 3)
    {
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║  模拟点击: 立绘 + Gallery 输入验证       ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine($"小说: {Path.GetFileName(novelPath)}");
        Console.WriteLine($"角色: {characterName}");
        Console.WriteLine($"点击: 第 {clickRow} 行");
        Console.WriteLine();

        // 1. 对齐
        Console.WriteLine("── Step 1: 对齐 ──");
        var aligner = new NovelAligner();
        var (entries, stats) = await aligner.AlignByFileNameAsync(novelPath);
        Console.WriteLine($"  对齐: {stats.AlignedDialogs}/{stats.TotalDialogs} 对话");
        Console.WriteLine();

        // 2. 找该角色的所有片段
        var charSegments = entries
            .Where(e => e.IsDialog && e.CharacterName == characterName)
            .ToList();

        Console.WriteLine($"── Step 2: {characterName} 的片段 ──");
        Console.WriteLine($"  共 {charSegments.Count} 段对话");

        if (charSegments.Count == 0)
        {
            Console.WriteLine($"  ❌ 找不到角色 '{characterName}'");
            return;
        }

        for (int i = 0; i < Math.Min(5, charSegments.Count); i++)
        {
            var seg = charSegments[i];
            var preview = (seg.NovelText ?? "").Length > 40
                ? seg.NovelText![..40] + "..."
                : seg.NovelText;
            Console.WriteLine($"  [{i + 1}] EntryIndex={seg.EntryIndex} \"{preview}\"");
        }
        Console.WriteLine();

        if (clickRow > charSegments.Count)
            clickRow = charSegments.Count;

        var clicked = charSegments[clickRow - 1];
        Console.WriteLine($"── Step 3: 点击第 {clickRow} 行 ──");
        Console.WriteLine($"  EntryIndex = {clicked.EntryIndex}");
        Console.WriteLine($"  CharacterName = {clicked.CharacterName}");
        Console.WriteLine($"  CharacterCode = {clicked.CharacterCode}");
        Console.WriteLine($"  NovelText = {(clicked.NovelText ?? "")[..Math.Min(80, (clicked.NovelText ?? "").Length)]}");
        Console.WriteLine();

        // 3. 模拟立绘加载 (LoadPortraitAsync)
        Console.WriteLine("── PortraitPanel 输入 ──");
        var db = DbFactory.GetClient();

        var entry = await db.Queryable<FormattedTextEntry>()
            .Where(e => e.Index == clicked.EntryIndex)
            .FirstAsync();

        string? portraitUrl = null;
        if (entry != null && entry.Portraits != null && entry.Portraits.Count > 0)
        {
            portraitUrl = entry.Portraits
                .FirstOrDefault(p => !string.IsNullOrEmpty(p) && !p.Contains("transparent.png"));
        }

        Console.WriteLine($"  PortraitSource = {portraitUrl ?? "(null → 灰色占位 #555555)"}");
        Console.WriteLine($"  SpeakerName    = \"{clicked.CharacterName}\"");
        Console.WriteLine($"  HasPortrait    = {(portraitUrl != null ? "true ✅" : "false (显示灰色块)") }");
        Console.WriteLine();

        // 4. 模拟 Gallery 加载 (UpdateGalleryForSegment)
        Console.WriteLine("── GalleryPanel 输入 ──");

        // 加载所有背景图数据
        var allCharSlots = await db.Queryable<FormattedTextEntry>()
            .Where(e => e.Type == "charslot")
            .ToListAsync();

        var bgEntries = allCharSlots
            .Where(e => !string.IsNullOrEmpty(e.Bg))
            .OrderBy(e => e.Index)
            .ToList();

        Console.WriteLine($"  背景图条目总数: {bgEntries.Count}");

        // 找当前 EntryIndex 最近的背景图
        var currentBg = bgEntries
            .Where(e => e.Index <= clicked.EntryIndex)
            .OrderByDescending(e => e.Index)
            .FirstOrDefault();

        var prevBg = bgEntries
            .Where(e => e.Index < (currentBg?.Index ?? 0))
            .OrderByDescending(e => e.Index)
            .FirstOrDefault();

        var nextBg = bgEntries
            .Where(e => e.Index > (currentBg?.Index ?? int.MaxValue))
            .OrderBy(e => e.Index)
            .FirstOrDefault();

        Console.WriteLine($"  CurrentBackground = {currentBg?.Bg ?? "(null → 灰色 #505050)"}");
        Console.WriteLine($"    (charslot Index={currentBg?.Index})");
        Console.WriteLine($"  PrevBackground    = {prevBg?.Bg ?? "(null → 灰色 #383838)"}");
        Console.WriteLine($"    (charslot Index={prevBg?.Index})");
        Console.WriteLine($"  NextBackground    = {nextBg?.Bg ?? "(null → 灰色 #484848)"}");
        Console.WriteLine($"    (charslot Index={nextBg?.Index})");

        // PicDesc
        string picDesc = "";
        if (currentBg != null && !string.IsNullOrEmpty(currentBg.CharacterCode))
        {
            var picDescEntry = await db.Queryable<PicDescription>()
                .Where(p => p.DedupKey == currentBg.CharacterCode)
                .FirstAsync();
            picDesc = picDescEntry?.PicDesc ?? "";
        }
        Console.WriteLine($"  PicDescription    = \"{(picDesc.Length > 60 ? picDesc[..57] + "..." : picDesc)}\"");

        // 上下文
        var nearbyEntries = await db.Queryable<FormattedTextEntry>()
            .Where(e => !string.IsNullOrEmpty(e.Dialog))
            .ToListAsync();

        var upperDialogs = nearbyEntries
            .Where(e => e.Index < clicked.EntryIndex)
            .OrderByDescending(e => e.Index)
            .Take(2)
            .Reverse()
            .Select(e => e.Dialog)
            .ToList();

        var lowerDialogs = nearbyEntries
            .Where(e => e.Index > clicked.EntryIndex)
            .Take(2)
            .Select(e => e.Dialog)
            .ToList();

        Console.WriteLine($"  UpperContext2     = \"{(upperDialogs.Count > 0 ? upperDialogs[0][..Math.Min(40, upperDialogs[0].Length)] : "")}\"");
        Console.WriteLine($"  UpperContext1     = \"{(upperDialogs.Count > 1 ? upperDialogs[1][..Math.Min(40, upperDialogs[1].Length)] : "")}\"");
        Console.WriteLine($"  LowerContext1     = \"{(lowerDialogs.Count > 0 ? lowerDialogs[0][..Math.Min(40, lowerDialogs[0].Length)] : "")}\"");
        Console.WriteLine($"  LowerContext2     = \"{(lowerDialogs.Count > 1 ? lowerDialogs[1][..Math.Min(40, lowerDialogs[1].Length)] : "")}\"");

        Console.WriteLine();
        Console.WriteLine("── 总结 ──");
        Console.WriteLine($"  立绘: {(portraitUrl != null ? "✅ 有真实图片" : "⚠️ 灰色占位")}");
        Console.WriteLine($"  中心背景图: {(currentBg != null ? "✅ 有真实图片" : "⚠️ 灰色占位")}");
        Console.WriteLine($"  左/右裁剪图: {(prevBg != null ? "✅" : "⚠️ 灰")} / {(nextBg != null ? "✅" : "⚠️ 灰")}");
        Console.WriteLine($"  上下文台词: {upperDialogs.Count + lowerDialogs.Count} 条");
    }
}
