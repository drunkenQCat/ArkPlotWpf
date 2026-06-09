using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Tts.Alignment;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// verify-tts-component-inputs 命令：自动化验证立绘和 Gallery 组件输入。
/// 用法: verify-tts-component-inputs &lt;novel.md&gt; &lt;角色名&gt;
/// </summary>
public static class VerifyTtsComponentInputsRunner
{
    public static async Task<int> RunAsync(string novelPath, string characterName)
    {
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║  自动化验证: 组件输入 (立绘+Gallery)      ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine($"小说: {Path.GetFileName(novelPath)}");
        Console.WriteLine($"角色: {characterName}");
        Console.WriteLine();

        var passCount = 0;
        var failCount = 0;

        // 1. 对齐
        Console.WriteLine("── Step 1: 对齐 ──");
        var aligner = new NovelAligner();
        var (entries, stats) = await aligner.AlignByFileNameAsync(novelPath);
        Console.WriteLine($"  对齐: {stats.AlignedDialogs}/{stats.TotalDialogs}");

        if (stats.AlignedDialogs == stats.TotalDialogs)
        {
            Console.WriteLine("  ✅ PASS: 全部对齐");
            passCount++;
        }
        else
        {
            Console.WriteLine("  ❌ FAIL: 对齐不完整");
            failCount++;
        }
        Console.WriteLine();

        // 2. 找角色片段
        var charSegments = entries
            .Where(e => e.IsDialog && e.CharacterName == characterName)
            .ToList();

        Console.WriteLine($"── Step 2: {characterName} 片段 ──");
        Console.WriteLine($"  共 {charSegments.Count} 段");

        if (charSegments.Count > 0)
        {
            Console.WriteLine("  ✅ PASS: 找到角色");
            passCount++;
        }
        else
        {
            Console.WriteLine($"  ❌ FAIL: 找不到角色 '{characterName}'");
            failCount++;
            PrintSummary(passCount, failCount);
            return failCount > 0 ? 1 : 0;
        }
        Console.WriteLine();

        // 3. 测试第3行
        var clickRow = Math.Min(3, charSegments.Count);
        var clicked = charSegments[clickRow - 1];

        Console.WriteLine($"── Step 3: 点击第 {clickRow} 行 (EntryIndex={clicked.EntryIndex}) ──");

        var db = DbFactory.GetClient();

        // 3.1 PortraitPanel 输入
        Console.WriteLine("  PortraitPanel:");
        var entry = await db.Queryable<FormattedTextEntry>()
            .Where(e => e.Index == clicked.EntryIndex)
            .FirstAsync();

        string? portraitUrl = null;
        if (entry != null && entry.Portraits != null && entry.Portraits.Count > 0)
        {
            portraitUrl = entry.Portraits
                .FirstOrDefault(p => !string.IsNullOrEmpty(p) && !p.Contains("transparent.png"));
        }

        if (portraitUrl != null)
        {
            Console.WriteLine($"    PortraitSource = {Path.GetFileName(portraitUrl)}");
            Console.WriteLine($"    SpeakerName = \"{clicked.CharacterName}\"");
            Console.WriteLine("    ✅ PASS: 有立绘");
            passCount++;
        }
        else
        {
            Console.WriteLine("    PortraitSource = (null)");
            Console.WriteLine("    ⚠️ WARN: 无立绘（显示灰色占位）");
            // 不算失败，因为有些角色可能没有立绘
        }
        Console.WriteLine();

        // 3.2 GalleryPanel 输入
        Console.WriteLine("  GalleryPanel:");

        var allCharSlots = await db.Queryable<FormattedTextEntry>()
            .Where(e => e.Type == "charslot")
            .ToListAsync();

        var bgEntries = allCharSlots
            .Where(e => !string.IsNullOrEmpty(e.Bg))
            .OrderBy(e => e.Index)
            .ToList();

        var currentBg = bgEntries
            .Where(e => e.Index <= clicked.EntryIndex)
            .OrderByDescending(e => e.Index)
            .FirstOrDefault();

        if (currentBg != null)
        {
            Console.WriteLine($"    CurrentBackground = {Path.GetFileName(currentBg.Bg)}");
            Console.WriteLine("    ✅ PASS: 有中心背景图");
            passCount++;
        }
        else
        {
            Console.WriteLine("    CurrentBackground = (null)");
            Console.WriteLine("    ⚠️ WARN: 无中心背景图（显示灰色占位）");
        }

        var prevBg = bgEntries
            .Where(e => e.Index < (currentBg?.Index ?? 0))
            .OrderByDescending(e => e.Index)
            .FirstOrDefault();

        var nextBg = bgEntries
            .Where(e => e.Index > (currentBg?.Index ?? int.MaxValue))
            .OrderBy(e => e.Index)
            .FirstOrDefault();

        Console.WriteLine($"    PrevBackground = {(prevBg != null ? Path.GetFileName(prevBg.Bg) : "(null)")}");
        Console.WriteLine($"    NextBackground = {(nextBg != null ? Path.GetFileName(nextBg.Bg) : "(null)")}");

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

        var contextCount = upperDialogs.Count + lowerDialogs.Count;
        if (contextCount >= 2)
        {
            Console.WriteLine($"    上下文 = {contextCount} 条");
            Console.WriteLine("    ✅ PASS: 有上下文台词");
            passCount++;
        }
        else
        {
            Console.WriteLine($"    上下文 = {contextCount} 条");
            Console.WriteLine("    ⚠️ WARN: 上下文不足");
        }
        Console.WriteLine();

        // 总结
        PrintSummary(passCount, failCount);
        return failCount > 0 ? 1 : 0;
    }

    private static void PrintSummary(int pass, int fail)
    {
        Console.WriteLine("── 验证结果 ──");
        Console.WriteLine($"  ✅ PASS: {pass}");
        Console.WriteLine($"  ❌ FAIL: {fail}");
        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "🎉 全部通过！组件输入正确。" : "💥 有失败项，请检查。");
    }
}
