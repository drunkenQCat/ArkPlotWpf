using Xunit;

namespace ArkPlot.Novelizer.Tests;

public class PromptComparisonTests
{
    private static readonly string ProjectRoot = FindProjectRoot();
    private static readonly string PromptsDir = Path.Combine(ProjectRoot, "ArkPlot.Novelizer.Tests", "prompts");
    private static readonly string OutputDir = Path.Combine(ProjectRoot, "ArkPlot.Novelizer.Tests", "output");
    private static readonly string InputFile = Path.Combine(ProjectRoot, "example_pre_novel.md");

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "ArkPlot.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Cannot find ArkPlot.sln");
    }

    private static (BailianClient client, ApiConfig config) CreateClient()
    {
        var apiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            var envPath = Path.Combine(ProjectRoot, ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                    var eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = trimmed[..eq].Trim();
                    var value = trimmed[(eq + 1)..].Trim();
                    if (key == "DASHSCOPE_API_KEY" && string.IsNullOrEmpty(apiKey))
                        apiKey = value;
                }
            }
        }

        var config = new ApiConfig
        {
            Provider = ApiProvider.Bailian,
            ApiKey = apiKey,
            BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
            Models = ["deepseek-v4-flash"],
            EnableThinking = true,
            MaxTokens = 16384,
            TimeoutSeconds = 300
        };

        var http = new HttpClient();
        var client = new BailianClient(http, config, msg => Console.WriteLine(msg));
        return (client, config);
    }

    /// <summary>
    /// 对 prompts/ 目录下所有 prompt 文件逐一调用 LLM，输出写入 output/
    /// 已有输出的 prompt 跳过（缓存）
    /// </summary>
    [Fact]
    public async Task RunAllPrompts()
    {
        Directory.CreateDirectory(OutputDir);

        var rawMd = File.ReadAllText(InputFile);
        var preprocessed = MarkdownBuilder.PreprocessMdContent(rawMd);
        Console.WriteLine($"Input: {preprocessed.Length} chars (from {rawMd.Length})");

        var (client, _) = CreateClient();
        const string model = "deepseek-v4-flash";

        var promptFiles = Directory.GetFiles(PromptsDir, "*.md").OrderBy(f => f).ToArray();
        Console.WriteLine($"Found {promptFiles.Length} prompts");

        foreach (var promptFile in promptFiles)
        {
            var name = Path.GetFileNameWithoutExtension(promptFile);
            var outputPath = Path.Combine(OutputDir, $"{name}_flash.md");

            if (File.Exists(outputPath))
            {
                Console.WriteLine($"[SKIP] {name} — output already exists");
                continue;
            }

            Console.WriteLine($"[RUN]  {name}");
            var systemPrompt = File.ReadAllText(promptFile);
            var result = await client.ChatAsync(model, systemPrompt, preprocessed);

            var output = ChapterSplitter.StripHeadings(result.AnswerContent);
            File.WriteAllText(outputPath, output);

            Console.WriteLine($"[DONE] {name} — {output.Length} chars, tokens: {result.Usage?.TotalTokens}");
        }

        Console.WriteLine("\nAll prompts processed. Outputs in: " + OutputDir);
    }

    /// <summary>
    /// 用 champion 09 prompt 对多个模型各跑 3 次，对比覆盖率和质量
    /// 输出写入 output/models/{model}_{run}.md
    /// </summary>
    [Fact]
    public async Task CompareModels()
    {
        var modelsDir = Path.Combine(OutputDir, "models");
        Directory.CreateDirectory(modelsDir);

        var rawMd = File.ReadAllText(InputFile);
        var preprocessed = MarkdownBuilder.PreprocessMdContent(rawMd);
        var championPrompt = File.ReadAllText(Path.Combine(PromptsDir, "09_rephrase_portraits.md"));

        var (client, _) = CreateClient();

        string[] models = ["deepseek-v4-flash", "MiniMax-M2.5", "kimi-k2.5", "qwen3.7-plus", "qwen3.6-flash"];
        const int runsPerModel = 3;

        Console.WriteLine($"Input: {preprocessed.Length} chars");
        Console.WriteLine($"Models: {string.Join(", ", models)}");
        Console.WriteLine($"Runs per model: {runsPerModel}");
        Console.WriteLine();

        foreach (var model in models)
        {
            Console.WriteLine($"=== {model} ===");
            for (int run = 1; run <= runsPerModel; run++)
            {
                var safeModelName = model.Replace('.', '_').Replace('-', '_');
                var outputPath = Path.Combine(modelsDir, $"{safeModelName}_run{run}.md");

                if (File.Exists(outputPath))
                {
                    Console.WriteLine($"  [SKIP] run {run} — already exists");
                    continue;
                }

                try
                {
                    Console.WriteLine($"  [RUN]  {model} run {run}/{runsPerModel}...");
                    var result = await client.ChatAsync(model, championPrompt, preprocessed);
                    var output = ChapterSplitter.StripHeadings(result.AnswerContent);
                    File.WriteAllText(outputPath, output);

                    var hasScene1 = output.Contains("社区居民") || output.Contains("社区小贩") || output.Contains("仙人掌");
                    var hasScene2 = output.Contains("艾拉") || output.Contains("雷内尔");
                    var hasScene3 = output.Contains("双月") || output.Contains("蹦极") || output.Contains("发射");

                    Console.WriteLine($"  [DONE] {output.Length} chars, tokens: {result.Usage?.TotalTokens}");
                    Console.WriteLine($"         Scene1(市场):{(hasScene1 ? "✅" : "❌")}  Scene2(艾拉):{(hasScene2 ? "✅" : "❌")}  Scene3(蹦极):{(hasScene3 ? "✅" : "❌")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [FAIL] {model} run {run}: {ex.Message}");
                    File.WriteAllText(outputPath, $"ERROR: {ex.Message}");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine("All models tested. Outputs in: " + modelsDir);
    }

    /// <summary>
    /// Round 1 验证：用 bootstrap 孤星第一章 md 测试新 prompt 的覆盖率
    /// </summary>
    [Fact]
    public async Task VerifyRound1_GuxingCoverage()
    {
        var bootstrapDir = Path.Combine(ProjectRoot, "ArkPlot.Novelizer.Tests", "bootstrap", "current_prompts");
        var mdPath = Path.Combine(bootstrapDir, "孤星", "孤星.md");
        var verifyDir = Path.Combine(OutputDir, "verify_round1");
        Directory.CreateDirectory(verifyDir);

        var rawMd = File.ReadAllText(mdPath);
        var preprocessed = MarkdownBuilder.PreprocessMdContent(rawMd);
        var chapters = ChapterSplitter.SplitChapters(preprocessed);

        Console.WriteLine($"孤星 md: {rawMd.Length} chars, preprocessed: {preprocessed.Length} chars, chapters: {chapters.Count}");

        // 取内容最多的章节（第一章可能是空标题）
        var chapter = chapters.OrderByDescending(c => c.Body.Length).First();
        Console.WriteLine($"Chapter: {chapter.Title}, body: {chapter.Body.Length} chars");

        var (client, _) = CreateClient();
        const string model = "deepseek-v4-flash";

        // 从 NovelizerPipeline.cs 源码提取当前 DefaultSystemPrompt
        var pipelineSourcePath = Path.Combine(ProjectRoot, "ArkPlot.Novelizer", "NovelizerPipeline.cs");
        var source = File.ReadAllText(pipelineSourcePath);
        var startMarker = "private const string DefaultSystemPrompt = \"\"\"";
        var endMarker = "\"\"\";";
        var startIdx = source.IndexOf(startMarker) + startMarker.Length;
        var endIdx = source.IndexOf(endMarker, startIdx);
        var systemPrompt = source[startIdx..endIdx].Trim();
        Console.WriteLine($"Prompt length: {systemPrompt.Length} chars");

        Console.WriteLine($"[RUN] Calling deepseek-v4-flash with chapter body ({chapter.Body.Length} chars)...");
        var result = await client.ChatAsync(model, systemPrompt, chapter.Body);
        var output = ChapterSplitter.StripHeadings(result.AnswerContent);

        var outputPath = Path.Combine(verifyDir, "guxing_ch1_new_prompt.md");
        File.WriteAllText(outputPath, output);
        Console.WriteLine($"[DONE] {output.Length} chars, tokens: {result.Usage?.TotalTokens}");

        // 检查关键场景覆盖
        var checks = new Dictionary<string, string[]>
        {
            ["监狱走廊"] = ["小贾斯汀", "监狱负责人", "米诺斯", "汐斯塔", "病入膏肓"],
            ["实验室"] = ["三十号", "洛肯", "保命计划"],
            ["杰克逊演讲"] = ["杰克逊", "副总统", "开拓精神"],
            ["缪尔赛思"] = ["缪尔赛思", "霍尔海雅"],
        };

        Console.WriteLine("\n场景覆盖检查:");
        foreach (var (scene, keywords) in checks)
        {
            var found = keywords.Count(k => output.Contains(k));
            var total = keywords.Length;
            var status = found >= total / 2 ? "✅" : "❌";
            Console.WriteLine($"  {status} {scene}: {found}/{total} 关键词命中 [{string.Join(", ", keywords.Select(k => output.Contains(k) ? k : $"({k})"))}]");
        }

        // 与旧输出对比
        var oldNovelPath = Path.Combine(bootstrapDir, "孤星", "孤星_novel_MiniMax-M2.5.md");
        if (File.Exists(oldNovelPath))
        {
            var oldOutput = File.ReadAllText(oldNovelPath);
            Console.WriteLine($"\n旧输出 (MiniMax-M2.5): {oldOutput.Length} chars");
            Console.WriteLine("旧输出场景覆盖:");
            foreach (var (scene, keywords) in checks)
            {
                var found = keywords.Count(k => oldOutput.Contains(k));
                var total = keywords.Length;
                var status = found >= total / 2 ? "✅" : "❌";
                Console.WriteLine($"  {status} {scene}: {found}/{total}");
            }
        }
    }

    /// <summary>
    /// Round 3 交叉验证：用辞岁行测试 prompt 的泛化能力
    /// </summary>
    [Fact]
    public async Task VerifyRound3_CisuihangCoverage()
    {
        var bootstrapDir = Path.Combine(ProjectRoot, "ArkPlot.Novelizer.Tests", "bootstrap", "current_prompts");
        var mdPath = Path.Combine(bootstrapDir, "辞岁行", "辞岁行.md");
        var verifyDir = Path.Combine(OutputDir, "verify_round3");
        Directory.CreateDirectory(verifyDir);

        var rawMd = File.ReadAllText(mdPath);
        var preprocessed = MarkdownBuilder.PreprocessMdContent(rawMd);
        var chapters = ChapterSplitter.SplitChapters(preprocessed);

        Console.WriteLine($"辞岁行 md: {rawMd.Length} chars, preprocessed: {preprocessed.Length} chars, chapters: {chapters.Count}");

        var chapter = chapters.OrderByDescending(c => c.Body.Length).First();
        Console.WriteLine($"Chapter: {chapter.Title}, body: {chapter.Body.Length} chars");

        var (client, _) = CreateClient();
        const string model = "deepseek-v4-flash";

        var pipelineSourcePath = Path.Combine(ProjectRoot, "ArkPlot.Novelizer", "NovelizerPipeline.cs");
        var source = File.ReadAllText(pipelineSourcePath);
        var startMarker = "private const string DefaultSystemPrompt = \"\"\"";
        var endMarker = "\"\"\";";
        var startIdx = source.IndexOf(startMarker) + startMarker.Length;
        var endIdx = source.IndexOf(endMarker, startIdx);
        var systemPrompt = source[startIdx..endIdx].Trim();
        Console.WriteLine($"Prompt length: {systemPrompt.Length} chars");

        Console.WriteLine($"[RUN] Calling deepseek-v4-flash with chapter body ({chapter.Body.Length} chars)...");
        var result = await client.ChatAsync(model, systemPrompt, chapter.Body);
        var output = ChapterSplitter.StripHeadings(result.AnswerContent);

        var outputPath = Path.Combine(verifyDir, "cisuihang_ch1_new_prompt.md");
        File.WriteAllText(outputPath, output);
        Console.WriteLine($"[DONE] {output.Length} chars, tokens: {result.Usage?.TotalTokens}");

        // 辞岁行关键词检查
        var checks = new Dictionary<string, string[]>
        {
            ["开篇"] = ["璟", "娲", "大炎"],
            ["魏彦吾/龙门"] = ["魏彦吾", "龙门", "熊sir"],
            ["太尉"] = ["太尉", "陛下", "先帝"],
            ["勾吴"] = ["勾吴", "循兽", "蜃景"],
        };

        Console.WriteLine("\n场景覆盖检查:");
        foreach (var (scene, keywords) in checks)
        {
            var found = keywords.Count(k => output.Contains(k));
            var total = keywords.Length;
            var status = found >= total / 2 ? "✅" : "❌";
            Console.WriteLine($"  {status} {scene}: {found}/{total} 关键词命中 [{string.Join(", ", keywords.Select(k => output.Contains(k) ? k : $"({k})"))}]");
        }

        // 模板化表达检查
        var badPhrases = new[] { "仿佛正递出一枚无法言说的契约", "如未及收拢的密信", "沉静如深潭", "站在空无一物的背景前", "站在空茫的背景里" };
        Console.WriteLine("\n模板化表达检查:");
        foreach (var phrase in badPhrases)
        {
            var found = output.Contains(phrase);
            Console.WriteLine($"  {(found ? "❌ 仍照抄" : "✅ 已消除")}: \"{phrase}\"");
        }

        // 与旧输出对比
        var oldNovelPath = Path.Combine(bootstrapDir, "辞岁行", "辞岁行_novel_MiniMax-M2.5.md");
        if (File.Exists(oldNovelPath))
        {
            var oldOutput = File.ReadAllText(oldNovelPath);
            Console.WriteLine($"\n旧输出 (MiniMax-M2.5): {oldOutput.Length} chars");
            Console.WriteLine("旧输出场景覆盖:");
            foreach (var (scene, keywords) in checks)
            {
                var found = keywords.Count(k => oldOutput.Contains(k));
                var total = keywords.Length;
                var status = found >= total / 2 ? "✅" : "❌";
                Console.WriteLine($"  {status} {scene}: {found}/{total}");
            }
        }
    }

    /// <summary>
    /// 只跑指定的 prompt（通过 PROMPT_NAME 环境变量）
    /// </summary>
    [Fact]
    public async Task RunSinglePrompt()
    {
        var promptName = Environment.GetEnvironmentVariable("PROMPT_NAME") ?? "";
        if (string.IsNullOrEmpty(promptName))
        {
            Console.WriteLine("Set PROMPT_NAME env var to specify which prompt to run (without .md extension)");
            return;
        }

        Directory.CreateDirectory(OutputDir);

        var promptFile = Path.Combine(PromptsDir, $"{promptName}.md");
        if (!File.Exists(promptFile))
        {
            Console.WriteLine($"Prompt not found: {promptFile}");
            return;
        }

        var rawMd = File.ReadAllText(InputFile);
        var preprocessed = MarkdownBuilder.PreprocessMdContent(rawMd);

        var (client, _) = CreateClient();
        const string model = "deepseek-v4-flash";
        var systemPrompt = File.ReadAllText(promptFile);

        Console.WriteLine($"[RUN] {promptName} — prompt {systemPrompt.Length} chars, input {preprocessed.Length} chars");
        var result = await client.ChatAsync(model, systemPrompt, preprocessed);
        var output = ChapterSplitter.StripHeadings(result.AnswerContent);

        var outputPath = Path.Combine(OutputDir, $"{promptName}_flash.md");
        File.WriteAllText(outputPath, output);
        Console.WriteLine($"[DONE] {promptName} — {output.Length} chars, tokens: {result.Usage?.TotalTokens}");
    }
}
