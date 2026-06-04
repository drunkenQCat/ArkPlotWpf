using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Novelizer;

/// <summary>
/// 对齐结果：一个小说片段与对应 FormattedTextEntry 的映射。
/// </summary>
public record AlignmentEntry(
    string NovelText,
    bool IsDialog,
    string? CharacterName,
    string? CharacterCode,
    int EntryIndex,
    string ChapterTitle,
    string? Gender = null
);

/// <summary>
/// 对齐统计信息。
/// </summary>
public record AlignmentStats(
    int TotalNovelChapters,
    int MatchedChapters,
    int TotalDialogs,
    int AlignedDialogs,
    int UnalignedDialogs
);

/// <summary>
/// 将小说化文本中的对话与原始 FormattedTextEntry 按顺序对齐。
/// 
/// 核心假设：LLM 小说化时保持对话顺序不变，
/// 因此小说中引号内对话的出现顺序 == DB 中 Dialog 字段的顺序。
/// </summary>
public class NovelAligner
{
    private readonly SqlSugarClient _db;

    public NovelAligner() : this(DbFactory.GetClient()) { }

    public NovelAligner(SqlSugarClient db)
    {
        _db = db;
    }

    /// <summary>
    /// 从小说文件名推断活动名，执行完整对齐。
    /// 文件名格式：{活动名}_novel_{model}.md
    /// </summary>
    public async Task<(List<AlignmentEntry> Entries, AlignmentStats Stats)> AlignByFileNameAsync(
        string novelFilePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(novelFilePath);
        var actName = ExtractActName(fileName);
        var novelText = await File.ReadAllTextAsync(novelFilePath);

        return await AlignAsync(novelText, actName);
    }

    /// <summary>
    /// 执行对齐：小说文本 + 活动名 → 对齐结果。
    /// </summary>
    public async Task<(List<AlignmentEntry> Entries, AlignmentStats Stats)> AlignAsync(
        string novelText, string actName)
    {
        // 1. 从小说文本提取章节和对话
        var novelChapters = DialogExtractor.ExtractChapters(novelText);

        // 2. 从 DB 加载数据
        var (plots, entriesByPlot, allEntriesByPlot) = await LoadActDataAsync(actName);

        // 3. 构建映射
        var nameToCode = BuildNameToCodeMap(allEntriesByPlot);
        var charCodeAtEntry = BuildCharCodeAtEntry(allEntriesByPlot, nameToCode);

        // 4. 预加载 PicDescription
        var picDescByCode = await LoadPicDescMapAsync();

        // 5. 对齐
        return AlignChapters(novelChapters, plots, entriesByPlot, charCodeAtEntry, picDescByCode);
    }

    /// <summary>
    /// 从 DB 加载活动的所有 Plot、FormattedTextEntry。
    /// 返回 (plots, entriesByPlot[仅dialog], allEntriesByPlot[含charslot])。
    /// </summary>
    private async Task<(List<Plot> Plots,
        Dictionary<long, List<FormattedTextEntry>> EntriesByPlot,
        Dictionary<long, List<FormattedTextEntry>> AllEntriesByPlot)>
        LoadActDataAsync(string actName)
    {
        var act = await _db.Queryable<Act>()
            .FirstAsync(a => a.Name == actName && a.Lang == "zh_CN");

        if (act == null)
            throw new InvalidOperationException($"活动 '{actName}' 未找到。请先运行 CLI 生成原始 Markdown。");

        var plots = await _db.Queryable<Plot>()
            .Where(p => p.ActId == act.Id && p.StoryChapterId > 0)
            .ToListAsync();

        var plotIds = plots.Select(p => p.Id).ToList();
        var allEntriesRaw = await _db.Queryable<FormattedTextEntry>()
            .Where(e => plotIds.Contains(e.PlotId))
            .OrderBy(e => e.PlotId)
            .OrderBy(e => e.Index)
            .ToListAsync();

        var entriesByPlot = new Dictionary<long, List<FormattedTextEntry>>();
        var allEntriesByPlot = new Dictionary<long, List<FormattedTextEntry>>();

        foreach (var group in allEntriesRaw.GroupBy(e => e.PlotId))
        {
            var sorted = group.OrderBy(e => e.Index).ToList();
            entriesByPlot[group.Key] = sorted.Where(e => !string.IsNullOrEmpty(e.Dialog)).ToList();
            allEntriesByPlot[group.Key] = sorted;
        }

        return (plots, entriesByPlot, allEntriesByPlot);
    }

