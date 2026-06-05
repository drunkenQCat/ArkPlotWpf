using ArkPlot.Tts.Alignment;

namespace ArkPlot.Tts.Tests;

public class GenderOverrideProviderTests : IDisposable
{
    private readonly string _tempDir;

    public GenderOverrideProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arkplot_gender_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Constructor_Default_NoOverrides()
    {
        var provider = new GenderOverrideProvider();
        Assert.Equal(0, provider.Count);
        Assert.Null(provider.GetOverride("任何角色"));
    }

    [Fact]
    public void Constructor_NonexistentFile_NoOverrides()
    {
        var provider = new GenderOverrideProvider("nonexistent.json");
        Assert.Equal(0, provider.Count);
        Assert.Null(provider.GetOverride("任何角色"));
    }

    [Fact]
    public void Constructor_ValidJson_LoadsOverrides()
    {
        var jsonPath = Path.Combine(_tempDir, "overrides.json");
        File.WriteAllText(jsonPath, """{"阿米娅": {"gender": "女"}, "博士": {"gender": "男"}}""");

        var provider = new GenderOverrideProvider(jsonPath);

        Assert.Equal(2, provider.Count);
        Assert.Equal("女", provider.GetOverride("阿米娅"));
        Assert.Equal("男", provider.GetOverride("博士"));
    }

    [Fact]
    public void GetOverride_CaseInsensitive()
    {
        var provider = new GenderOverrideProvider(new Dictionary<string, string>
        {
            ["TestChar"] = "女"
        });

        Assert.Equal("女", provider.GetOverride("testchar"));
        Assert.Equal("女", provider.GetOverride("TESTCHAR"));
    }

    [Fact]
    public void GetOverride_NullName_ReturnsNull()
    {
        var provider = new GenderOverrideProvider(new Dictionary<string, string>
        {
            ["某角色"] = "女"
        });

        Assert.Null(provider.GetOverride(null));
        Assert.Null(provider.GetOverride(""));
    }

    [Fact]
    public void Constructor_InvalidJson_DoesNotThrow()
    {
        var jsonPath = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(jsonPath, "this is not json");

        var provider = new GenderOverrideProvider(jsonPath);
        Assert.Equal(0, provider.Count);
    }

    [Fact]
    public void InferGender_OverrideTakesPrecedence()
    {
        var overrides = new GenderOverrideProvider(new Dictionary<string, string>
        {
            ["阿米娅"] = "男" // 故意覆盖为错误性别来验证 override 生效
        });

        var picDesc = new Dictionary<string, string>
        {
            ["avg_npc_1"] = "她是一位女性角色"
        };

        // 有 override 时返回 override 值
        var result = NovelAligner.InferGender("avg_npc_1", picDesc, overrides, "阿米娅");
        Assert.Equal("男", result);
    }

    [Fact]
    public void InferGender_NoOverride_FallsBackToPicDesc()
    {
        var overrides = new GenderOverrideProvider();

        var picDesc = new Dictionary<string, string>
        {
            ["avg_npc_1"] = "她是一位女性角色"
        };

        var result = NovelAligner.InferGender("avg_npc_1", picDesc, overrides, "未配置的角色");
        Assert.Equal("女", result);
    }

    [Fact]
    public void InferGender_NullOverrides_FallsBackToPicDesc()
    {
        var picDesc = new Dictionary<string, string>
        {
            ["avg_npc_1"] = "她是一位女性角色"
        };

        var result = NovelAligner.InferGender("avg_npc_1", picDesc);
        Assert.Equal("女", result);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
