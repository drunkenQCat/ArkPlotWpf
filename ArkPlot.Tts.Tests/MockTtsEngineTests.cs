namespace ArkPlot.Tts.Tests;

public class MockTtsEngineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MockTtsEngine _engine;

    public MockTtsEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arkplot_tts_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _engine = new MockTtsEngine();
    }

    [Fact]
    public async Task SynthesizeAsync_CreatesOutputFile()
    {
        var outputPath = Path.Combine(_tempDir, "test.mp3");

        await _engine.SynthesizeAsync("你好世界", "zh-CN-XiaoxiaoNeural", outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
        Assert.Equal(1, _engine.SynthesizeCallCount);
    }

    [Fact]
    public async Task SynthesizeAsync_RecordsCallParameters()
    {
        var outputPath = Path.Combine(_tempDir, "test.mp3");

        await _engine.SynthesizeAsync("测试文本", "zh-CN-YunxiNeural", outputPath);

        var call = Assert.Single(_engine.SynthesizeCalls);
        Assert.Equal("测试文本", call.Text);
        Assert.Equal("zh-CN-YunxiNeural", call.Voice);
        Assert.Equal(outputPath, call.OutputPath);
    }

    [Fact]
    public async Task SynthesizeAsync_WhenShouldFail_ThrowsIOException()
    {
        _engine.ShouldFail = true;
        _engine.FailMessage = "Connection refused";
        var outputPath = Path.Combine(_tempDir, "test.mp3");

        var ex = await Assert.ThrowsAsync<IOException>(
            () => _engine.SynthesizeAsync("text", "voice", outputPath));

        Assert.Equal("Connection refused", ex.Message);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task ListVoicesAsync_ReturnsMockVoices()
    {
        var voices = await _engine.ListVoicesAsync();

        Assert.Equal(4, voices.Count);
        Assert.Contains(voices, v => v.ShortName == "zh-CN-XiaoxiaoNeural");
        Assert.Contains(voices, v => v.ShortName == "zh-CN-YunxiNeural");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
