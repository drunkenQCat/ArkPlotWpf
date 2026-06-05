using System.Text.Json;
using ArkPlot.Tts.Alignment;

namespace ArkPlot.Tts.Tests;

public class TtsPipelineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly string _outputDir;
    private readonly MockTtsEngine _engine;
    private readonly VoiceManager _voices;
    private readonly TtsCacheService _cache;

    public TtsPipelineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arkplot_pipeline_test_{Guid.NewGuid():N}");
        _cacheDir = Path.Combine(_tempDir, "_tts_cache");
        _outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(_outputDir);

        _engine = new MockTtsEngine();
        _voices = new VoiceManager();
        _cache = new TtsCacheService(_cacheDir);
    }

    [Fact]
    public async Task GenerateAsync_AlignedJson_ProducesOutput()
    {
        // 创建 aligned JSON
        var entries = new List<AlignmentEntry>(
        [
            new("她微笑着说：\"你好啊。\"", true, "阿米娅", "char_002", 0, "第一章", "女"),
            new("他沉默了片刻。", false, null, null, -1, "第一章", null),
            new("\"我理解你的意思。\"", true, "博士", "char_001", 1, "第一章", "男"),
        ]);

        var jsonPath = Path.Combine(_tempDir, "test_aligned.json");
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(jsonPath, json);

        var request = new TtsRequest(
            TtsInputMode.AlignedJson,
            jsonPath,
            _outputDir,
            RequestDelayMs: 0);

        using var pipeline = new TtsPipeline(_engine, _voices, _cache);
        var progress = new Progress<string>();

        var result = await pipeline.GenerateAsync(request, progress: progress);

        Assert.True(result.TotalSegments > 0);
        Assert.Single(result.OutputFiles);
        Assert.True(File.Exists(result.OutputFiles[0]));
    }

    [Fact]
    public async Task GenerateAsync_CacheHit_SkipsSynthesis()
    {
        var entries = new List<AlignmentEntry>(
        [
            new("\"缓存测试文本\"", true, "角色A", "code_a", 0, "第一章", "女"),
        ]);

        var jsonPath = Path.Combine(_tempDir, "cache_test.json");
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(jsonPath, json);

        var request = new TtsRequest(
            TtsInputMode.AlignedJson,
            jsonPath,
            _outputDir,
            RequestDelayMs: 0);

        using var pipeline = new TtsPipeline(_engine, _voices, _cache);

        // 第一次运行
        var result1 = await pipeline.GenerateAsync(request);
        var callCount1 = _engine.SynthesizeCallCount;
        Assert.True(callCount1 > 0);

        // 第二次运行 - 应命中缓存，不再调用引擎
        // 重新创建 pipeline（缓存保留）
        using var pipeline2 = new TtsPipeline(_engine, _voices, _cache);
        var result2 = await pipeline2.GenerateAsync(request);

        // 引擎调用数不应增加（缓存命中）
        Assert.Equal(callCount1, _engine.SynthesizeCallCount);
    }

    [Fact]
    public async Task GenerateAsync_DebugVoiceOnly_DoesNotSynthesize()
    {
        var entries = new List<AlignmentEntry>(
        [
            new("\"调试模式\"", true, "角色B", "code_b", 0, "第一章", "男"),
        ]);

        var jsonPath = Path.Combine(_tempDir, "debug_test.json");
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(jsonPath, json);

        var request = new TtsRequest(
            TtsInputMode.AlignedJson,
            jsonPath,
            _outputDir,
            DebugVoiceOnly: true,
            RequestDelayMs: 0);

        using var pipeline = new TtsPipeline(_engine, _voices, _cache);
        var result = await pipeline.GenerateAsync(request);

        Assert.Equal(0, _engine.SynthesizeCallCount);
        Assert.Empty(result.OutputFiles);
    }

    [Fact]
    public async Task GenerateAsync_SegmentLimit_TruncatesSegments()
    {
        var entries = new List<AlignmentEntry>();
        for (int i = 0; i < 10; i++)
            entries.Add(new($"\"第{i + 1}句话\"", true, $"角色{i}", $"code_{i}", i, "第一章", "女"));

        var jsonPath = Path.Combine(_tempDir, "limit_test.json");
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(jsonPath, json);

        var request = new TtsRequest(
            TtsInputMode.AlignedJson,
            jsonPath,
            _outputDir,
            SegmentLimit: 3,
            RequestDelayMs: 0);

        using var pipeline = new TtsPipeline(_engine, _voices, _cache);
        var result = await pipeline.GenerateAsync(request);

        // 应该只合成了 3 个片段
        Assert.Equal(3, _engine.SynthesizeCallCount);
    }

    [Fact]
    public async Task GenerateAsync_EngineFailure_SkipsSegment()
    {
        _engine.ShouldFail = true;

        var entries = new List<AlignmentEntry>(
        [
            new("\"失败的文本\"", true, "角色", "code", 0, "第一章", "女"),
        ]);

        var jsonPath = Path.Combine(_tempDir, "fail_test.json");
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(jsonPath, json);

        var request = new TtsRequest(
            TtsInputMode.AlignedJson,
            jsonPath,
            _outputDir,
            RequestDelayMs: 0);

        using var pipeline = new TtsPipeline(_engine, _voices, _cache);
        var result = await pipeline.GenerateAsync(request);

        // 合成失败应跳过，不崩溃，无输出文件
        Assert.Empty(result.OutputFiles);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
