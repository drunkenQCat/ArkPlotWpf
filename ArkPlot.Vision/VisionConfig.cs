namespace ArkPlot.Vision;

/// <summary>
/// Ollama 视觉模型配置
/// </summary>
public record VisionConfig
{
    /// <summary>
    /// Ollama 服务地址，默认 http://localhost:11434
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>
    /// 模型名称，默认 qwen3-vl:8b
    /// </summary>
    public string Model { get; init; } = "qwen3-vl:8b";

    /// <summary>
    /// 请求超时秒数，默认 120 秒
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// 最大重试次数，默认 3 次
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 系统提示词，默认图片描述任务
    /// </summary>
    public string SystemPrompt { get; init; } = "你是一个专业的图片描述助手。请仔细观察图片，用详细的中文描述图片中的内容、场景、角色和细节。";

    /// <summary>
    /// 温度参数，控制创造性，默认 0.7
    /// </summary>
    public float Temperature { get; init; } = 0.7f;

    /// <summary>
    /// 最大生成 token 数，默认 2048
    /// </summary>
    public int MaxTokens { get; init; } = 2048;
}
