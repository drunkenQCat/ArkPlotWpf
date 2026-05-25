# ArkPlot.Vision - Ollama 视觉模型调用模块

## 概述

独立的 Ollama 视觉模型调用模块，支持图片描述任务。默认使用 `qwen3-vl:8b` 模型。

## 快速开始

### 1. 前置要求

- 安装并运行 [Ollama](https://ollama.com/)
- 拉取视觉模型：`ollama pull qwen3-vl:8b`

### 2. 运行示例程序

```bash
# 确保 test.jpg 在运行目录下
dotnet run --project ArkPlot.Vision
```

## 在其他模块中使用

### 基础用法

```csharp
using ArkPlot.Vision;

// 创建客户端
var config = new VisionConfig
{
    BaseUrl = "http://localhost:11434",
    Model = "qwen3-vl:8b",
    TimeoutSeconds = 120
};

using var client = new OllamaVisionClient(config);

// 描述图片
var description = await client.DescribeImageAsync("test.jpg");
Console.WriteLine(description);
```

### 自定义提示词

```csharp
var description = await client.DescribeImageAsync(
    "test.jpg",
    userPrompt: "请判断这张图片中是否有两轮月亮，并详细描述天空场景。"
);
```

### 批量处理

```csharp
var images = Directory.GetFiles("images", "*.jpg");
var results = await client.DescribeImagesAsync(images);

foreach (var (path, desc) in results)
{
    Console.WriteLine($"{path}: {desc}");
}
```

### 在 ArkPlot.Avalonia 中集成

```csharp
// 在 ViewModel 或 Service 中
public class ImageAnalysisService
{
    private readonly OllamaVisionClient _client;

    public ImageAnalysisService()
    {
        var config = new VisionConfig
        {
            SystemPrompt = "你是明日方舟世界观下的图片分析助手。"
        };
        _client = new OllamaVisionClient(config, onLog: log => Debug.WriteLine(log));
    }

    public async Task<string> AnalyzeImageAsync(string imagePath)
    {
        return await _client.DescribeImageAsync(imagePath);
    }
}
```

### 依赖注入方式

```csharp
// Program.cs 或 Startup
services.AddSingleton(new VisionConfig
{
    Model = "qwen3-vl:8b",
    TimeoutSeconds = 120
});

services.AddSingleton<HttpClient>();

services.AddSingleton<OllamaVisionClient>(sp =>
{
    var http = sp.GetRequiredService<HttpClient>();
    var config = sp.GetRequiredService<VisionConfig>();
    return new OllamaVisionClient(http, config);
});
```

## API 参考

### VisionConfig

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| BaseUrl | string | http://localhost:11434 | Ollama 服务地址 |
| Model | string | qwen3-vl:8b | 模型名称 |
| TimeoutSeconds | int | 120 | 请求超时秒数 |
| MaxRetries | int | 3 | 最大重试次数 |
| SystemPrompt | string | 图片描述提示词 | 系统提示词 |
| Temperature | float | 0.7 | 温度参数 |
| MaxTokens | int | 2048 | 最大生成 token 数 |

### OllamaVisionClient

| 方法 | 说明 |
|------|------|
| `DescribeImageAsync(path, userPrompt?)` | 描述本地图片文件 |
| `DescribeImageBase64Async(base64, userPrompt?)` | 描述 Base64 编码的图片 |
| `DescribeImagesAsync(paths, userPrompt?)` | 批量描述图片 |

## 错误处理

```csharp
try
{
    var desc = await client.DescribeImageAsync("test.jpg");
}
catch (VisionException ex)
{
    // Ollama 服务未运行、模型未下载、网络异常等
    Console.WriteLine($"错误: {ex.Message}");
}
catch (FileNotFoundException ex)
{
    // 图片文件不存在
    Console.WriteLine($"文件不存在: {ex.Message}");
}
```

## 注意事项

- Ollama 视觉模型首次加载可能较慢，建议适当增加 `TimeoutSeconds`
- 大图片会占用较多内存，Base64 编码后约为原图的 1.33 倍
- 批量处理时建议控制并发数量（可使用 `SemaphoreSlim`）
