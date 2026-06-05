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
