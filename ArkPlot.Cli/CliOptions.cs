namespace ArkPlot.Cli;

/// <summary>
/// CLI 运行配置常量。
/// </summary>
public static class CliOptions
{
    /// <summary>开启时强制重写 PicDesc 数据库，方便测试。</summary>
    public const bool DebugMode = false;

    /// <summary>开启时使用 Mock 视觉描述（确定性输出，不消耗 API 配额）。</summary>
    public const bool UseMockVision = true;

    /// <summary>开启时使用百炼视觉模型生成真实图片描述。</summary>
    public const bool EnableVision = true;

    /// <summary>开启时使用 EdgeTTS 生成章节音频。</summary>
    public const bool EnableTts = false;

    /// <summary>开启时输出带图片描述的增强版 Markdown（含 2 行角色表）。关闭时输出简洁版。</summary>
    public const bool EnablePicDesc = true;
}
