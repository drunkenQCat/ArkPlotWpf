using System.Text.Json;
using ArkPlot.Core.Model;
using Microsoft.Extensions.Configuration;

namespace ArkPlot.Novelizer;

class Program
{
    static async Task<int> Main(string[] args)
    {
        LoadEnvFile();

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "run" => await RunAsync(args[1..]),
            "test" => await TestAsync(args[1..]),
            _ => PrintUsageWithError($"未知命令: {command}")
        };
    }

    static async Task<int> RunAsync(string[] args)
    {
        var (input, compare, force, model, provider) = ParseRunArgs(args);
        var config = LoadConfig(provider);

        if (string.IsNullOrEmpty(config.ApiKey))
        {
            Console.Error.WriteLine("❌ 未配置 API Key。请设置 DEEPSEEK_API_KEY 或 DASHSCOPE_API_KEY 环境变量");
            return 1;
        }

        var models = model is not null ? [model] : (compare ? config.Models : [config.Models[0]]);
        Console.WriteLine($"🔌 平台: {config.Provider}, 模型: {string.Join(", ", models)}");

        using var http = new HttpClient();
        var client = new BailianClient(http, config);
        var pipeline = new NovelizerPipeline(client, config);

        if (Directory.Exists(input))
        {
            await pipeline.BatchProcessAsync(input, models, force);
        }
        else if (File.Exists(input))
        {
            if (input.EndsWith(".json"))
            {
                var json = File.ReadAllText(input);
                var entries = JsonSerializer.Deserialize<List<FormattedTextEntry>>(json) ?? [];

                foreach (var m in models)
                {
                    var outputPath = Path.ChangeExtension(input, null) + $"_novel_{(m.Contains("flash") ? "flash" : "pro")}.md";
                    await pipeline.ProcessEntriesAsync(entries, m, outputPath, Path.GetFileName(input));
                }
            }
            else if (input.EndsWith(".md"))
            {
                var dir = Path.GetDirectoryName(input) ?? ".";
                var cache = new ChapterCache(dir);

                foreach (var m in models)
                {
                    var cached = cache.Check(input, m, force);
                    if (cached is not null)
                    {
                        Console.WriteLine($"⏭️  跳过（缓存命中）: {Path.GetFileName(cached)}");
                        continue;
                    }

                    try
                    {
                        await pipeline.ProcessMdFileAsync(input, m, dir);
                        cache.Update(input, m);
                    }
                    catch (BailianException ex)
                    {
                        Console.Error.WriteLine($"❌ [{m}] 失败: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.Error.WriteLine($"❌ 不支持的文件类型: {input}");
                return 1;
            }
        }
        else
        {
            Console.Error.WriteLine($"❌ 路径不存在: {input}");
            return 1;
        }

        return 0;
    }

    static async Task<int> TestAsync(string[] args)
    {
        var (input, _, _, model, provider) = ParseRunArgs(args);
        if (string.IsNullOrEmpty(input))
        {
            Console.Error.WriteLine("用法: Novelizer test <example_data.json> [--model flash|pro] [--provider deepseek|bailian]");
            return 1;
        }

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"❌ 文件不存在: {input}");
            return 1;
        }

        var config = LoadConfig(provider);
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            Console.Error.WriteLine("❌ 未配置 API Key");
            return 1;
        }

        Console.WriteLine($"🔌 平台: {config.Provider}");

        var json = File.ReadAllText(input);
        var entries = JsonSerializer.Deserialize<List<FormattedTextEntry>>(json) ?? [];
        Console.WriteLine($"✅ 已加载 {entries.Count} 条数据");

        var novelInput = MarkdownBuilder.BuildNovelInput(entries);
        Console.WriteLine($"📝 构建输入 ({novelInput.Length} 字符):");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine(novelInput);
        Console.WriteLine(new string('-', 40));

        using var http = new HttpClient();
        var client = new BailianClient(http, config);
        var pipeline = new NovelizerPipeline(client, config);

        if (model is not null)
        {
            // 单模型测试
            var m = model.Contains("flash") ? config.Models.Last(m => m.Contains("flash")) : config.Models.First(m => !m.Contains("flash"));
            var outputPath = Path.ChangeExtension(input, null) + $"_novel_{model}.md";
            await pipeline.ProcessEntriesAsync(entries, m, outputPath, Path.GetFileName(input));
        }
        else
        {
            // 双模型对比
            var proOutput = Path.ChangeExtension(input, null) + "_novel_pro.md";
            await pipeline.ProcessEntriesAsync(entries, config.Models[0], proOutput, Path.GetFileName(input));

            if (config.Models.Length > 1)
            {
                var flashOutput = Path.ChangeExtension(input, null) + "_novel_flash.md";
                await pipeline.ProcessEntriesAsync(entries, config.Models[1], flashOutput, Path.GetFileName(input));
            }
        }

        return 0;
    }

    static ApiConfig LoadConfig(string? providerOverride)
    {
        var dsKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "";
        var blKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? "";

        var provider = providerOverride?.ToLowerInvariant() switch
        {
            "deepseek" or "ds" => ApiProvider.DeepSeek,
            "bailian" or "bl" => ApiProvider.Bailian,
            _ => (ApiProvider?)null
        };

        if (provider is null)
        {
            // 未指定 provider，自动检测
            var bothAvailable = !string.IsNullOrEmpty(dsKey) && !string.IsNullOrEmpty(blKey);
            if (bothAvailable)
            {
                throw new InvalidOperationException(
                    "检测到 DEEPSEEK_API_KEY 和 DASHSCOPE_API_KEY 均已配置。请使用 --provider deepseek 或 --provider bailian 明确指定平台。");
            }

            if (!string.IsNullOrEmpty(dsKey))
                provider = ApiProvider.DeepSeek;
            else if (!string.IsNullOrEmpty(blKey))
                provider = ApiProvider.Bailian;
        }

        return provider switch
        {
            ApiProvider.DeepSeek => new ApiConfig
            {
                Provider = ApiProvider.DeepSeek,
                ApiKey = dsKey,
                BaseUrl = "https://api.deepseek.com"
            },
            ApiProvider.Bailian => new ApiConfig
            {
                Provider = ApiProvider.Bailian,
                ApiKey = blKey,
                BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1"
            },
            _ => new ApiConfig()
        };
    }

    static (string input, bool compare, bool force, string? model, string? provider) ParseRunArgs(string[] args)
    {
        var input = "";
        var compare = false;
        var force = false;
        string? model = null;
        string? provider = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--input" or "-i" when i + 1 < args.Length:
                    input = args[++i];
                    break;
                case "--compare" or "-c":
                    compare = true;
                    break;
                case "--force" or "-f":
                    force = true;
                    break;
                case "--model" or "-m" when i + 1 < args.Length:
                    model = args[++i].ToLowerInvariant();
                    break;
                case "--provider" or "-p" when i + 1 < args.Length:
                    provider = args[++i].ToLowerInvariant();
                    break;
                default:
                    if (!args[i].StartsWith("-") && string.IsNullOrEmpty(input))
                        input = args[i];
                    break;
            }
        }

        return (input, compare, force, model, provider);
    }

    /// <summary>
    /// 加载项目根目录下的 .env 文件，将 KEY=VALUE 行设为环境变量（不覆盖已存在的）
    /// </summary>
    static void LoadEnvFile()
    {
        // 从当前目录向上查找 .env
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var envPath = Path.Combine(dir, ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                        continue;
                    var eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = trimmed[..eq].Trim();
                    var value = trimmed[(eq + 1)..].Trim();
                    if (Environment.GetEnvironmentVariable(key) is null)
                        Environment.SetEnvironmentVariable(key, value);
                }
                Console.WriteLine($"📄 已加载 .env: {envPath}");
                return;
            }
            dir = Path.GetDirectoryName(dir);
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("ArkPlot.Novelizer — 百炼 / DeepSeek 小说化工具");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine("  Novelizer run --input <path> [--compare] [--force] [--model flash|pro] [--provider deepseek|bailian]");
        Console.WriteLine("  Novelizer test <example_data.json> [--model flash|pro] [--provider deepseek|bailian]");
        Console.WriteLine();
        Console.WriteLine("命令:");
        Console.WriteLine("  run   从 .md 文件或 .json (FormattedTextEntry[]) 生成小说");
        Console.WriteLine("  test  用 example_data.json 测试");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  --input, -i    输入文件(.md/.json) 或目录");
        Console.WriteLine("  --compare, -c  并行调用 pro 和 flash 两个模型进行对比");
        Console.WriteLine("  --force, -f    忽略缓存，强制重新生成");
        Console.WriteLine("  --model, -m    指定模型 (flash / pro)");
        Console.WriteLine("  --provider, -p 指定平台 (deepseek / bailian)");
        Console.WriteLine();
        Console.WriteLine("平台自动检测:");
        Console.WriteLine("  仅 DEEPSEEK_API_KEY     → 自动使用 DeepSeek (api.deepseek.com)");
        Console.WriteLine("  仅 DASHSCOPE_API_KEY    → 自动使用百炼 (dashscope.aliyuncs.com)");
        Console.WriteLine("  两者都配置              → 必须用 --provider 显式指定");
    }

    static int PrintUsageWithError(string error)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
        PrintUsage();
        return 1;
    }
}