using System.Text.Json;
using System.Text.RegularExpressions;
using ArkPlot.Core.Model;

namespace ArkPlot.Novelizer;

/// <summary>
/// 小说化管线：读 Markdown → 拆章 → 逐章调 LLM → 合并写小说文件
/// </summary>
public partial class NovelizerPipeline
{
    private readonly BailianClient _client;
    private readonly BailianConfig _config;
    private readonly Action<string>? _onLog;

    private const string SystemPrompt = """
        你是一位精通明日方舟世界观的资深小说家。
        请将输入的剧情脚本转化为连贯、流畅的小说叙述。
        要求：
        - 保持游戏原文的角色对话核心内容不变
        - 将舞台指示（立绘变化、背景切换、音乐提示、音效等）自然地融入叙事
        - 对话之间补充恰当的衔接描写（动作、心理、环境）
        - 语气符合明日方舟冷峻、克制的文学风格
        - 用第三人称叙述
        - 直接输出小说正文，不要前缀说明、不要后缀总结
        """;

    [GeneratedRegex(@"^(?=## )", RegexOptions.Multiline)]
    private static partial Regex ChapterSplitRegex();

    /// <param name="onLog">可选日志回调，同时写入 Console 和此回调（用于 Avalonia UI 同步）</param>
    public NovelizerPipeline(BailianClient client, BailianConfig config, Action<string>? onLog = null)
    {
        _client = client;
        _config = config;
        _onLog = onLog;
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
        var mdContent = File.ReadAllText(mdPath);
        var novelPath = ChapterCache.GetNovelPath(mdPath, model);

        var processed = MarkdownBuilder.PreprocessMdContent(mdContent);

        // 拆章
        var rawChapters = ChapterSplitRegex().Split(processed)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        Log($"\n{'='*60}");
        Log($"📖 模型: {model}");
        Log($"📄 输入: {Path.GetFileName(mdPath)} → 共 {rawChapters.Count} 章");
        Log($"📝 输出: {Path.GetFileName(novelPath)}");
        Log($"{'='*60}");

        var allParts = new List<string>();
        int totalPrompt = 0, totalCompletion = 0;

        for (int i = 0; i < rawChapters.Count; i++)
        {
            var chunk = rawChapters[i];
            // 第一行是 ## 标题
            var lines = chunk.Split('\n', 2);
            var title = lines[0].TrimStart('#', ' ').Trim();
            var body = lines.Length > 1 ? lines[1].Trim() : "";

            if (string.IsNullOrEmpty(body))
            {
                Log($"⏭️  第 {i + 1}/{rawChapters.Count} 章「{title}」无正文，跳过。");
                continue;
            }

            Log($"\n--- 第 {i + 1}/{rawChapters.Count} 章: {title} ({body.Length} 字符) ---");

            try
            {
                var result = await _client.ChatAsync(model, SystemPrompt, body);

                allParts.Add($"## {title}\n\n{result.AnswerContent}");

                if (result.Usage is not null)
                {
                    totalPrompt += result.Usage.PromptTokens;
                    totalCompletion += result.Usage.CompletionTokens;
                    Log($"✅ Token: 入 {result.Usage.PromptTokens} / 出 {result.Usage.CompletionTokens}");
                }
            }
            catch (BailianException ex)
            {
                LogError($"❌ 第 {i + 1} 章失败: {ex.Message}");
                allParts.Add($"## {title}\n\n> *（本章生成失败：{ex.Message}）*");
            }
        }

        File.WriteAllText(novelPath, string.Join("\n\n", allParts));

        Log($"\n{'='*60}");
        Log($"📊 总计 Token: 入 {totalPrompt} / 出 {totalCompletion} / 共 {totalPrompt + totalCompletion}");
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

        Log($"\n{'='*60}");
        Log($"📖 模型: {model}");
        Log($"📄 来源: {sourceLabel ?? "(entries)"} ({novelInput.Length} 字符)");
        Log($"📝 输出: {Path.GetFileName(outputPath)}");
        Log($"{'='*60}");

        var result = await _client.ChatAsync(model, SystemPrompt, novelInput);

        File.WriteAllText(outputPath, result.AnswerContent);

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
        var cache = new ChapterCache(outputDir);

        var mdFiles = Directory.GetFiles(inputDir, "*.md", SearchOption.TopDirectoryOnly);
        if (mdFiles.Length == 0)
        {
            Log($"❌ 目录中没有 .md 文件: {inputDir}");
            return;
        }

        Log($"📂 发现 {mdFiles.Length} 个 .md 文件");

        foreach (var mdFile in mdFiles.OrderBy(f => f))
        {
            foreach (var model in models)
            {
                var cached = cache.Check(mdFile, model, force);
                if (cached is not null)
                {
                    Log($"⏭️  跳过（缓存命中）: {Path.GetFileName(cached)}");
                    continue;
                }

                try
                {
                    await ProcessMdFileAsync(mdFile, model, outputDir);
                    cache.Update(mdFile, model);
                }
                catch (BailianException ex)
                {
                    LogError($"❌ [{model}] 失败: {ex.Message}");
                    var failedLog = Path.Combine(outputDir, "failed.txt");
                    File.AppendAllText(failedLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{mdFile}\t{model}\t{ex.Message}\n");
                }
            }
        }

        Log("\n🏁 批处理完成");
    }
}