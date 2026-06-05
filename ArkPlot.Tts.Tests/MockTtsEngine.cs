using ArkPlot.Tts;

namespace ArkPlot.Tts.Tests;

/// <summary>
/// 测试用 Mock TTS 引擎：不调用任何网络 API，直接写入固定内容的 MP3 文件。
/// 可通过 SynthesizeCallCount 验证调用次数。
/// </summary>
internal class MockTtsEngine : ITtsEngine
{
    private static readonly byte[] FakeMp3Bytes =
    [
        // Minimal valid MP3 frame header (MPEG1 Layer3, 128kbps, 44100Hz, stereo)
        0xFF, 0xFB, 0x90, 0x00,
        // Padding to simulate a small frame
        ..new byte[417]
    ];

    public int SynthesizeCallCount { get; private set; }

    public List<(string Text, string Voice, string OutputPath)> SynthesizeCalls { get; } = [];

    public bool ShouldFail { get; set; }

    public string? FailMessage { get; set; }

    public Task SynthesizeAsync(
        string text,
        string voice,
        string outputPath,
        string rate = "+0%",
        string volume = "+0%",
        CancellationToken cancellationToken = default)
    {
        SynthesizeCallCount++;
        SynthesizeCalls.Add((text, voice, outputPath));

        if (ShouldFail)
            throw new IOException(FailMessage ?? "Mock TTS failure");

        cancellationToken.ThrowIfCancellationRequested();

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(outputPath, FakeMp3Bytes);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TtsVoiceInfo>> ListVoicesAsync()
    {
        IReadOnlyList<TtsVoiceInfo> voices = new List<TtsVoiceInfo>
        {
            new("zh-CN-XiaoxiaoNeural", "zh-CN", "Female"),
            new("zh-CN-XiaoyiNeural", "zh-CN", "Female"),
            new("zh-CN-YunxiNeural", "zh-CN", "Male"),
            new("zh-CN-YunjianNeural", "zh-CN", "Male"),
        };
        return Task.FromResult(voices);
    }
}
