namespace ArkPlot.Tts.Tests;

public class AudioMergerTests : IDisposable
{
    private readonly string _tempDir;

    public AudioMergerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arkplot_tts_merge_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void MergeFiles_EmptyList_ThrowsArgumentException()
    {
        var output = Path.Combine(_tempDir, "out.mp3");
        Assert.Throws<ArgumentException>(() => AudioMerger.MergeFiles([], output));
    }

    [Fact]
    public void MergeFiles_NullList_ThrowsArgumentException()
    {
        var output = Path.Combine(_tempDir, "out.mp3");
        Assert.Throws<ArgumentException>(() => AudioMerger.MergeFiles(null!, output));
    }

    [Fact]
    public void MergeFiles_CreatesOutputDirectory()
    {
        // 用不存在文件的 MergeFiles 会先抛 FileNotFoundException（在 NAudio 层）
        // 但目录应该已被创建
        var subDir = Path.Combine(_tempDir, "sub", "deep");
        var output = Path.Combine(subDir, "out.mp3");

        try
        {
            AudioMerger.MergeFiles(["nonexistent.mp3"], output);
        }
        catch { /* 预期失败 */ }

        Assert.True(Directory.Exists(subDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
