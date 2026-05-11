using System.Text.Json;
using ArkPlot.Core.Model;
using Microsoft.Extensions.Configuration;

namespace ArkPlot.Novelizer;

class Program
{
    static async Task<int> Main(string[] args)
    {
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
        var (input, compare, force) = ParseRunArgs(args);
        var config = LoadConfig();

        if (string.IsNullOrEmpty(config.ApiKey))
        {
            Console.Error.WriteLine("❌ 未配置 API Key。请在 appsettings.json 中设置 Bailian:ApiKey 或设置环境变量 DASHSCOPE_API_KEY");
            return 1;
        }

        var models = compare ? config.Models : [config.Models[0]];

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
                // JSON 格式：反序列化为 FormattedTextEntry[] 后处理
                var json = File.ReadAllText(input);
                var entries = JsonSerializer.Deserialize<List<FormattedTextEntry>>(json) ?? [];

                foreach (var model in models)
                {
                    var outputPath = Path.ChangeExtension(input, null) + $"_novel_{(model.Contains("flash") ? "flash" : "pro")}.md";
                    await pipeline.ProcessEntriesAsync(entries, model, outputPath, Path.GetFileName(input));
                }
            }
            else if (input.EndsWith(".md"))
            {
                var dir = Path.GetDirectoryName(input) ?? ".";
                var cache = new ChapterCache(dir);

                foreach (var model in models)
                {
                    var cached = cache.Check(input, model, force);
                    if (cached is not null)
                    {
                        Console.WriteLine($"⏭️  跳过（缓存命中）: {Path.GetFileName(cached)}");
                        continue;
                    }

                    try
                    {
                        await pipeline.ProcessMdFileAsync(input, model, dir);
                        cache.Update(input, model);
                    }
                    catch (BailianException ex)
                    {
                        Console.Error.WriteLine($"❌ [{model}] 失败: {ex.Message}");
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
        var input = args.FirstOrDefault();
        if (string.IsNullOrEmpty(input))
        {
            Console.Error.WriteLine("用法: Novelizer test <example_data.json>");
            return 1;
        }

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"❌ 文件不存在: {input}");
            return 1;
        }

        var config = LoadConfig();
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            Console.Error.WriteLine("❌ 未配置 API Key");
            return 1;
        }

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

        var proOutput = Path.ChangeExtension(input, null) + "_novel_pro.md";
        await pipeline.ProcessEntriesAsync(entries, config.Models[0], proOutput, Path.GetFileName(input));

        if (config.Models.Length > 1)
        {
            var flashOutput = Path.ChangeExtension(input, null) + "_novel_flash.md";
            await pipeline.ProcessEntriesAsync(entries, config.Models[1], flashOutput, Path.GetFileName(input));
        }

        return 0;
    }

    static BailianConfig LoadConfig()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var config = new BailianConfig();
        configuration.GetSection("Bailian").Bind(config);

        // 环境变量优先
        var envKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
        if (!string.IsNullOrEmpty(envKey))
            config.ApiKey = envKey;

        return config;
    }

    static (string input, bool compare, bool force) ParseRunArgs(string[] args)
    {
        var input = "";
        var compare = false;
        var force = false;

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
                default:
                    // 可能是位置参数
                    if (!args[i].StartsWith("-") && string.IsNullOrEmpty(input))
                        input = args[i];
                    break;
            }
        }

        return (input, compare, force);
    }

    static void PrintUsage()
    {
        Console.WriteLine("ArkPlot.Novelizer — 百炼 DeepSeek V4 小说化工具");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine("  Novelizer run --input <path> [--compare] [--force]");
        Console.WriteLine("  Novelizer test <example_data.json>");
        Console.WriteLine();
        Console.WriteLine("命令:");
        Console.WriteLine("  run   从 .md 文件或 .json (FormattedTextEntry[]) 生成小说");
        Console.WriteLine("  test  用 example_data.json 测试（生成 pro + flash 两个版本）");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  --input, -i   输入文件(.md/.json) 或目录");
        Console.WriteLine("  --compare, -c 并行调用 pro 和 flash 两个模型进行对比");
        Console.WriteLine("  --force, -f   忽略缓存，强制重新生成");
    }

    static int PrintUsageWithError(string error)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
        PrintUsage();
        return 1;
    }
}