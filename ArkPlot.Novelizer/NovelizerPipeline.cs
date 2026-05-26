using ArkPlot.Core.Model;

namespace ArkPlot.Novelizer;

/// <summary>
/// 小说化管线：读 Markdown → 拆章 → 逐章调 LLM → 合并写小说文件
/// </summary>
public class NovelizerPipeline
{
    private readonly BailianClient _client;
    private readonly ApiConfig _config;
    private readonly Action<string>? _onLog;
    private readonly string _systemPrompt;

    private const string DefaultSystemPrompt = """
        你是一位精通明日方舟世界观的资深小说家。
        请将输入的剧情脚本转化为连贯、流畅的小说叙述。
        文本要求：
        - 保持游戏原文的角色对话核心内容不变
        - 将舞台指示（立绘变化、背景切换、音乐提示、音效等）自然地融入叙事
        - 对话之间补充恰当的衔接描写（动作、心理、环境）
        - 语气符合明日方舟冷峻、克制的文学风格
        - 用第三人称叙述
        - 直接输出小说正文，不要前缀说明、不要后缀总结
        格式要求：
        - 输出时不许带任何# （标题）,无论几级都不需要
        - 对于`音乐`，你可以结合全文，构建出相应的气氛。音乐永远不会出现在剧情里。
        """;

    /// <param name="onLog">可选日志回调，同时写入 Console 和此回调（用于 Avalonia UI 同步）</param>
    /// <param name="systemPrompt">可选的自定义系统提示词，未提供时使用默认值</param>
    public NovelizerPipeline(BailianClient client, ApiConfig config, Action<string>? onLog = null, string? systemPrompt = null)
    {
        _client = client;
        _config = config;
        _onLog = onLog;
        _systemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? DefaultSystemPrompt : systemPrompt;
    }

    private void Log(string msg)
    {
        Console.WriteLine(msg);
        _onLog?.Invoke(msg);
    }

    private void LogError(string msg)
    {
        Console.Error.WriteLine(msg);
        _onLog?.Invoke(msg);
    }

    /// <summary>
    /// 处理一个 .md 文件：
    /// 1. 读取 + 预处理（去 HTML）
    /// 2. 按 ## 标题拆成独立章节
    /// 3. 每一章单独调 LLM
    /// 4. 所有章节合并成一个小说文件
    /// </summary>
    public async Task<string> ProcessMdFileAsync(string mdPath, string model, string outputDir)
    {
        Log($"[DIAG] ProcessMdFileAsync 开始。file={Path.GetFileName(mdPath)}, model={model}");

        Log($"[DIAG] 读取文件...");
        var mdContent = File.ReadAllText(mdPath);
        Log($"[DIAG] 文件读取完成，{mdContent.Length} 字符");

        Log($"[DIAG] 预处理（去 HTML）...");
        var processed = MarkdownBuilder.PreprocessMdContent(mdContent);
        Log($"[DIAG] 预处理完成，{processed.Length} 字符（原始 {mdContent.Length}）");

        // 拆章
        Log($"[DIAG] 按 ## 标题拆章...");
        var chapters = ChapterSplitter.SplitChapters(processed);
        Log($"[DIAG] 拆分为 {chapters.Count} 章");

        Log($"\n{'=' * 60}");
        Log($"📖 模型: {model}");
        Log($"📄 输入: {Path.GetFileName(mdPath)} → 共 {chapters.Count} 章");
        Log($"📝 输出: {Path.GetFileName(NovelComposer.GetNovelPath(mdPath, model))}");
        Log($"{'=' * 60}");

        // 处理所有章节
        var processor = new ChapterProcessor(_client, _systemPrompt, Log, LogError);
        var results = await processor.ProcessAllAsync(chapters, model);

        // 组装并写入
        var novelPath = NovelComposer.ComposeAndWrite(results, mdPath, model, Log);

        var tracker = new TokenTracker();
        foreach (var r in results)
        {
            // Token 统计已在 ChapterProcessor 中输出
        }

        Log($"\n{'=' * 60}");
        Log($"✅ 已保存: {novelPath}\n");

        return novelPath;
    }

