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

        // 2. 从 DB 加载该活动的所有章节和对话 entries
        var act = await _db.Queryable<Act>()
            .FirstAsync(a => a.Name == actName && a.Lang == "zh_CN");

        if (act == null)
            throw new InvalidOperationException($"活动 '{actName}' 未找到。请先运行 CLI 生成原始 Markdown。");

        // 按 StorySort 排序加载所有章节
        var storyChapters = await _db.Queryable<StoryChapter>()
            .Where(sc => sc.ActId == act.Id)
            .OrderBy(sc => sc.StorySort)
            .ToListAsync();

        // 加载该活动所有已解析的 Plot
        var plots = await _db.Queryable<Plot>()
            .Where(p => p.ActId == act.Id && p.StoryChapterId > 0)
            .ToListAsync();

        // 加载该活动所有 FormattedTextEntry（包括 charslot 和 dialog），用于构建角色名→code映射
        var plotIds = plots.Select(p => p.Id).ToList();
        var allEntriesRaw = await _db.Queryable<FormattedTextEntry>()
            .Where(e => plotIds.Contains(e.PlotId))
            .OrderBy(e => e.PlotId)
            .OrderBy(e => e.Index)
            .ToListAsync();

        // 按 Plot 分组
        var entriesByPlot = new Dictionary<long, List<FormattedTextEntry>>();
        // 全局角色名 → CharacterCode 映射（从 charslot + dialog 配对中学习）
        var nameToCode = new Dictionary<string, string>();

        foreach (var group in allEntriesRaw.GroupBy(e => e.PlotId))
        {
            var sorted = group.OrderBy(e => e.Index).ToList();
            var dialogEntries = sorted.Where(e => !string.IsNullOrEmpty(e.Dialog)).ToList();
            entriesByPlot[group.Key] = dialogEntries;

            // 扫描：每次 charslot 后紧跟的第一个 dialog，建立 name→code 映射
            // 只对"有 name 参数的 charslot"建立映射（无 name 的只是焦点切换）
            // 排除 focus="none" 的 charslot（画外音，说话者不在画面中）
            FormattedTextEntry? lastCharSlot = null;
            foreach (var entry in sorted)
            {
                if (entry.Type == "charslot" && entry.CommandSet != null && entry.CommandSet.Count > 0)
                {
                    // 只对有 name 参数且 focus != "none" 的 charslot 感兴趣（新角色登场）
                    if (entry.CommandSet.ContainsKey("name"))
                    {
                        var focusValue = entry.CommandSet.TryGetValue("focus", out var f) ? f : null;
                        if (focusValue != "none")
                        {
                            lastCharSlot = entry;
                        }
                        // focus="none" 是画外音，跳过
                    }
                    // 无 name 的 charslot（如 focus="r"）只是焦点切换，跳过
                }
                else if (!string.IsNullOrEmpty(entry.Dialog) && !string.IsNullOrEmpty(entry.CharacterName))
                {
                    if (lastCharSlot != null && !nameToCode.ContainsKey(entry.CharacterName))
                    {
                        var code = ExtractCodeFromCharSlot(lastCharSlot);
                        if (code != null)
                            nameToCode[entry.CharacterName] = code;
                    }
                    lastCharSlot = null; // 已消费
                }
            }
        }

        // 也加载每个 Plot 内 charslot→dialog 的 code 映射（局部，处理同角色换立绘的情况）
        var charCodeAtEntry = new Dictionary<(long PlotId, int Index), string?>();
        foreach (var group in allEntriesRaw.GroupBy(e => e.PlotId))
        {
            var sorted = group.OrderBy(e => e.Index).ToList();
            string? lastCharSlotCode = null;
            foreach (var entry in sorted)
            {
                // 只对有 name 参数的 charslot 更新 lastCharSlotCode
                if (entry.Type == "charslot" && entry.CommandSet != null && 
                    entry.CommandSet.Count > 0 && entry.CommandSet.ContainsKey("name"))
                {
                    var code = ExtractCodeFromCharSlot(entry);
                    if (code != null) lastCharSlotCode = code;
                }
                else if (!string.IsNullOrEmpty(entry.Dialog))
                {
                    // 优先用 name→code 全局映射，fallback 到最近 charslot
                    var nameCode = !string.IsNullOrEmpty(entry.CharacterName) 
                        ? nameToCode.GetValueOrDefault(entry.CharacterName) 
                        : null;
                    charCodeAtEntry[(group.Key, entry.Index)] = nameCode ?? lastCharSlotCode;
                }
            }
        }

        // 预加载所有 PicDescription，用于判断性别
        var allPicDescs = await _db.Queryable<PicDescription>().ToListAsync();
        var picDescByCode = allPicDescs
            .Where(p => !string.IsNullOrEmpty(p.DedupKey))
            .ToDictionary(p => p.DedupKey, p => p.PicDesc);

        // 3. 匹配小说章节 ↔ Plot（按标题）
        var results = new List<AlignmentEntry>();
        int totalDialogs = 0;
        int alignedDialogs = 0;
        int matchedChapters = 0;

        foreach (var novelChapter in novelChapters)
        {
            // 找到标题匹配的 Plot
            var plot = plots.FirstOrDefault(p =>
                p.Title.Contains(novelChapter.Title) || novelChapter.Title.Contains(p.Title));

            if (plot == null) continue;
            matchedChapters++;

            // 获取该 Plot 的所有 Dialog entries（已排序）
            if (!entriesByPlot.TryGetValue(plot.Id, out var plotEntries)) continue;

            // 按顺序对齐：小说对话 ↔ DB entries
            var dbQueue = new Queue<FormattedTextEntry>(plotEntries);

            foreach (var segment in novelChapter.Segments)
            {
                if (segment.IsDialog)
                {
                    totalDialogs++;

                    if (dbQueue.Count > 0)
                    {
                        var entry = dbQueue.Dequeue();
                        segment.CharacterName = entry.CharacterName;
                        segment.EntryIndex = entry.Index;
                        alignedDialogs++;

                        // 使用该 dialog 之前最近的 charslot 的 CharacterCode，
                        // 而非 entry.CharacterCode（可能是旧值/错值）
                        var effectiveCode = charCodeAtEntry.GetValueOrDefault((plot.Id, entry.Index))
                                            ?? entry.CharacterCode;
                        segment.CharacterCode = effectiveCode;

                        var gender = InferGender(effectiveCode, picDescByCode);

                        results.Add(new AlignmentEntry(
                            segment.Text, segment.IsDialog,
                            entry.CharacterName, effectiveCode,
                            entry.Index, novelChapter.Title, gender));
                    }
                    else
                    {
                        // 小说中的对话比 DB 多（LLM 可能添加了额外对话）
                        results.Add(new AlignmentEntry(
                            segment.Text, segment.IsDialog,
                            null, null, -1, novelChapter.Title, null));
                    }
                }
                else
                {
                    // 旁白也输出，方便 TTS 分段
                    results.Add(new AlignmentEntry(
                        segment.Text, segment.IsDialog,
                        null, null, -1, novelChapter.Title, null));
                }
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

    /// <summary>
    /// 从文件名中提取活动名。
    /// 格式：{活动名}_novel_{model}.md → {活动名}
    /// </summary>
    private static string ExtractActName(string fileNameWithoutExt)
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
    private static string? ExtractCodeFromCharSlot(FormattedTextEntry entry)
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
    private static string? InferGender(string? characterCode, Dictionary<string, string> picDescByCode)
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
