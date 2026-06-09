using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// diagnose-tts-assets 命令：诊断立绘和背景图在 DB 中的真实数据。
/// 用法: diagnose-tts-assets <act_name>
/// </summary>
public static class DiagnoseTtsAssetsRunner
{
    public static async Task RunAsync(string actName)
    {
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║     TTS 资源诊断 (立绘 + 背景图)        ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine($"活动名: {actName}");
        Console.WriteLine();

        var db = DbFactory.GetClient();

        // 1. 查找活动
        var act = await db.Queryable<Act>()
            .FirstAsync(a => a.Name == actName && a.Lang == "zh_CN");

        if (act == null)
        {
            Console.WriteLine($"❌ 活动 '{actName}' 未找到");
            return;
        }

        Console.WriteLine($"✅ 找到活动: {act.Name} (ID={act.Id})");

        // 2. 查找所有章节
        var plots = await db.Queryable<Plot>()
            .Where(p => p.ActId == act.Id && p.StoryChapterId > 0)
            .OrderBy(p => p.StoryChapterId)
            .ToListAsync();

        Console.WriteLine($"📚 章节数: {plots.Count}");
        if (plots.Count == 0)
        {
            Console.WriteLine("❌ 无章节数据");
            return;
        }

        var firstPlot = plots[0];
        Console.WriteLine($"🔍 诊断第一章: {firstPlot.Title} (ID={firstPlot.Id})");
        Console.WriteLine();

        // 3. 查询第一章的所有条目
        var entries = await db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == firstPlot.Id)
            .OrderBy(e => e.Index)
            .ToListAsync();

        Console.WriteLine($"📊 条目总数: {entries.Count}");
        Console.WriteLine();

        // 4. 统计各类型
        var typeGroups = entries.GroupBy(e => e.Type).OrderByDescending(g => g.Count());
        Console.WriteLine("── 类型分布 ──");
        foreach (var g in typeGroups)
            Console.WriteLine($"  {g.Key}: {g.Count()}");
        Console.WriteLine();

        // 5. 分析 charslot 条目
        var charSlots = entries.Where(e => e.Type == "charslot").ToList();
        Console.WriteLine($"── charslot 条目: {charSlots.Count} ──");

        int withPortraits = 0;
        int withResourceUrls = 0;
        int withBg = 0;
        int withCommandSetName = 0;

        var portraitUrls = new HashSet<string>();
        var bgUrls = new HashSet<string>();
        var resourceUrlSamples = new List<string>();

        foreach (var cs in charSlots)
        {
            if (cs.Portraits != null && cs.Portraits.Count > 0)
            {
                withPortraits++;
                foreach (var p in cs.Portraits)
                {
                    if (!string.IsNullOrEmpty(p) && !p.Contains("transparent.png"))
                        portraitUrls.Add(p);
                }
            }

            if (cs.ResourceUrls != null && cs.ResourceUrls.Count > 0)
            {
                withResourceUrls++;
                foreach (var r in cs.ResourceUrls)
                {
                    if (!string.IsNullOrEmpty(r) && resourceUrlSamples.Count < 20)
                        resourceUrlSamples.Add($"[{cs.Index}] {r}");
                }
            }

            if (!string.IsNullOrEmpty(cs.Bg))
            {
                withBg++;
                bgUrls.Add(cs.Bg);
            }

            if (cs.CommandSet != null && cs.CommandSet.ContainsKey("name"))
                withCommandSetName++;
        }

        Console.WriteLine($"  有 Portraits: {withPortraits}");
        Console.WriteLine($"  有 ResourceUrls: {withResourceUrls}");
        Console.WriteLine($"  有 Bg: {withBg}");
        Console.WriteLine($"  有 CommandSet[name]: {withCommandSetName}");
        Console.WriteLine();

        // 6. 展示 Portraits 样本
        Console.WriteLine("── Portraits 样本 (非 transparent) ──");
        foreach (var url in portraitUrls.Take(10))
            Console.WriteLine($"  {url}");
        Console.WriteLine($"  (共 {portraitUrls.Count} 个不重复 URL)");
        Console.WriteLine();

        // 7. 展示 ResourceUrls 样本
        Console.WriteLine("── ResourceUrls 样本 ──");
        foreach (var sample in resourceUrlSamples.Take(10))
            Console.WriteLine($"  {sample}");
        Console.WriteLine($"  (共 {withResourceUrls} 条有 ResourceUrls)");
        Console.WriteLine();

        // 8. 展示 Bg 样本
        Console.WriteLine("── Bg 字段样本 ──");
        foreach (var bg in bgUrls.Take(10))
            Console.WriteLine($"  {bg}");
        Console.WriteLine($"  (共 {bgUrls.Count} 个不重复值)");
        Console.WriteLine();

        // 9. 分析 dialog 条目
        var dialogs = entries.Where(e => e.Type == "dialog" || !string.IsNullOrEmpty(e.Dialog)).ToList();
        Console.WriteLine($"── 对话条目: {dialogs.Count} ──");

        int dialogWithPortraits = 0;
        int dialogWithCharacterCode = 0;
        var dialogPortraitUrls = new HashSet<string>();

        foreach (var d in dialogs)
        {
            if (d.Portraits != null && d.Portraits.Count > 0)
            {
                dialogWithPortraits++;
                foreach (var p in d.Portraits)
                {
                    if (!string.IsNullOrEmpty(p) && !p.Contains("transparent.png"))
                        dialogPortraitUrls.Add(p);
                }
            }
            if (!string.IsNullOrEmpty(d.CharacterCode))
                dialogWithCharacterCode++;
        }

        Console.WriteLine($"  有 Portraits: {dialogWithPortraits}");
        Console.WriteLine($"  有 CharacterCode: {dialogWithCharacterCode}");
        Console.WriteLine($"  Portraits URL 数: {dialogPortraitUrls.Count}");

        if (dialogPortraitUrls.Count > 0)
        {
            Console.WriteLine("  样本:");
            foreach (var url in dialogPortraitUrls.Take(5))
                Console.WriteLine($"    {url}");
        }
        Console.WriteLine();

        // 10. 总结
        Console.WriteLine("── 总结 ──");
        Console.WriteLine($"  立绘来源: {(portraitUrls.Count > 0 ? "charslot.Portraits" : "未找到")}");
        Console.WriteLine($"  背景图来源: {(bgUrls.Count > 0 ? "charslot.Bg" : (withResourceUrls > 0 ? "charslot.ResourceUrls" : "未找到"))}");
        Console.WriteLine($"  对话角色立绘: {(dialogPortraitUrls.Count > 0 ? "dialog.Portraits" : "需要从 charslot 关联")}");

        // 11. 建议
        Console.WriteLine();
        Console.WriteLine("── 建议 ──");
        if (portraitUrls.Count > 0)
            Console.WriteLine("  ✅ 立绘: 从 charslot.Portraits 读取 (过滤 transparent.png)");
        else if (dialogPortraitUrls.Count > 0)
            Console.WriteLine("  ✅ 立绘: 从 dialog.Portraits 读取 (过滤 transparent.png)");
        else
            Console.WriteLine("  ❌ 立绘: 未找到有效 URL，检查 DB 填充");

        if (bgUrls.Count > 0)
            Console.WriteLine("  ✅ 背景图: 从 charslot.Bg 读取");
        else if (withResourceUrls > 0)
            Console.WriteLine("  ✅ 背景图: 从 charslot.ResourceUrls 读取");
        else
            Console.WriteLine("  ❌ 背景图: 未找到有效 URL，检查 DB 填充");
    }
}
