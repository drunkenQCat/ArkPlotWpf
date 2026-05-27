namespace ArkPlot.Cli;

/// <summary>
/// CLI 运行配置常量。
/// </summary>
public static class CliOptions
{
    /// <summary>开启时强制重写 PicDesc 数据库，方便测试。</summary>
    public const bool DebugMode = false;

    /// <summary>开启时使用 Ollama / 百炼视觉模型生成真实图片描述。</summary>
    public const bool EnableVision = true;

    /// <summary>开启时使用 EdgeTTS 生成章节音频。</summary>
    public const bool EnableTts = true;
}
