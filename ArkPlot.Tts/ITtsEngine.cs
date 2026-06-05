namespace ArkPlot.Tts;

/// <summary>
/// TTS 合成引擎抽象接口。
/// 实现此接口以接入不同的 TTS 后端（EdgeTTS、Azure Speech、本地模型等）。
/// </summary>
public interface ITtsEngine
{
    /// <summary>
    /// 将文本合成为音频文件。
    /// </summary>
    /// <param name="text">要合成的文本。</param>
    /// <param name="voice">音色标识（如 "zh-CN-XiaoxiaoNeural"）。</param>
    /// <param name="outputPath">输出音频文件的完整路径。</param>
    /// <param name="rate">语速调整，如 "+10%"、"-5%"。</param>
    /// <param name="volume">音量调整，如 "+10%"、"-5%"。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SynthesizeAsync(
        string text,
        string voice,
        string outputPath,
        string rate = "+0%",
        string volume = "+0%",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出引擎支持的所有音色。
    /// </summary>
    /// <returns>音色信息列表。</returns>
    Task<IReadOnlyList<TtsVoiceInfo>> ListVoicesAsync();
}

/// <summary>
/// 音色信息。
/// </summary>
public record TtsVoiceInfo(
    string ShortName,
    string Locale,
    string Gender
);
