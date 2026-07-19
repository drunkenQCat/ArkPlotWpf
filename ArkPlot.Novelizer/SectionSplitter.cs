using System.Text.Json;
using ArkPlot.Arknights;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Novelizer;

/// <summary>
/// TTS 分节器：Novelizer Pass 2 后处理。
/// 利用 DB 中的 Bg 变化点在小说化文本中定位，批量调用 LLM 生成分节插入指令。
/// </summary>
public class SectionSplitter
{
    private readonly BailianClient _client;
    private readonly ISqlSugarClient _db;
    private readonly Action<string>? _onLog;

    public SectionSplitter(BailianClient client, Action<string>? onLog = null)
        : this(client, DbFactory.GetClient(), onLog)
    {
    }

    public SectionSplitter(BailianClient client, ISqlSugarClient db, Action<string>? onLog = null)
    {
        _client = client;
        _db = db;
        _onLog = onLog;
    }

    /// <summary>
    /// 对小说化文本执行 TTS 分节，返回带节标题和过渡句的文本。
    /// 若任何环节失败，返回原始文本并记录日志。
    /// </summary>
    public async Task<string> SplitAsync(
        string novelText,
        long plotId,
        string model = "deepseek-v4-flash",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        var log = onLog ?? _onLog;
        log?.Invoke($"[SectionSplitter] 开始分节。plotId={plotId}, model={model}, novelText={novelText.Length} 字符");

        try
        {
            ct.ThrowIfCancellationRequested();

            var transitions = FindBgTransitions(plotId);
            log?.Invoke($"[SectionSplitter] 找到 {transitions.Count} 个 Bg 变化点（已过滤 bg_black）");

            if (transitions.Count == 0)
                return novelText;

            var snippets = LocateSnippets(novelText, transitions, log);
            log?.Invoke($"[SectionSplitter] 成功定位 {snippets.Count}/{transitions.Count} 个切片");

            string userContent;
            string systemPrompt;
            if (snippets.Count >= Math.Max(1, transitions.Count / 2))
            {
                userContent = BuildUserContent(snippets);
                systemPrompt = SectionSplitterPrompt.DefaultSystemPrompt;
            }
            else
            {
                log?.Invoke("[SectionSplitter] 切片定位不足，切换到整章模式");
                userContent = BuildWholeChapterUserContent(novelText, transitions);
                systemPrompt = SectionSplitterPrompt.WholeChapterSystemPrompt;
            }

            log?.Invoke($"[SectionSplitter] 发送 {userContent.Length} 字符 prompt");

            var result = await _client.ChatAsync(model, systemPrompt, userContent);
            ct.ThrowIfCancellationRequested();

            log?.Invoke($"[SectionSplitter] API 返回 answer={result.AnswerContent.Length} 字符");
            return ApplyInsertions(novelText, result.AnswerContent, log);
        }
        catch (BailianException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[SectionSplitter] 分节失败，返回原文: {ex.Message}");
            return novelText;
        }
    }

    /// <summary>
    /// 通过章节标题查 DB 获取 PlotId，再执行分节。
    /// 若找不到 Plot，返回原文。
    /// </summary>
    public async Task<string> SplitByTitleAsync(
        string novelText,
        string chapterTitle,
        string model = "deepseek-v4-flash",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        var log = onLog ?? _onLog;
        var plotId = ResolvePlotId(chapterTitle);

        if (plotId <= 0)
        {
            log?.Invoke($"[SectionSplitter] 未找到章节「{chapterTitle}」对应的 Plot，跳过分节");
            return novelText;
        }

        return await SplitAsync(novelText, plotId, model, log, ct);
    }

    /// <summary>
    /// 通过章节标题查 DB 获取 PlotId。
    /// </summary>
    private long ResolvePlotId(string chapterTitle)
    {
        var normalized = chapterTitle.Trim();
        var plot = _db.Queryable<Plot>()
            .Where(p => p.Title == normalized)
            .First();

        if (plot is not null)
            return plot.Id;

        // 回退：模糊匹配
        plot = _db.Queryable<Plot>()
            .Where(p => p.Title.Contains(normalized) || normalized.Contains(p.Title))
            .First();

        return plot?.Id ?? 0;
    }

    /// <summary>
    /// 从 DB 查询指定 Plot 的条目，找出 Bg 变化点（过滤 bg_black）。
    /// </summary>
    private List<BgTransition> FindBgTransitions(long plotId)
    {
        var entries = _db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plotId)
            .OrderBy(e => e.Index)
            .ToList();

        var transitions = new List<BgTransition>();
        string prevBg = "";
        FormattedTextEntry? prevEntry = null;

        foreach (var entry in entries)
        {
            var bg = entry.Bg ?? "";
            if (!string.IsNullOrEmpty(prevBg) &&
                !string.IsNullOrEmpty(bg) &&
                !bg.Contains(SectionSplitterPrompt.BlackBg, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(prevBg, bg, StringComparison.OrdinalIgnoreCase))
            {
                transitions.Add(new BgTransition(
                    entry.Index,
                    prevBg,
                    bg,
                    FindNearestDialog(entries, prevEntry?.Index ?? entry.Index, backward: true),
                    FindNearestDialog(entries, entry.Index, backward: false)));
            }

            prevBg = bg;
            prevEntry = entry;
        }

        return transitions;
    }