    /// <summary>
    /// 从 charslot→dialog 配对中构建角色名→CharacterCode 的全局映射。
    /// 排除 focus="none" 的 charslot（画外音）和无 name 参数的 charslot（焦点切换）。
    /// </summary>
    internal static Dictionary<string, string> BuildNameToCodeMap(
        Dictionary<long, List<FormattedTextEntry>> allEntriesByPlot)
    {
        var nameToCode = new Dictionary<string, string>();

        foreach (var (_, entries) in allEntriesByPlot)
        {
            FormattedTextEntry? lastCharSlot = null;
            foreach (var entry in entries)
            {
                if (entry.Type == "charslot")
                {
                    lastCharSlot = IsEffectiveCharSlot(entry) ? entry : lastCharSlot;
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Dialog) || string.IsNullOrEmpty(entry.CharacterName))
                    continue;

                // 这是一条有角色名的 dialog，消费最近的 charslot
                if (lastCharSlot != null && !nameToCode.ContainsKey(entry.CharacterName))
                {
                    var code = ExtractCodeFromCharSlot(lastCharSlot);
                    if (code != null)
                        nameToCode[entry.CharacterName] = code;
                }
                lastCharSlot = null;
            }
        }

        return nameToCode;
    }

    /// <summary>
    /// 判断 charslot 是否为"有效登场"（有 name 参数且 focus ≠ "none"）。
    /// </summary>
    internal static bool IsEffectiveCharSlot(FormattedTextEntry entry)
    {
        if (entry.CommandSet == null || entry.CommandSet.Count == 0)
            return false;
        if (!entry.CommandSet.ContainsKey("name"))
            return false;
        var focus = entry.CommandSet.TryGetValue("focus", out var f) ? f : null;
        return focus != "none";
    }

    /// <summary>
    /// 为每个 dialog entry 计算其有效的 CharacterCode。
    /// 优先用 nameToCode 全局映射，fallback 到该 entry 之前最近的 charslot code。
    /// </summary>
    internal static Dictionary<(long PlotId, int Index), string?> BuildCharCodeAtEntry(
        Dictionary<long, List<FormattedTextEntry>> allEntriesByPlot,
        Dictionary<string, string> nameToCode)
    {
        var charCodeAtEntry = new Dictionary<(long PlotId, int Index), string?>();

        foreach (var (plotId, entries) in allEntriesByPlot)
        {
            string? lastCharSlotCode = null;
            foreach (var entry in entries)
            {
                if (entry.Type == "charslot")
                {
                    var code = IsEffectiveCharSlot(entry) ? ExtractCodeFromCharSlot(entry) : null;
                    if (code != null) lastCharSlotCode = code;
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Dialog))
                    continue;

                var nameCode = !string.IsNullOrEmpty(entry.CharacterName)
                    ? nameToCode.GetValueOrDefault(entry.CharacterName)
                    : null;
                charCodeAtEntry[(plotId, entry.Index)] = nameCode ?? lastCharSlotCode;
            }
        }

        return charCodeAtEntry;
    }

    /// <summary>
    /// 加载 PicDescription 表，构建 DedupKey → PicDesc 映射。
    /// </summary>
    private async Task<Dictionary<string, string>> LoadPicDescMapAsync()
    {
        var allPicDescs = await _db.Queryable<PicDescription>().ToListAsync();
        return allPicDescs
            .Where(p => !string.IsNullOrEmpty(p.DedupKey))
            .ToDictionary(p => p.DedupKey, p => p.PicDesc);
    }

    /// <summary>
    /// 将小说章节与 DB Plot 按标题匹配，按对话顺序对齐，生成 AlignmentEntry 列表。
    /// </summary>
    private static (List<AlignmentEntry> Entries, AlignmentStats Stats) AlignChapters(
        List<NovelChapter> novelChapters,
        List<Plot> plots,
        Dictionary<long, List<FormattedTextEntry>> entriesByPlot,
        Dictionary<(long PlotId, int Index), string?> charCodeAtEntry,
        Dictionary<string, string> picDescByCode)
    {
        var results = new List<AlignmentEntry>();
        int totalDialogs = 0;
        int alignedDialogs = 0;
        int matchedChapters = 0;

        foreach (var novelChapter in novelChapters)
        {
            var plot = plots.FirstOrDefault(p =>
                p.Title.Contains(novelChapter.Title) || novelChapter.Title.Contains(p.Title));
            if (plot == null) continue;
            matchedChapters++;

            if (!entriesByPlot.TryGetValue(plot.Id, out var plotEntries)) continue;

            var dbQueue = new Queue<FormattedTextEntry>(plotEntries);
            foreach (var segment in novelChapter.Segments)
            {
                if (!segment.IsDialog)
                {
                    results.Add(MakeNarrationEntry(segment, novelChapter.Title));
                    continue;
                }

                totalDialogs++;
                var entry = dbQueue.Count > 0 ? dbQueue.Dequeue() : null;
                if (entry == null)
                {
                    results.Add(MakeUnalignedDialogEntry(segment, novelChapter.Title));
                    continue;
                }

                alignedDialogs++;
                results.Add(MakeAlignedDialogEntry(
                    segment, entry, plot.Id, novelChapter.Title,
                    charCodeAtEntry, picDescByCode));
            }
        }

        var stats = new AlignmentStats(
            TotalNovelChapters: novelChapters.Count,
            MatchedChapters: matchedChapters,
            TotalDialogs: totalDialogs,
            AlignedDialogs: alignedDialogs,
            UnalignedDialogs: totalDialogs - alignedDialogs);

        return (results, stats);
    }

    private static AlignmentEntry MakeNarrationEntry(NovelSegment segment, string chapterTitle)
        => new(segment.Text, false, null, null, -1, chapterTitle, null);

    private static AlignmentEntry MakeUnalignedDialogEntry(NovelSegment segment, string chapterTitle)
        => new(segment.Text, true, null, null, -1, chapterTitle, null);

    private static AlignmentEntry MakeAlignedDialogEntry(
        NovelSegment segment, FormattedTextEntry entry, long plotId, string chapterTitle,
        Dictionary<(long PlotId, int Index), string?> charCodeAtEntry,
        Dictionary<string, string> picDescByCode)
    {
        segment.CharacterName = entry.CharacterName;
        segment.EntryIndex = entry.Index;

        var effectiveCode = charCodeAtEntry.GetValueOrDefault((plotId, entry.Index))
                            ?? entry.CharacterCode;
        segment.CharacterCode = effectiveCode;

        var gender = InferGender(effectiveCode, picDescByCode);
        return new AlignmentEntry(
            segment.Text, true,
            entry.CharacterName, effectiveCode,
            entry.Index, chapterTitle, gender);
    }

    /// <summary>
    /// 从文件名中提取活动名。
    /// 格式：{活动名}_novel_{model}.md → {活动名}
    /// </summary>
    internal static string ExtractActName(string fileNameWithoutExt)
    {
        var novelIdx = fileNameWithoutExt.IndexOf("_novel_", StringComparison.Ordinal);
        if (novelIdx > 0)
            return fileNameWithoutExt[..novelIdx];
        return fileNameWithoutExt;
    }

    /// <summary>
    /// 从 charslot 条目的 CommandSet 中提取 CharacterCode。
    /// 根据 focus 决定取 name 还是 name2，去除 # 和 $ 后缀。
    /// 这比依赖 entry.CharacterCode 更可靠，因为 DB 中的 CharacterCode 可能是旧代码留下的错误值。
    /// </summary>
    internal static string? ExtractCodeFromCharSlot(FormattedTextEntry entry)
    {
        if (entry.CommandSet == null || entry.CommandSet.Count == 0)
            return entry.CharacterCode; // fallback

        // 根据 focus 决定取 name 还是 name2
        var nameKey = "name";
        if (entry.CommandSet.TryGetValue("focus", out var focusVal) &&
            focusVal == "2" && entry.CommandSet.ContainsKey("name2"))
        {
            nameKey = "name2";
        }

        if (!entry.CommandSet.TryGetValue(nameKey, out var rawName) || string.IsNullOrEmpty(rawName))
            return entry.CharacterCode; // fallback

        // 去除 # 后缀（如 "avg_npc_1272_1#1$1" → "avg_npc_1272_1"）
        var code = rawName.ToLower();
        var hashIdx = code.IndexOf('#');
        if (hashIdx >= 0) code = code[..hashIdx];

        return code;
    }

    /// <summary>
    /// 根据 PicDescription 的描述文本推断角色性别。
    /// 搜索描述中的"男"或"女"关键词。
    /// </summary>
    internal static string? InferGender(string? characterCode, Dictionary<string, string> picDescByCode)
    {
        if (string.IsNullOrEmpty(characterCode))
            return null;

        // 尝试匹配 CharacterCode（可能带 # 后缀）
        var baseCode = characterCode.Split('#')[0];
        if (!picDescByCode.TryGetValue(baseCode, out var desc))
            return null;

        // 优先：检查描述前 100 字符中的"她"或"他"（适配小说化描述）
        var head = desc.Length > 100 ? desc[..100] : desc;
        if (head.Contains("她"))
            return "女";
        if (head.Contains("他"))
            return "男";

        // 兜底：旧版分析性描述中的关键词
        if (desc.Contains("女性") || desc.Contains("女人") || desc.Contains("女孩") || desc.Contains("少女"))
            return "女";
        if (desc.Contains("男性") || desc.Contains("男人") || desc.Contains("男孩") || desc.Contains("少年"))
            return "男";

        return null;
    }
}
