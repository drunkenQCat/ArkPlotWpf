using ArkPlot.Vision;

namespace ArkPlot.Cli.Infrastructure;

/// <summary>
/// 创建视觉描述客户端（百炼优先，Ollama 回退）。
/// </summary>
public static class VisionClientFactory
{
    /// <summary>
    /// 尝试创建图片描述委托。返回 null 表示无可用视觉后端。
    /// </summary>
    public static VisionClientResult? Create()
    {
        // 优先使用百炼（直接传 URL，无需下载图片）
        var bailianApiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? "";
        if (!string.IsNullOrEmpty(bailianApiKey))
        {
            var config = new BailianVisionConfig
            {
                ApiKey = bailianApiKey,
                Model = "qwen3-vl-flash",
                TimeoutSeconds = 60,
                SystemPrompt = "请用中文详细描述这张图片中的所有视觉元素，包括角色、场景、动作、服饰、背景等细节。直接输出描述内容，不要加任何前缀或总结性语句。",
                MaxTokens = 2048
            };
            var client = new BailianVisionClient(config, onLog: msg => Console.WriteLine($"  [Vision] {msg}"));
            Console.WriteLine("    ✅ 百炼视觉客户端已初始化（qwen3-vl-flash，直接 URL 调用）");
            return new VisionClientResult(
                async url => await client.DescribeImageUrlAsync(url),
                client);
        }

        // 回退到 Ollama（需要下载图片）
        Console.WriteLine("    ⚠️ 未配置 DASHSCOPE_API_KEY，尝试使用 Ollama...");
        try
        {
            var visionConfig = new VisionConfig
            {
                BaseUrl = "http://localhost:11434",
                Model = "qwen3-vl:8b",
                TimeoutSeconds = 600,
                SystemPrompt = "请用中文详细描述这张图片中的所有视觉元素，包括角色、场景、动作、服饰、背景等细节。直接输出描述内容，不要加任何前缀或总结性语句。",
                Temperature = 0.7f,
                MaxTokens = 2048
            };
            var ollamaClient = new OllamaVisionClient(visionConfig, onLog: msg => Console.WriteLine($"  [Vision] {msg}"));
            Console.WriteLine("    ✅ Ollama 视觉客户端已初始化（需要下载图片）");

            return new VisionClientResult(
                async url =>
                {
                    var tempPath = Path.GetTempFileName();
                    try
                    {
                        using var http = new HttpClient();
                        http.Timeout = TimeSpan.FromMinutes(5);
                        var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        await using var fs = File.Create(tempPath);
                        await (await response.Content.ReadAsStreamAsync()).CopyToAsync(fs);
                        return await ollamaClient.DescribeImageAsync(tempPath);
                    }
                    finally
                    {
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }
                },
                ollamaClient);
        }
        catch
        {
            Console.WriteLine("    ⚠️ Ollama 不可用，将使用占位符模式。");
            return null;
        }
    }
}
