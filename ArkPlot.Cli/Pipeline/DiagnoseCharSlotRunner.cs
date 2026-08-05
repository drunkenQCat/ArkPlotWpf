using System.IO;
using ArkPlot.Core.Model;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using ArkPlot.Arknights;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// diagnose-charslot 命令：诊断 charslot 立绘分配与图片描述填充。
/// 直接连数据库，复现 PropagateCharacterCodeAndFacts 的 pendingCode 演变，
/// 定位"NPC 描述被安到对话角色头上"与"对话行完全无图片描述"两类问题。
/// 用法: diagnose-charslot <章节标题关键字> [--db <db_path>]
/// </summary>
public static class DiagnoseCharSlotRunner
{
    public static async Task RunAsync(string chapterKeyword, string? dbPath = null)
    {
        if (dbPath != null)
        {
            if (!File.Exists(dbPath))
            {
                Console.Error.WriteLine($"❌ DB 文件不存在: {dbPath}");
                return;
            }
            DbFactory.ConfigureForTesting($"Data Source={dbPath}");
        }
        else
        {
            var defaultDb = @"C:\TechProjects\About_MyRepos\ArkPlot\ArkPlot.Avalonia\bin\Debug\net9.0\arkplot.db";
            if (!File.Exists(defaultDb))
            {
                Console.Error.WriteLine($"❌ 默认 DB 不存在: {defaultDb}，请用 --db 指定");
                return;
            }
            DbFactory.ConfigureForTesting($"Data Source={defaultDb}");
        }

        var db = DbFactory.GetClient();

        var plot = db.Queryable<Plot>()
            .Where(p => p.Title.Contains(chapterKeyword))
            .First();
        if (plot == null)
        {
            Console.Error.WriteLine($"❌ 未找到标题含 [{chapterKeyword}] 的章节");
            Console.WriteLine("数据库中的章节列表（PlotId | Title | Status）：");
            var plots = db.Queryable<Plot>().OrderBy(p => p.Id).Take(200).ToList();
            foreach (var p in plots)
                Console.WriteLine($"  {p.Id,4} | {p.Title} | Status={p.Status}");
            Console.WriteLine();
            SearchEntries(db, chapterKeyword);
            return;
        }

        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plot.Id)
            .OrderBy(e => e.Index)
            .ToList();

        // 直接 dump 指定行的完整存储字段（定位洛伦茨被替换的真实存储值）
        DumpRawEntries(db, plot.Id, entries, chapterKeyword);

        Console.WriteLine("╔═════════════════════════════════════════════════════╗");
        Console.WriteLine("║            Charslot / 图片描述 诊断                ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════╝");
        Console.WriteLine($"章节: {plot.Title} (PlotId={plot.Id}, 条目 {entries.Count} 条)");
        Console.WriteLine();