    /// <summary>
    /// 在指定位置前后搜索最近的对话文本（非空 Dialog）。
    /// </summary>
    private static string FindNearestDialog(List<FormattedTextEntry> entries, int anchorIndex, bool backward)
    {
        var idx = entries.FindIndex(e => e.Index == anchorIndex);
        if (idx < 0) idx = entries.Count / 2;

        var radius = SectionSplitterPrompt.SearchRadius;
        var start = backward ? Math.Max(0, idx - radius) : idx;
        var end = backward ? idx : Math.Min(entries.Count, idx + radius + 1);

        for (int i = backward ? end - 1 : start; backward ? i >= start : i < end; i += backward ? -1 : 1)
        {
            var dialog = entries[i].Dialog;
            if (!string.IsNullOrWhiteSpace(dialog))
                return dialog;
        }

        return "";
    }

    /// <summary>
    /// 用 Bg 变化点附近文本在小说正文中定位，提取前后各 125 字的切片。
    /// 锚点选择按优先级：当前对话子串 → 前场景对话子串 → 当前 Bg 名称关键词。
    /// </summary>
    private static List<Snippet> LocateSnippets(
        string novel,
        List<BgTransition> transitions,
        Action<string>? log)
    {
        var snippets = new List<Snippet>();
        foreach (var t in transitions)
        {
            var (anchor, pos) = PickAnchor(novel, t);
            if (string.IsNullOrWhiteSpace(anchor) || pos < 0)
            {
                log?.Invoke($"[SectionSplitter] 定位失败 EntryIndex={t.EntryIndex}：无可用定位文本");
                continue;
            }

            var start = Math.Max(0, pos - SectionSplitterPrompt.SnippetHalfLength);
            var end = Math.Min(novel.Length, pos + SectionSplitterPrompt.SnippetHalfLength);
            var text = novel[start..end];

            snippets.Add(new Snippet(pos, text, t));
        }

        return snippets;
    }

    /// <summary>
    /// 选择定位锚点。依次尝试当前对话、前场景对话、Bg 关键词的递减长度子串，
    /// 返回能在小说正文中定位的最长子串及其位置。
    /// </summary>
    private static (string Anchor, int Position) PickAnchor(string novel, BgTransition transition)
    {
        var candidates = new List<string>
        {
            transition.CurrDialog,
            transition.PrevDialog,
            transition.CurrChar,
            transition.PrevChar
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            for (int len = SectionSplitterPrompt.MaxAnchorLength; len >= SectionSplitterPrompt.MinAnchorLength; len -= 2)
            {
                var anchor = FirstNChars(candidate, len);
                if (string.IsNullOrWhiteSpace(anchor))
                    continue;

                var pos = FindAnchorPosition(novel, anchor);
                if (pos >= 0)
                    return (anchor, pos);
            }
        }

        return ("", -1);
    }

    /// <summary>
    /// 在小说正文中定位锚点，支持去标点回退。
    /// </summary>
    private static int FindAnchorPosition(string novel, string anchor)
    {
        var exact = novel.IndexOf(anchor, StringComparison.Ordinal);
        if (exact >= 0) return exact;

        var cleaned = RemovePunctuation(anchor);
        if (!string.IsNullOrEmpty(cleaned) && cleaned != anchor)
        {
            var fallback = novel.IndexOf(cleaned, StringComparison.Ordinal);
            if (fallback >= 0) return fallback;
        }

        return -1;
    }

