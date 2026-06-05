namespace ArkPlot.Tts.Tests;

public class TtsCacheServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TtsCacheService _cache;

    public TtsCacheServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arkplot_tts_cache_test_{Guid.NewGuid():N}");
        _cache = new TtsCacheService(Path.Combine(_tempDir, "_tts_cache"));
    }

    [Fact]
    public void Constructor_CreatesCacheDirectory()
    {
        Assert.True(Directory.Exists(_cache.CacheDir));
    }

    [Fact]
    public void GetCacheKey_SameInput_SameKey()
    {
        var k1 = TtsCacheService.GetCacheKey("你好", "zh-CN-XiaoxiaoNeural");
        var k2 = TtsCacheService.GetCacheKey("你好", "zh-CN-XiaoxiaoNeural");
        Assert.Equal(k1, k2);
    }

    [Fact]
    public void GetCacheKey_DifferentText_DifferentKey()
    {
        var k1 = TtsCacheService.GetCacheKey("你好", "zh-CN-XiaoxiaoNeural");
        var k2 = TtsCacheService.GetCacheKey("再见", "zh-CN-XiaoxiaoNeural");
        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void GetCacheKey_DifferentVoice_DifferentKey()
    {
        var k1 = TtsCacheService.GetCacheKey("你好", "zh-CN-XiaoxiaoNeural");
        var k2 = TtsCacheService.GetCacheKey("你好", "zh-CN-YunxiNeural");
        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void GetCacheKey_DifferentRate_DifferentKey()
    {
        var k1 = TtsCacheService.GetCacheKey("你好", "voice", "+0%");
        var k2 = TtsCacheService.GetCacheKey("你好", "voice", "+10%");
        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void TryGetCachedAudio_NoCache_ReturnsFalse()
    {
        var (hit, path) = _cache.TryGetCachedAudio("nonexistent_key");
        Assert.False(hit);
        Assert.Null(path);
    }

    [Fact]
    public void SaveToCache_ThenRetrieve_ReturnsHit()
    {
        // 创建源文件
        var sourceFile = Path.Combine(_tempDir, "source.mp3");
        File.WriteAllBytes(sourceFile, [0xFF, 0xFB, 0x90, 0x00, ..new byte[100]]);

        var key = TtsCacheService.GetCacheKey("测试文本", "voice");
        _cache.SaveToCache(key, sourceFile);

        var (hit, path) = _cache.TryGetCachedAudio(key);
        Assert.True(hit);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveToCache_Overwrite_Works()
    {
        var source1 = Path.Combine(_tempDir, "source1.mp3");
        var source2 = Path.Combine(_tempDir, "source2.mp3");
        File.WriteAllBytes(source1, [0x01]);
        File.WriteAllBytes(source2, [0x02, 0x03]);

        var key = TtsCacheService.GetCacheKey("文本", "voice");

        _cache.SaveToCache(key, source1);
        _cache.SaveToCache(key, source2); // overwrite

        var (hit, path) = _cache.TryGetCachedAudio(key);
        Assert.True(hit);
        Assert.Equal(2, new FileInfo(path!).Length);
    }

    [Fact]
    public void CachedFileCount_TracksCorrectly()
    {
        Assert.Equal(0, _cache.CachedFileCount);

        var source = Path.Combine(_tempDir, "src.mp3");
        File.WriteAllBytes(source, [0xFF]);

        _cache.SaveToCache("key1", source);
        Assert.Equal(1, _cache.CachedFileCount);

        _cache.SaveToCache("key2", source);
        Assert.Equal(2, _cache.CachedFileCount);

        // 覆盖不增加
        _cache.SaveToCache("key1", source);
        Assert.Equal(2, _cache.CachedFileCount);
    }

    [Fact]
    public void TryGetCachedAudio_EmptyFile_ReturnsFalse()
    {
        // 创建空缓存文件
        var key = "empty_key";
        var emptyPath = Path.Combine(_cache.CacheDir, $"{key}.mp3");
        File.WriteAllBytes(emptyPath, []);

        var (hit, _) = _cache.TryGetCachedAudio(key);
        Assert.False(hit);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
