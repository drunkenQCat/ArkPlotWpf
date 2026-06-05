using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Tts.Alignment;

/// <summary>
/// 将小说化文本中的对话与原始 FormattedTextEntry 按顺序对齐。
///
/// 核心假设：LLM 小说化时保持对话顺序不变，
/// 因此小说中引号内对话的出现顺序 == DB 中 Dialog 字段的顺序。
/// </summary>
public class NovelAligner
{
    private readonly SqlSugarClient _db;
    private readonly GenderOverrideProvider? _genderOverrides;

    public NovelAligner(GenderOverrideProvider? genderOverrides = null)
        : this(DbFactory.GetClient(), genderOverrides) { }

    public NovelAligner(SqlSugarClient db, GenderOverrideProvider? genderOverrides = null)
    {
        _db = db;
        _genderOverrides = genderOverrides;
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
        var novelChapters = DialogExtractor.ExtractChapters(novelText);
        var (plots, entriesByPlot, allEntriesByPlot) = await LoadActDataAsync(actName);
        var nameToCode = BuildNameToCodeMap(allEntriesByPlot);
        var charCodeAtEntry = BuildCharCodeAtEntry(allEntriesByPlot, nameToCode);
        var picDescByCode = await LoadPicDescMapAsync();
        return AlignChapters(novelChapters, plots, entriesByPlot, charCodeAtEntry, picDescByCode, _genderOverrides);
    }

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

    internal static bool IsEffectiveCharSlot(FormattedTextEntry entry)
    {
        if (entry.CommandSet == null || entry.CommandSet.Count == 0)
            return false;
        if (!entry.CommandSet.ContainsKey("name"))
            return false;
        var focus = entry.CommandSet.TryGetValue("focus", out var f) ? f : null;
        return focus != "none";
    }

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

    private async Task<Dictionary<string, string>> LoadPicDescMapAsync()
    {
        var allPicDescs = await _db.Queryable<PicDescription>().ToListAsync();
        return allPicDescs
            .Where(p => !string.IsNullOrEmpty(p.DedupKey))
            .ToDictionary(p => p.DedupKey, p => p.PicDesc);
    }

    private static (List<AlignmentEntry> Entries, AlignmentStats Stats) AlignChapters(
        List<NovelChapter> novelChapters,
        List<Plot> plots,
        Dictionary<long, List<FormattedTextEntry>> entriesByPlot,
        Dictionary<(long PlotId, int Index), string?> charCodeAtEntry,
        Dictionary<string, string> picDescByCode,
        GenderOverrideProvider? genderOverrides = null)
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
                    charCodeAtEntry, picDescByCode, genderOverrides));
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
        Dictionary<string, string> picDescByCode,
        GenderOverrideProvider? genderOverrides = null)
    {
        segment.CharacterName = entry.CharacterName;
        segment.EntryIndex = entry.Index;

        var effectiveCode = charCodeAtEntry.GetValueOrDefault((plotId, entry.Index))
                            ?? entry.CharacterCode;
        segment.CharacterCode = effectiveCode;

        var gender = InferGender(effectiveCode, picDescByCode, genderOverrides, entry.CharacterName);
        return new AlignmentEntry(
            segment.Text, true,
            entry.CharacterName, effectiveCode,
            entry.Index, chapterTitle, gender);
    }

    internal static string ExtractActName(string fileNameWithoutExt)
    {
        var novelIdx = fileNameWithoutExt.IndexOf("_novel_", StringComparison.Ordinal);
        if (novelIdx > 0)
            return fileNameWithoutExt[..novelIdx];
        return fileNameWithoutExt;
    }

    internal static string? ExtractCodeFromCharSlot(FormattedTextEntry entry)
    {
        if (entry.CommandSet == null || entry.CommandSet.Count == 0)
            return entry.CharacterCode;

        var nameKey = "name";
        if (entry.CommandSet.TryGetValue("focus", out var focusVal) &&
            focusVal == "2" && entry.CommandSet.ContainsKey("name2"))
        {
            nameKey = "name2";
        }

        if (!entry.CommandSet.TryGetValue(nameKey, out var rawName) || string.IsNullOrEmpty(rawName))
            return entry.CharacterCode;

        var code = rawName.ToLower();
        var hashIdx = code.IndexOf('#');
        if (hashIdx >= 0) code = code[..hashIdx];

        return code;
    }

    /// <summary>
    /// 推断性别：优先查 override → fallback PicDescription 推断。
    /// </summary>
    internal static string? InferGender(
        string? characterCode,
        Dictionary<string, string> picDescByCode,
        GenderOverrideProvider? genderOverrides = null,
        string? characterName = null)
    {
        // 1. 优先查 override（按角色名）
        var overrideGender = genderOverrides?.GetOverride(characterName);
        if (!string.IsNullOrEmpty(overrideGender))
            return overrideGender;

        // 2. Fallback: PicDescription 推断
        return InferGenderFromPicDesc(characterCode, picDescByCode);
    }

    /// <summary>
    /// 从 PicDescription 推断性别（原始逻辑）。
    /// </summary>
    internal static string? InferGenderFromPicDesc(string? characterCode, Dictionary<string, string> picDescByCode)
    {
        if (string.IsNullOrEmpty(characterCode))
            return null;

        var baseCode = characterCode.Split('#')[0];
        if (!picDescByCode.TryGetValue(baseCode, out var desc))
            return null;

        var head = desc.Length > 100 ? desc[..100] : desc;
        if (head.Contains("她"))
            return "女";
        if (head.Contains("他"))
            return "男";

        if (desc.Contains("女性") || desc.Contains("女人") || desc.Contains("女孩") || desc.Contains("少女"))
            return "女";
        if (desc.Contains("男性") || desc.Contains("男人") || desc.Contains("男孩") || desc.Contains("少年"))
            return "男";

        return null;
    }
}
