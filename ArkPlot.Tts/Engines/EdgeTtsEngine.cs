using EdgeTTS.DotNet;

namespace ArkPlot.Tts.Engines;

/// <summary>
/// 基于 Microsoft Edge TTS（WebSocket）的 TTS 引擎实现。
/// </summary>
public class EdgeTtsEngine : ITtsEngine
{
    private const int DefaultMaxRetries = 3;

    public async Task SynthesizeAsync(
        string text,
        string voice,
        string outputPath,
        string rate = "+0%",
        string volume = "+0%",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("台词文本不能为空", nameof(text));

        for (int attempt = 1; attempt <= DefaultMaxRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var request = new Communicate(text, voice: voice, rate: rate, volume: volume);
                await request.SaveAsync(outputPath);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (attempt < DefaultMaxRetries && IsTransientError(ex))
            {
                var delay = attempt * 2000;
                Console.WriteLine($"  ⚠️ EdgeTTS 合成失败(第{attempt}次): {ex.Message}，{delay / 1000}秒后重试…");
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<TtsVoiceInfo>> ListVoicesAsync()
    {
        var voices = await Voices.ListVoicesAsync();
        return voices
            .Select(v => new TtsVoiceInfo(v.ShortName, v.Locale, v.Gender))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 判断是否为可重试的瞬态错误。
    /// </summary>
    internal static bool IsTransientError(Exception ex)
    {
        var msg = ex.Message.ToLower();
        return msg.Contains("unable to connect") ||
               msg.Contains("connection") ||
               msg.Contains("timeout") ||
               msg.Contains("reset") ||
               msg.Contains("websocket") ||
               ex is System.Net.Http.HttpRequestException ||
               ex is System.Net.Sockets.SocketException ||
               ex is TaskCanceledException ||
               ex.GetType().FullName?.Contains("EdgeTTS.DotNet") == true;
    }
}
