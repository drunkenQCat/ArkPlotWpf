using ArkPlot.Vision;

// 示例：使用 Ollama qwen3-vl:8b 描述图片
Console.WriteLine("=== ArkPlot.Vision 示例程序 ===\n");

// 检查图片是否存在
var imagePath = "test.jpg";
if (!File.Exists(imagePath))
{
    Console.WriteLine($"未找到 {imagePath}，请在程序运行目录下放置一张测试图片。");
    Console.WriteLine("当前工作目录: " + Directory.GetCurrentDirectory());
    return;
}

// 配置 Ollama 客户端
var config = new VisionConfig
{
    BaseUrl = "http://localhost:11434",
    Model = "qwen3-vl:8b",
    TimeoutSeconds = 600,
    SystemPrompt = "请详细描述这张图片中的所有视觉元素，包括场景、角色、颜色、氛围等。特别注意是否有月亮、天空等自然元素。"
};

// 创建客户端并描述图片
using var client = new OllamaVisionClient(config, onLog: msg => Console.WriteLine($"  {msg}"));

try
{
    Console.WriteLine($"\n正在描述图片: {imagePath}");
    Console.WriteLine($"模型: {config.Model}\n");

    var description = await client.DescribeImageAsync(imagePath);

    Console.WriteLine("\n=== 图片描述结果 ===");
    Console.WriteLine(description);
    Console.WriteLine("\n=== 完成 ===");
}
catch (VisionException ex)
{
    Console.WriteLine($"\n错误: {ex.Message}");
    Console.WriteLine("请确保:");
    Console.WriteLine("1. Ollama 服务正在运行 (http://localhost:11434)");
    Console.WriteLine("2. 已拉取模型: ollama pull qwen3-vl:8b");
}
