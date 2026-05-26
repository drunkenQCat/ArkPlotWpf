namespace ArkPlot.Novelizer;

/// <summary>
/// 章节处理器：负责单章 LLM 调用、并发控制、错误处理
/// </summary>
public class ChapterProcessor
{
    private readonly BailianClient _client;
    private readonly string _systemPrompt;
    private readonly Action<string> _log;
    private readonly Action<string> _logError;
    private readonly int _maxConcurrency;

    public ChapterProcessor(
        BailianClient client,
        string systemPrompt,
        Action<string> log,
        Action<string> logError,
        int maxConcurrency = 3)
    {
        _client = client;
        _systemPrompt = systemPrompt;
        _log = log;
        _logError = logError;
        _maxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// 并发处理所有章节，返回按索引排序的处理结果
    /// </summary>
    public async Task<IReadOnlyList<ChapterResult>> ProcessAllAsync(
        IReadOnlyList<Chapter> chapters,
        string model)
    {
        var semaphore = new SemaphoreSlim(_maxConcurrency);
        var results = new Dictionary<int, ChapterResult>();
        var tasks = new List<Task>();
        var tokenTracker = new TokenTracker();

        foreach (var chapter in chapters)
        {
            if (string.IsNullOrEmpty(chapter.Body))
            {
                _log($"⏭️  第 {chapter.Index + 1}/{chapters.Count} 章「{chapter.Title}」无正文，跳过。");
                results[chapter.Index] = ChapterResult.Skipped(chapter.Index, chapter.Title, chapters.Count);
                continue;
            }

            tasks.Add(ProcessChapterAsync(chapter, chapters.Count, model, semaphore, results, tokenTracker));
        }

        await Task.WhenAll(tasks);

        return results.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
    }

    private async Task ProcessChapterAsync(
        Chapter chapter,
        int totalCount,
        string model,
        SemaphoreSlim semaphore,
        Dictionary<int, ChapterResult> results,
        TokenTracker tokenTracker)
    {
        await semaphore.WaitAsync();
        try
        {
            _log($"\n--- 第 {chapter.Index + 1}/{totalCount} 章: {chapter.Title} ({chapter.Body.Length} 字符) ---");
            _log($"[DIAG] 即将调用 ChatAsync for 第 {chapter.Index + 1} 章「{chapter.Title}」");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var chatResult = await _client.ChatAsync(model, _systemPrompt, chapter.Body);
                sw.Stop();
                _log($"[DIAG] ChatAsync 返回，耗时 {sw.Elapsed.TotalSeconds:F1}s");

                var strippedContent = ChapterSplitter.StripHeadings(chatResult.AnswerContent);
                results[chapter.Index] = ChapterResult.FromSuccess(
                    chapter.Index,
                    chapter.Title,
                    strippedContent,
                    totalCount);

                if (chatResult.Usage is not null)
                {
                    tokenTracker.Add(chatResult.Usage.PromptTokens, chatResult.Usage.CompletionTokens);
                    _log($"✅ Token: 入 {chatResult.Usage.PromptTokens} / 出 {chatResult.Usage.CompletionTokens}");
                    _log($"[DIAG] 第 {chapter.Index + 1} 章 token: prompt={chatResult.Usage.PromptTokens}, completion={chatResult.Usage.CompletionTokens}");
                }
            }
            catch (BailianException ex)
            {
                sw.Stop();
                _log($"[DIAG] ChatAsync 抛出 BailianException（{sw.Elapsed.TotalSeconds:F1}s）: {ex.Message}");
                _logError($"❌ 第 {chapter.Index + 1} 章失败: {ex.Message}");
                results[chapter.Index] = ChapterResult.FromFailure(chapter.Index, chapter.Title, ex.Message, totalCount);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
}

/// <summary>
/// 单章处理结果
/// </summary>
public record ChapterResult
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public int TotalChapters { get; init; }

    public static ChapterResult Skipped(int index, string title, int total) => new()
    {
        Index = index,
        Title = title,
        Content = $"## {title}\n\n> *（本章无正文）*",
        IsSuccess = true,
        TotalChapters = total
    };

    public static ChapterResult FromSuccess(int index, string title, string content, int total) => new()
    {
        Index = index,
        Title = title,
        Content = $"## {title}\n\n{content}",
        IsSuccess = true,
        TotalChapters = total
    };

    public static ChapterResult FromFailure(int index, string title, string error, int total) => new()
    {
        Index = index,
        Title = title,
        Content = $"## {title}\n\n> *（本章生成失败：{error}）*",
        IsSuccess = false,
        ErrorMessage = error,
        TotalChapters = total
    };
}

/// <summary>
/// Token 统计追踪器（线程安全）
/// </summary>
public class TokenTracker
{
    private readonly object _lock = new();
    public int TotalPromptTokens { get; private set; }
    public int TotalCompletionTokens { get; private set; }

    public void Add(int prompt, int completion)
    {
        lock (_lock)
        {
            TotalPromptTokens += prompt;
            TotalCompletionTokens += completion;
        }
    }
}
