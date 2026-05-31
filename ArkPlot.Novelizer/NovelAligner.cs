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

        // 加载所有有 Dialog 的 FormattedTextEntry，按 PlotId + Index 排序
        var plotIds = plots.Select(p => p.Id).ToList();
        var allEntries = await _db.Queryable<FormattedTextEntry>()
            .Where(e => plotIds.Contains(e.PlotId) && !string.IsNullOrEmpty(e.Dialog))
            .OrderBy(e => e.PlotId)
            .OrderBy(e => e.Index)
            .ToListAsync();

        // 按 Plot 分组
        var entriesByPlot = allEntries
            .GroupBy(e => e.PlotId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Index).ToList());

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
                        segment.CharacterCode = entry.CharacterCode;
                        segment.EntryIndex = entry.Index;
                        alignedDialogs++;

                        var gender = InferGender(entry.CharacterCode, picDescByCode);

                        results.Add(new AlignmentEntry(
                            segment.Text, segment.IsDialog,
                            entry.CharacterName, entry.CharacterCode,
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

        // 搜索性别关键词
        if (desc.Contains("女性") || desc.Contains("女人") || desc.Contains("女孩") || desc.Contains("少女"))
            return "女";
        if (desc.Contains("男性") || desc.Contains("男人") || desc.Contains("男孩") || desc.Contains("少年"))
            return "男";

        return null;
    }
}
