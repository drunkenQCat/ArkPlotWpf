namespace ArkPlot.Tts.Tests;

public class VoiceManagerTests
{
    private readonly VoiceManager _vm = new();

    [Fact]
    public void GetVoiceForCharacter_EmptyName_ReturnsNarrator()
    {
        var voice = _vm.GetVoiceForCharacter("");
        Assert.Equal(VoicePool.Narrator, voice);
    }

    [Fact]
    public void GetVoiceForCharacter_NullName_ReturnsNarrator()
    {
        var voice = _vm.GetVoiceForCharacter(null!);
        Assert.Equal(VoicePool.Narrator, voice);
    }

    [Fact]
    public void GetVoiceForCharacter_FemaleGender_ReturnsFemaleVoice()
    {
        var voice = _vm.GetVoiceForCharacter("阿米娅", "女");
        Assert.True(VoicePool.IsFemaleVoice(voice), $"Expected female voice, got {voice}");
    }

    [Fact]
    public void GetVoiceForCharacter_MaleGender_ReturnsMaleVoice()
    {
        var voice = _vm.GetVoiceForCharacter("博士", "男");
        Assert.True(VoicePool.IsMaleVoice(voice), $"Expected male voice, got {voice}");
    }

    [Fact]
    public void GetVoiceForCharacter_SameNameSameGender_ReturnsSameVoice()
    {
        var v1 = _vm.GetVoiceForCharacter("陈", "女");
        var v2 = _vm.GetVoiceForCharacter("陈", "女");
        Assert.Equal(v1, v2);
    }

    [Fact]
    public void GetVoiceForCharacter_UnknownGender_UsesHashFallback()
    {
        // 无性别时走哈希，结果稳定
        var v1 = _vm.GetVoiceForCharacter("某角色");
        var v2 = _vm.GetVoiceForCharacter("某角色");
        Assert.Equal(v1, v2);
    }

    [Fact]
    public void GetVoiceForCharacter_ConsistentAcrossInstances()
    {
        // 不同 VoiceManager 实例对同一角色应分配相同音色（SHA256 稳定）
        var vm1 = new VoiceManager();
        var vm2 = new VoiceManager();

        var v1 = vm1.GetVoiceForCharacter("测试角色", "女");
        var v2 = vm2.GetVoiceForCharacter("测试角色", "女");
        Assert.Equal(v1, v2);
    }

    [Fact]
    public void GetNarratorVoice_ReturnsXiaoxiao()
    {
        Assert.Equal("zh-CN-XiaoxiaoNeural", _vm.GetNarratorVoice());
    }

    [Fact]
    public void GetFallbackVoice_ReturnsYunxi()
    {
        Assert.Equal("zh-CN-YunxiNeural", _vm.GetFallbackVoice());
    }

    [Fact]
    public void GetVoiceForGender_Female_ReturnsFirstFemale()
    {
        Assert.Equal(VoicePool.Female[0], _vm.GetVoiceForGender("女"));
    }

    [Fact]
    public void GetVoiceForGender_Male_ReturnsFirstMale()
    {
        Assert.Equal(VoicePool.Male[0], _vm.GetVoiceForGender("男"));
    }

    [Fact]
    public void GetVoiceForGender_Null_ReturnsNarrator()
    {
        Assert.Equal(VoicePool.Narrator, _vm.GetVoiceForGender(null));
    }

    [Fact]
    public void AssignedCount_TracksUniqueAssignments()
    {
        Assert.Equal(0, _vm.AssignedCount);

        _vm.GetVoiceForCharacter("角色A", "女");
        Assert.Equal(1, _vm.AssignedCount);

        _vm.GetVoiceForCharacter("角色B", "男");
        Assert.Equal(2, _vm.AssignedCount);

        // 重复不增加
        _vm.GetVoiceForCharacter("角色A", "女");
        Assert.Equal(2, _vm.AssignedCount);
    }

    [Fact]
    public void GetStableHash_ConsistentAcrossCalls()
    {
        var h1 = VoiceManager.GetStableHash("hello");
        var h2 = VoiceManager.GetStableHash("hello");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void GetStableHash_DifferentInputsDifferentHashes()
    {
        var h1 = VoiceManager.GetStableHash("alpha");
        var h2 = VoiceManager.GetStableHash("beta");
        Assert.NotEqual(h1, h2);
    }
}

public class VoicePoolTests
{
    [Fact]
    public void FemalePool_HasFourVoices()
    {
        Assert.Equal(4, VoicePool.Female.Length);
    }

    [Fact]
    public void MalePool_HasFourVoices()
    {
        Assert.Equal(4, VoicePool.Male.Length);
    }

    [Fact]
    public void Narrator_NotInFemaleOrMalePool()
    {
        Assert.DoesNotContain(VoicePool.Narrator, VoicePool.Female);
        Assert.DoesNotContain(VoicePool.Narrator, VoicePool.Male);
    }

    [Fact]
    public void IsFemaleVoice_Narrator_ReturnsTrue()
    {
        Assert.True(VoicePool.IsFemaleVoice(VoicePool.Narrator));
    }

    [Fact]
    public void IsMaleVoice_Yunxi_ReturnsTrue()
    {
        Assert.True(VoicePool.IsMaleVoice("zh-CN-YunxiNeural"));
    }
}