        await DiagnosePropagation(entries);
        Console.WriteLine();
        DiagnoseEnrichment(entries);
        Console.WriteLine();
        await DiagnoseEnrichRepro(plot, entries);
    }

    /// <summary>
    /// 复现 PicDescEnricher + PropagateCharacterCodeAndFacts，看 aphris 的 charslot 和洛伦茨对话行最终 PicFacts。
    /// 依赖 DB 中已有的 PicDescriptions 缓存（命中则不触发网络）。
    /// </summary>
    private static async Task DiagnoseEnrichRepro(Plot plot, List<FormattedTextEntry> entries)
    {
        Console.WriteLine("── 复现 PicDescEnricher + PropagateCharacterCodeAndFacts ──");
        Console.WriteLine("(用 DB 中已有 PicDescriptions 缓存，命中则不触发网络)");
        Console.WriteLine();

        // 用默认 DB（诊断已 ConfigureForTesting 指向目标库）
        using var picDesc = new PicDescService();
        var pm = new ArkPlot.Arknights.TagProcessing.PlotManager(plot);
        pm.CurrentPlot.TextVariants = entries.Cast<ScriptLine>().ToList();

        await ArkPlot.Arknights.Workflow.AkpProcessor.ExportPlotsAsync(
            new List<ArkPlot.Arknights.TagProcessing.PlotManager> { pm },
            picDescService: picDesc,
            enableDescriptions: true,
            outputMode: OutputMode.PromptOptimized);

        // 打印 aphris 相关条目填充后的状态
        var aphrisRows = entries
            .Where(e => e.OriginalText.Contains("aphris") || e.CharacterName == "洛伦茨")
            .Take(30)
            .ToList();
        foreach (var e in aphrisRows)
        {
            var isPortrait = e.Type is "character" or "charactercutin" or "charslot";
            Console.WriteLine($"Idx={e.Index,4} | Type={e.Type,-12} | name={e.CharacterName,-8} | " +
                $"code={e.CharacterCode ?? "(null)",-18} | " +
                $"PicFacts={(string.IsNullOrEmpty(e.PicFacts) ? "(空!)" : e.PicFacts.Length + "字符")}");
            if (!string.IsNullOrEmpty(e.PicFacts))
                Console.WriteLine($"    PicFacts 前60: {Truncate(e.PicFacts, 60)}");
        }
    }

    // 直接 dump 指定范围的原始存储字段，看洛伦茨那行实际被填了谁
    private static void DumpRawEntries(
        SqlSugar.ISqlSugarClient db, long plotId, List<FormattedTextEntry> entries, string keyword)
    {
        // 找到含洛伦茨或 aphris 或 2321 的行索引范围
        var hits = entries
            .Select((e, i) => (e, i))
            .Where(x => x.e.CharacterName.Contains(keyword)
                || x.e.OriginalText.Contains("aphris")
                || x.e.OriginalText.Contains("2321")
                || x.e.OriginalText.Contains("洛伦茨"))
            .ToList();

        if (hits.Count == 0) return;

        var minIdx = hits.Min(x => x.i);
        var maxIdx = hits.Max(x => x.i);
        var start = Math.Max(0, minIdx - 2);
        var end = Math.Min(entries.Count - 1, maxIdx + 2);

        Console.WriteLine($"── 原始存储 dump（Idx范围 {start}~{end}，含洛伦茨/aphris/2321）──");
        Console.WriteLine("(CharacterCode 为 null 表示是解析前中间态，运行时才由 PrtsPreloader 计算)");
        Console.WriteLine();
        for (var i = start; i <= end; i++)
        {
            var e = entries[i];
            Console.WriteLine($"Idx={e.Index,4} | Type={e.Type,-12} | name={e.CharacterName,-10} | " +
                $"code={e.CharacterCode ?? "(null)",-16} | PicFacts={(string.IsNullOrEmpty(e.PicFacts) ? "(空)" : e.PicFacts.Length + "字符")}");
            Console.WriteLine($"    ResourceUrls.Count={e.ResourceUrls.Count} | Portraits={string.Join(",", e.Portraits ?? new())} | Focus={e.PortraitFocus}");
            Console.WriteLine($"    原文: {Truncate(e.OriginalText, 90)}");
        }
        Console.WriteLine();
    }

    // 在全部 FormattedTextEntry 中按原文/角色名搜索关键词，定位数据所在章节
    private static void SearchEntries(SqlSugar.ISqlSugarClient db, string keyword)
    {
        Console.WriteLine($"── 在全部 FormattedTextEntry 中搜索 [{keyword}] ──");
        var hits = db.Queryable<FormattedTextEntry>()
            .Where(e => e.OriginalText.Contains(keyword) || e.CharacterName.Contains(keyword))
            .Take(30)
            .ToList();
        if (hits.Count == 0)
        {
            Console.WriteLine("  ❌ 未在 FormattedTextEntry 中找到匹配。数据库中可能没有该角色的数据。");
            return;
        }
        foreach (var e in hits)
        {
            var plot = db.Queryable<Plot>().Where(p => p.Id == e.PlotId).First();
            Console.WriteLine($"  PlotId={e.PlotId} ({(plot?.Title ?? "?")}) | Idx={e.Index} | Type={e.Type} | name={e.CharacterName}");
            Console.WriteLine($"     原文: {Truncate(e.OriginalText, 80)}");
        }
    }

    // ──── Bug 1: PropagateCharacterCodeAndFacts 的 pendingCode 演变 ────
    private static Task DiagnosePropagation(List<FormattedTextEntry> entries)
    {
        var nameToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var codeToFacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? pendingCode = null;
        string? pendingFacts = null;

        Console.WriteLine("── 复现 PropagateCharacterCodeAndFacts 的 pendingCode 演变 ──");
        Console.WriteLine("(⚠ 复现从 DB 原始数据开始，未含 PicDescEnricher 填充；含 focus=画外音处理)");
        Console.WriteLine();

        var hasLiveFocus = true;
        foreach (var entry in entries)
        {
            var isPortrait = entry.Type is "character" or "charactercutin" or "charslot";
            var hasCode = !string.IsNullOrEmpty(entry.CharacterCode);
            var hasName = !string.IsNullOrEmpty(entry.CharacterName);

            string marker = "";
            if (isPortrait && hasCode)
            {
                // 与真实逻辑一致：focus="none" 的画外音立绘不提供说话者归属，清空 pending。
                hasLiveFocus = !IsFocusNoneDiag(entry);
                if (hasLiveFocus)
                {
                    pendingCode = entry.CharacterCode;
                    if (!string.IsNullOrEmpty(entry.PicFacts))
                        pendingFacts = entry.PicFacts;
                    marker = "◀ 立绘（聚焦）：更新 pendingCode";
                }
                else
                {
                    pendingCode = null;
                    pendingFacts = null;
                    marker = "◀ 立绘（画外音 focus=none）：清空 pending";
                }
            }
            else if (hasName && string.IsNullOrEmpty(entry.CharacterCode))
            {
                marker = ApplyPendingCodeDiag(entry, nameToCode, codeToFacts, ref pendingCode, ref pendingFacts, hasLiveFocus);
            }

            // 只打印：charslot/character 立绘行 + 有对话名的行 + 有 pending 命中影响的
            if (isPortrait || hasName)
            {
                Console.WriteLine($"Idx={entry.Index,4} | Type={entry.Type,-15} | " +
                    $"name={entry.CharacterName ?? "",-12} | code={entry.CharacterCode ?? "(null)",-18} | " +
                    $"pendingCode={pendingCode ?? "(null)",-18}");
                if (!string.IsNullOrEmpty(entry.OriginalText))
                    Console.WriteLine($"         原文: {Truncate(entry.OriginalText, 70)}");
                if (!string.IsNullOrEmpty(marker))
                    Console.WriteLine($"         ▶ {marker}");
                Console.WriteLine();
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsFocusNoneDiag(FormattedTextEntry entry)
        => entry.CommandSet.TryGetValue("focus", out var focus)
           && string.Equals(focus, "none", StringComparison.OrdinalIgnoreCase);

    /// <summary>复现 AckProcessor.ExtractFocusCode：从立绘焦点反查角色 code。</summary>
    private static string? ExtractFocusCodeDiag(FormattedTextEntry entry)
    {
        if (entry.Portraits.Count == 0) return null;
        var focus = entry.PortraitFocus;
        if (focus < 0 || focus >= entry.Portraits.Count)
            return null;

        var url = entry.Portraits[focus];
        var fileName = Path.GetFileNameWithoutExtension(url);
        var idx = fileName.IndexOf('-');
        if (idx > 0) fileName = fileName[..idx];
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    private static string ApplyPendingCodeDiag(
        FormattedTextEntry entry,
        Dictionary<string, string> nameToCode,
        Dictionary<string, string> codeToFacts,
        ref string? pendingCode,
        ref string? pendingFacts,
        bool hasLiveFocus)
    {
        // 与真实逻辑一致：仅当存在"在场且聚焦"的立绘时才用焦点锚点。
        if (hasLiveFocus)
        {
            var focusCode = ExtractFocusCodeDiag(entry);
            if (focusCode != null)
            {
                if (string.IsNullOrEmpty(entry.CharacterCode))
                    entry.CharacterCode = focusCode;
                if (string.IsNullOrEmpty(entry.PicFacts) && codeToFacts.TryGetValue(focusCode, out var focusFacts))
                    entry.PicFacts = focusFacts;
                if (!nameToCode.ContainsKey(entry.CharacterName))
                    nameToCode[entry.CharacterName] = focusCode;
                return $"焦点锚点: {entry.CharacterName} → {focusCode}";
            }
        }

        if (nameToCode.TryGetValue(entry.CharacterName, out var knownCode))
        {
            entry.CharacterCode = knownCode;
            if (string.IsNullOrEmpty(entry.PicFacts) && codeToFacts.TryGetValue(knownCode, out var knownFacts))
                entry.PicFacts = knownFacts;
            return $"命中已有映射: {entry.CharacterName} → {knownCode}";
        }
        if (pendingCode != null)
        {
            nameToCode[entry.CharacterName] = pendingCode;
            entry.CharacterCode = pendingCode;
            if (string.IsNullOrEmpty(entry.PicFacts) && pendingFacts != null)
            {
                codeToFacts[pendingCode] = pendingFacts;
                entry.PicFacts = pendingFacts;
            }
            var msg = $"⚠ 把最近一个立绘的 code 安到 {entry.CharacterName} 头上: {pendingCode}";
            pendingCode = null;
            pendingFacts = null;
            return msg;
        }
        return "无 pendingCode，未赋值";
    }

    // ──── Bug 2: PicDescEnricher 的对话行跳过 ────
    private static void DiagnoseEnrichment(List<FormattedTextEntry> entries)
    {
        Console.WriteLine("── PicDescEnricher 填充评估 ──");
        Console.WriteLine("(EnrichAsync 对 ResourceUrls.Count==0 的行直接 continue，对话行无立绘 URL 则跳过)");
        Console.WriteLine();

        var dialogRows = entries.Where(e => e.Type == "dialog" || !string.IsNullOrEmpty(e.CharacterName)).ToList();
        var dialogWithUrls = dialogRows.Where(e => e.ResourceUrls.Count > 0).ToList();
        var dialogWithFacts = dialogRows.Where(e => !string.IsNullOrEmpty(e.PicFacts)).ToList();

        Console.WriteLine($"对话/有名行: {dialogRows.Count}  其中含 ResourceUrls: {dialogWithUrls.Count}  已含 PicFacts: {dialogWithFacts.Count}");
        Console.WriteLine($"(对话行 ResourceUrls.Count==0 {dialogRows.Count - dialogWithUrls.Count} 条 → 会被 EnrichAsync 跳过，无图片描述)");
        Console.WriteLine();

        // 打印几个有 CharacterName 但无 ResourceUrls 的行
        var namedNoUrl = dialogRows.Where(e => e.ResourceUrls.Count == 0).Take(8).ToList();
        if (namedNoUrl.Count > 0)
        {
            Console.WriteLine("示例——有名但无 ResourceUrls（会被跳过无描述）的行：");
            foreach (var e in namedNoUrl)
            {
                Console.WriteLine($"  Idx={e.Index,4} | {e.CharacterName,-12} | code={e.CharacterCode ?? "(null)",-18} | ResourceUrls.Count={e.ResourceUrls.Count}");
                Console.WriteLine($"    原文: {Truncate(e.OriginalText, 60)}");
            }
        }
    }

    private static string Truncate(string s, int len)
        => s.Length <= len ? s : s[..len] + "…";
}