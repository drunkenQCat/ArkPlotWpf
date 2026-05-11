using System.Text.Json;
using ArkPlot.Core.Model;

namespace ArkPlot.Novelizer;

/// <summary>
/// 小说化管线：读 Markdown → 调 LLM → 写小说文件
/// </summary>
public class NovelizerPipeline
{
    private readonly BailianClient _client;
    private readonly BailianConfig _config;

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

    public NovelizerPipeline(BailianClient client, BailianConfig config)
    {
        _client = client;
        _config = config;
    }

    /// <summary>
    /// 处理一个 .md 文件，生成小说
    /// </summary>
    public async Task<string> ProcessMdFileAsync(string mdPath, string model, string outputDir)
    {
        var mdContent = File.ReadAllText(mdPath);
        var novelPath = ChapterCache.GetNovelPath(mdPath, model);

        Console.WriteLine($"\n{'='*60}");
        Console.WriteLine($"📖 模型: {model}");
        Console.WriteLine($"📄 输入: {Path.GetFileName(mdPath)} ({mdContent.Length} 字符)");

        var processedContent = MarkdownBuilder.PreprocessMdContent(mdContent);
        Console.WriteLine($"🔧 预处理后: {processedContent.Length} 字符（去除 HTML 标签等）");
        Console.WriteLine($"📝 输出: {Path.GetFileName(novelPath)}");
        Console.WriteLine($"{'='*60}");

        var result = await _client.ChatAsync(model, SystemPrompt, processedContent);

        File.WriteAllText(novelPath, result.AnswerContent);

        if (result.Usage is not null)
        {
            Console.WriteLine($"📊 Token: 入 {result.Usage.PromptTokens} / 出 {result.Usage.CompletionTokens} / 共 {result.Usage.TotalTokens}");
        }
        Console.WriteLine($"✅ 已保存: {novelPath}\n");

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

        Console.WriteLine($"\n{'='*60}");
        Console.WriteLine($"📖 模型: {model}");
        Console.WriteLine($"📄 来源: {sourceLabel ?? "(entries)"} ({novelInput.Length} 字符)");
        Console.WriteLine($"📝 输出: {Path.GetFileName(outputPath)}");
        Console.WriteLine($"{'='*60}");

        var result = await _client.ChatAsync(model, SystemPrompt, novelInput);

        File.WriteAllText(outputPath, result.AnswerContent);

        if (result.Usage is not null)
        {
            Console.WriteLine($"📊 Token: 入 {result.Usage.PromptTokens} / 出 {result.Usage.CompletionTokens} / 共 {result.Usage.TotalTokens}");
        }
        Console.WriteLine($"✅ 已保存: {outputPath}\n");

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
            Console.WriteLine($"❌ 目录中没有 .md 文件: {inputDir}");
            return;
        }

        Console.WriteLine($"📂 发现 {mdFiles.Length} 个 .md 文件");

        foreach (var mdFile in mdFiles.OrderBy(f => f))
        {
            foreach (var model in models)
            {
                var cached = cache.Check(mdFile, model, force);
                if (cached is not null)
                {
                    Console.WriteLine($"⏭️  跳过（缓存命中）: {Path.GetFileName(cached)}");
                    continue;
                }

                try
                {
                    await ProcessMdFileAsync(mdFile, model, outputDir);
                    cache.Update(mdFile, model);
                }
                catch (BailianException ex)
                {
                    Console.Error.WriteLine($"❌ [{model}] 失败: {ex.Message}");
                    var failedLog = Path.Combine(outputDir, "failed.txt");
                    File.AppendAllText(failedLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{mdFile}\t{model}\t{ex.Message}\n");
                }
            }
        }

        Console.WriteLine("\n🏁 批处理完成");
    }
}