    /// <summary>
    /// 取字符串前 N 个字符，去除首尾空白。
    /// </summary>
    private static string FirstNChars(string value, int n)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var trimmed = value.Trim();
        return trimmed.Length <= n ? trimmed : trimmed[..n];
    }

    /// <summary>
    /// 去除常见标点，用于回退定位。
    /// </summary>
    private static string RemovePunctuation(string value)
    {
        return new string(value.Where(c => !char.IsPunctuation(c)).ToArray());
    }

    /// <summary>
    /// 将多个切片拼接为 LLM user content。
    /// </summary>
    private static string BuildUserContent(List<Snippet> snippets)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("以下是小说章节中的场景切换片段，请为每个片段返回插入指令。");
        sb.AppendLine();

        for (int i = 0; i < snippets.Count; i++)
        {
            var s = snippets[i];
            sb.AppendLine($"--- 片段 {i + 1} ---");
            sb.AppendLine($"前背景: {s.Transition.PrevChar}");
            sb.AppendLine($"当前背景: {s.Transition.CurrChar}");
            sb.AppendLine($"前文对话: {s.Transition.PrevDialog}");
            sb.AppendLine($"当前对话: {s.Transition.CurrDialog}");
            sb.AppendLine("正文片段:");
            sb.AppendLine(s.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 整章模式下，将完整小说和背景切换点列表拼接为 LLM user content。
    /// </summary>
    private static string BuildWholeChapterUserContent(string novel, List<BgTransition> transitions)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("以下是完整小说化文本，以及原始剧本中检测到的背景切换点。请根据小说内容判断哪些切换点需要插入节标题和过渡句。");
        sb.AppendLine();
        sb.AppendLine("=== 背景切换点（按出现顺序）===");

        for (int i = 0; i < transitions.Count; i++)
        {
            var t = transitions[i];
            sb.AppendLine($"切换 {i + 1}: EntryIndex={t.EntryIndex}");
            sb.AppendLine($"  前背景: {t.PrevChar}");
            sb.AppendLine($"  当前背景: {t.CurrChar}");
            sb.AppendLine($"  前文对话: {t.PrevDialog}");
            sb.AppendLine($"  当前对话: {t.CurrDialog}");
        }

        sb.AppendLine();
        sb.AppendLine("=== 小说正文 ===");
        sb.AppendLine(novel);

        return sb.ToString();
    }

    /// <summary>
    /// 解析 LLM 返回的 JSON 数组并按位置顺序插入内容。
    /// 插入点会调整到 anchor 所在段落的末尾，确保节标题独立成段。
    /// </summary>
    private static string ApplyInsertions(string novel, string json, Action<string>? log)
    {
        var insertions = ParseInsertions(json, log);
        if (insertions.Count == 0)
            return novel;

        var valid = insertions
            .Where(i => !string.IsNullOrWhiteSpace(i.InsertBefore))
            .Select(i =>
            {
                var pos = FindAnchorPosition(novel, i.Anchor);
                return pos < 0 ? null : new Insertion(i.Anchor, i.InsertBefore, i.Type, pos);
            })
            .Where(i => i != null)
            .Cast<Insertion>()
            .OrderByDescending(i => i.Position)
            .ToList();

        var sb = new System.Text.StringBuilder(novel);
        foreach (var insertion in valid)
        {
            var pos = MoveToParagraphEnd(novel, insertion.Position, insertion.Anchor.Length);
            var text = NormalizeInsertion(insertion.InsertBefore);
            sb.Insert(pos, text);
            log?.Invoke($"[SectionSplitter] 在 pos={pos} 插入 {text.Length} 字符");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 将插入位置从 anchor 开头移动到 anchor 所在段落之后，
    /// 确保插入的节标题独立成段，不破坏现有句子。
    /// </summary>
    private static int MoveToParagraphEnd(string novel, int anchorPos, int anchorLength)
    {
        var afterAnchor = anchorPos + anchorLength;
        var nextNewline = novel.IndexOf('\n', afterAnchor);
        if (nextNewline < 0)
            return novel.Length;

        // 跳过多余的换行，定位到下一个非空字符之前
        var pos = nextNewline + 1;
        while (pos < novel.Length && novel[pos] == '\n')
            pos++;

        return pos;
    }

    /// <summary>
    /// 解析 JSON 响应，返回带定位位置的插入指令列表。
    /// 无法定位的 anchor 位置标记为 -1。
    /// </summary>
    private static List<Insertion> ParseInsertions(string json, Action<string>? log)
    {
        var result = new List<Insertion>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        var sanitized = SanitizeJson(json);
        if (string.IsNullOrWhiteSpace(sanitized))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(sanitized);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var anchor = item.GetProperty("anchor").GetString() ?? "";
                var insertBefore = item.GetProperty("insert_before").GetString() ?? "";
                var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(anchor) || string.IsNullOrWhiteSpace(insertBefore))
                    continue;

                result.Add(new Insertion(anchor, insertBefore, type, -1));
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[SectionSplitter] JSON 解析失败: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 去除 Markdown 代码块标记和多余空白，提取 JSON 数组。
    /// </summary>
    private static string SanitizeJson(string json)
    {
        var text = json.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            text = text[3..].TrimStart();
            var end = text.IndexOf("```", 0, StringComparison.Ordinal);
            if (end >= 0)
                text = text[..end].TrimEnd();
        }

        var start = text.IndexOf("[", StringComparison.Ordinal);
        var endBracket = text.LastIndexOf("]", StringComparison.Ordinal);
        if (start >= 0 && endBracket > start)
            text = text[start..(endBracket + 1)];

        return text;
    }

    /// <summary>
    /// 规范化插入内容：确保末尾有两个换行。
    /// </summary>
    private static string NormalizeInsertion(string insertBefore)
    {
        var text = insertBefore.Trim();
        if (!text.EndsWith('\n')) text += "\n";
        if (!text.EndsWith("\n\n", StringComparison.Ordinal)) text += "\n";
        return text;
    }

    private record BgTransition(
        int EntryIndex,
        string PrevChar,
        string CurrChar,
        string PrevDialog,
        string CurrDialog);

    private record Snippet(int Position, string Text, BgTransition Transition);

    private record Insertion(string Anchor, string InsertBefore, string Type, int Position);
}