    /// <summary>
    /// 从 FormattedTextEntry 数组构建输入 → 调 LLM → 写小说。
    /// 适用于从 JSON 反序列化后直接调用的场景。
    /// </summary>
    public async Task<string> ProcessEntriesAsync(
        IReadOnlyList<FormattedTextEntry> entries, string model, string outputPath,
        string? sourceLabel = null)
    {
        var novelInput = MarkdownBuilder.BuildNovelInput(entries);

        Log($"\n{'=' * 60}");
        Log($"📖 模型: {model}");
        Log($"📄 来源: {sourceLabel ?? "(entries)"} ({novelInput.Length} 字符)");
        Log($"📝 输出: {Path.GetFileName(outputPath)}");
        Log($"{'=' * 60}");

        var result = await _client.ChatAsync(model, _systemPrompt, novelInput);

        File.WriteAllText(outputPath, ChapterSplitter.StripHeadings(result.AnswerContent));

        if (result.Usage is not null)
        {
            Log($"📊 Token: 入 {result.Usage.PromptTokens} / 出 {result.Usage.CompletionTokens} / 共 {result.Usage.TotalTokens}");
        }
        Log($"✅ 已保存: {outputPath}\n");

        return outputPath;
    }

    /// <summary>
    /// 批量处理目录下所有 .md 文件
    /// </summary>
    public async Task BatchProcessAsync(
        string inputDir, string[] models, bool force, string? outputDir = null)
    {
        outputDir ??= inputDir;
        Log($"[DIAG] BatchProcessAsync 开始。dir={inputDir}, models=[{string.Join(", ", models)}], force={force}");

        var cache = new ChapterCache(outputDir);

        Log($"[DIAG] 扫描 .md 文件: {inputDir}");
        var mdFiles = Directory.GetFiles(inputDir, "*.md", SearchOption.TopDirectoryOnly);
        if (mdFiles.Length == 0)
        {
            Log($"❌ 目录中没有 .md 文件: {inputDir}");
            Log($"[DIAG] 无 .md 文件，BatchProcessAsync 返回");
            return;
        }

        Log($"📂 发现 {mdFiles.Length} 个 .md 文件");
        Log($"[DIAG] 文件列表: {string.Join(", ", mdFiles.Select(Path.GetFileName))}");

        foreach (var mdFile in mdFiles.OrderBy(f => f))
        {
            var fn = Path.GetFileName(mdFile);
            foreach (var model in models)
            {
                Log($"[DIAG] Batch 处理: file={fn}, model={model}");

                var cached = cache.Check(mdFile, model, force);
                if (cached is not null)
                {
                    Log($"⏭️  跳过（缓存命中）: {Path.GetFileName(cached)}");
                    Log($"[DIAG] 缓存命中，跳过: {fn}");
                    continue;
                }

                Log($"[DIAG] 调用 ProcessMdFileAsync: {fn}, {model}");
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    await ProcessMdFileAsync(mdFile, model, outputDir);
                    sw.Stop();
                    Log($"[DIAG] ProcessMdFileAsync 返回成功，{fn} 耗时 {sw.Elapsed.TotalSeconds:F1}s");
                    cache.Update(mdFile, model);
                }
                catch (BailianException ex)
                {
                    Log($"[DIAG] ProcessMdFileAsync 抛出 BailianException: {fn}, {model}, {ex.Message}");
                    LogError($"❌ [{model}] 失败: {ex.Message}");
                    var failedLog = Path.Combine(outputDir, "failed.txt");
                    File.AppendAllText(failedLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{mdFile}\t{model}\t{ex.Message}\n");
                }
            }
        }

        Log("\n🏁 批处理完成");
        Log("[DIAG] BatchProcessAsync 执行完毕，即将生成 epub");

        // 为每个小说 md 生成 epub
        await GenerateEpubsForNovelsAsync(outputDir);
    }

    /// <summary>
    /// 为目录下所有小说 MD 生成 epub
    /// </summary>
    private async Task GenerateEpubsForNovelsAsync(string outputDir)
    {
        try
        {
            var novelMdFiles = Directory.GetFiles(outputDir, "*_novel_*.md");
            if (novelMdFiles.Length > 0)
            {
                Log($"[DIAG] 找到 {novelMdFiles.Length} 个小说 MD，开始生成 epub");
                foreach (var mdPath in novelMdFiles)
                {
                    var title = Path.GetFileNameWithoutExtension(mdPath);
                    var epubPath = await PandocService.GenerateEpubAsync(mdPath, title);
                    if (epubPath != null)
                    {
                        Log($"📚 已生成 epub: {Path.GetFileName(epubPath)}");
                    }
                    else
                    {
                        Log($"⚠️  epub 生成失败或 pandoc 不可用: {Path.GetFileName(mdPath)}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[DIAG] epub 生成过程异常: {ex.Message}");
        }
    }
}
