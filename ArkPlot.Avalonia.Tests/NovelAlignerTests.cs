using ArkPlot.Core.Model;
using ArkPlot.Tts.Alignment;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// NovelAligner 单元测试：覆盖纯函数、映射构建、对齐流程。
/// </summary>
public class NovelAlignerTests
{
    // ── ExtractActName ──────────────────────────────

    [Fact]
    public void ExtractActName_WithNovelSuffix()
        => Assert.Equal("水晶箭行动", NovelAligner.ExtractActName("水晶箭行动_novel_flash"));

    [Fact]
    public void ExtractActName_WithNovelPro()
        => Assert.Equal("水晶箭行动", NovelAligner.ExtractActName("水晶箭行动_novel_pro"));

    [Fact]
    public void ExtractActName_NoNovelSuffix()
        => Assert.Equal("水晶箭行动", NovelAligner.ExtractActName("水晶箭行动"));

    [Fact]
    public void ExtractActName_EmptyString()
        => Assert.Equal("", NovelAligner.ExtractActName(""));

    // ── InferGender ─────────────────────────────────

    [Fact]
    public void InferGender_NullCode_ReturnsNull()
        => Assert.Null(NovelAligner.InferGender(null, new()));

    [Fact]
    public void InferGender_EmptyCode_ReturnsNull()
        => Assert.Null(NovelAligner.InferGender("", new()));

    [Fact]
    public void InferGender_CodeNotInDict_ReturnsNull()
        => Assert.Null(NovelAligner.InferGender("avg_npc_999", new()));

    [Fact]
    public void InferGender_DescHasShe_ReturnsFemale()
    {
        var dict = new Dictionary<string, string> { ["avg_npc_1"] = "她站在空旷的白色背景里" };
        Assert.Equal("女", NovelAligner.InferGender("avg_npc_1", dict));
    }

    [Fact]
    public void InferGender_DescHasHe_ReturnsMale()
    {
        var dict = new Dictionary<string, string> { ["avg_npc_2"] = "他立于空无一物的背景前" };
        Assert.Equal("男", NovelAligner.InferGender("avg_npc_2", dict));
    }

    [Fact]
    public void InferGender_SheAndHe_SheFirst_ReturnsFemale()
    {
        // 前100字内"她"先出现
        var dict = new Dictionary<string, string>
        {
            ["avg_npc_3"] = "她站在背景里，他在旁边"
        };
        Assert.Equal("女", NovelAligner.InferGender("avg_npc_3", dict));
    }

    [Fact]
    public void InferGender_CodeWithHashSuffix_MatchesBaseCode()
    {
        var dict = new Dictionary<string, string> { ["avg_npc_1"] = "她立于虚空" };
        Assert.Equal("女", NovelAligner.InferGender("avg_npc_1#1$1", dict));
    }

    [Fact]
    public void InferGender_FallbackKeyword_Female()
    {
        var dict = new Dictionary<string, string> { ["avg_npc_4"] = "一位少女站在街角" };
        Assert.Equal("女", NovelAligner.InferGender("avg_npc_4", dict));
    }

    [Fact]
    public void InferGender_FallbackKeyword_Male()
    {
        var dict = new Dictionary<string, string> { ["avg_npc_5"] = "一位少年立于风中" };
        Assert.Equal("男", NovelAligner.InferGender("avg_npc_5", dict));
    }

    [Fact]
    public void InferGender_NoKeywords_ReturnsNull()
    {
        var dict = new Dictionary<string, string> { ["avg_npc_6"] = "空茫的背景里，一身战术装备" };
        Assert.Null(NovelAligner.InferGender("avg_npc_6", dict));
    }

    // ── ExtractCodeFromCharSlot ─────────────────────

    [Fact]
    public void ExtractCodeFromCharSlot_NormalName()
    {
        var entry = new FormattedTextEntry
        {
            CommandSet = new StringDict { ["name"] = "avg_npc_1272_1#1$1", ["focus"] = "r" }
        };
        Assert.Equal("avg_npc_1272_1", NovelAligner.ExtractCodeFromCharSlot(entry));
    }

    [Fact]
    public void ExtractCodeFromCharSlot_Focus2_TakesName2()
    {
        var entry = new FormattedTextEntry
        {
            CommandSet = new StringDict
            {
                ["name"] = "avg_npc_1#1$1",
                ["name2"] = "avg_npc_2#1$1",
                ["focus"] = "2"
            }
        };
        Assert.Equal("avg_npc_2", NovelAligner.ExtractCodeFromCharSlot(entry));
    }

    [Fact]
    public void ExtractCodeFromCharSlot_Focus2_NoName2_FallbackToName()
    {
        var entry = new FormattedTextEntry
        {
            CommandSet = new StringDict { ["name"] = "avg_npc_1#1$1", ["focus"] = "2" }
        };
        Assert.Equal("avg_npc_1", NovelAligner.ExtractCodeFromCharSlot(entry));
    }

    [Fact]
    public void ExtractCodeFromCharSlot_NoCommandSet_FallbackToCharacterCode()
    {
        var entry = new FormattedTextEntry { CharacterCode = "fallback_code" };
        Assert.Equal("fallback_code", NovelAligner.ExtractCodeFromCharSlot(entry));
    }

    [Fact]
    public void ExtractCodeFromCharSlot_EmptyCommandSet_FallbackToCharacterCode()
    {
        var entry = new FormattedTextEntry
        {
            CommandSet = new StringDict(),
            CharacterCode = "fallback_code"
        };
        Assert.Equal("fallback_code", NovelAligner.ExtractCodeFromCharSlot(entry));
    }

    [Fact]
    public void ExtractCodeFromCharSlot_NameWithoutHash()
    {
        var entry = new FormattedTextEntry
        {
            CommandSet = new StringDict { ["name"] = "avg_npc_100" }
        };
        Assert.Equal("avg_npc_100", NovelAligner.ExtractCodeFromCharSlot(entry));
    }

    // ── BuildNameToCodeMap（重构后提取） ─────────────

    [Fact]
    public void BuildNameToCodeMap_CharslotThenDialog_MapsCorrectly()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeCharSlot(0, "avg_npc_100_1#1$1", "r"),
            MakeDialog(1, "艾拉", "已确认安全。"),
        };

        var map = NovelAligner.BuildNameToCodeMap(new Dictionary<long, List<FormattedTextEntry>> { [1] = entries });

        Assert.Single(map);
        Assert.Equal("avg_npc_100_1", map["艾拉"]);
    }

    [Fact]
    public void BuildNameToCodeMap_FocusNone_Skipped()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeCharSlot(0, "avg_npc_100_1#1$1", "none"),
            MakeDialog(1, "艾拉", "已确认安全。"),
        };

        var map = NovelAligner.BuildNameToCodeMap(new Dictionary<long, List<FormattedTextEntry>> { [1] = entries });

        Assert.Empty(map);
    }

    [Fact]
    public void BuildNameToCodeMap_NoNameParam_Skipped()
    {
        // charslot 只有 focus，没有 name → 焦点切换，不建立映射
        var entries = new List<FormattedTextEntry>
        {
            new() { Index = 0, Type = "charslot", CommandSet = new StringDict { ["focus"] = "r" } },
            MakeDialog(1, "艾拉", "已确认安全。"),
        };

        var map = NovelAligner.BuildNameToCodeMap(new Dictionary<long, List<FormattedTextEntry>> { [1] = entries });

        Assert.Empty(map);
    }

    [Fact]
    public void BuildNameToCodeMap_MultipleCharacters_AllMapped()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeCharSlot(0, "avg_npc_100_1#1$1", "r"),
            MakeDialog(1, "艾拉", "第一句。"),
            MakeCharSlot(2, "avg_npc_200_1#1$1", "l"),
            MakeDialog(3, "双月", "第二句。"),
        };

        var map = NovelAligner.BuildNameToCodeMap(new Dictionary<long, List<FormattedTextEntry>> { [1] = entries });

        Assert.Equal(2, map.Count);
        Assert.Equal("avg_npc_100_1", map["艾拉"]);
        Assert.Equal("avg_npc_200_1", map["双月"]);
    }

    // ── BuildCharCodeAtEntry（重构后提取） ────────────

    [Fact]
    public void BuildCharCodeAtEntry_DialogAfterCharslot_GetsCode()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeCharSlot(0, "avg_npc_100_1#1$1", "r"),
            MakeDialog(1, "艾拉", "已确认安全。"),
        };
        var nameToCode = new Dictionary<string, string> { ["艾拉"] = "avg_npc_100_1" };

        var map = NovelAligner.BuildCharCodeAtEntry(
            new Dictionary<long, List<FormattedTextEntry>> { [1] = entries }, nameToCode);

        Assert.Equal("avg_npc_100_1", map[(1, 1)]);
    }

    [Fact]
    public void BuildCharCodeAtEntry_SameCharacterNewPortrait_UpdatesCode()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeCharSlot(0, "avg_npc_100_1#1$1", "r"),
            MakeDialog(1, "艾拉", "第一句。"),
            MakeCharSlot(2, "avg_npc_100_1#5$1", "r"),  // 同角色换表情
            MakeDialog(3, "艾拉", "第二句。"),
        };
        var nameToCode = new Dictionary<string, string> { ["艾拉"] = "avg_npc_100_1" };

        var map = NovelAligner.BuildCharCodeAtEntry(
            new Dictionary<long, List<FormattedTextEntry>> { [1] = entries }, nameToCode);

        // 第二个 dialog 用全局 nameToCode（"avg_npc_100_1"），而非本地 lastCharSlotCode
        Assert.Equal("avg_npc_100_1", map[(1, 1)]);
        Assert.Equal("avg_npc_100_1", map[(1, 3)]);
    }

    [Fact]
    public void BuildCharCodeAtEntry_NoNameMapping_FallbackToLocalCode()
    {
        // dialog 没有 CharacterName → nameToCode 查不到 → fallback 到最近 charslot
        var entries = new List<FormattedTextEntry>
        {
            MakeCharSlot(0, "avg_npc_100_1#1$1", "r"),
            new() { Index = 1, Type = "dialog", Dialog = "无角色名的对话" },
        };
        var nameToCode = new Dictionary<string, string>();

        var map = NovelAligner.BuildCharCodeAtEntry(
            new Dictionary<long, List<FormattedTextEntry>> { [1] = entries }, nameToCode);

        Assert.Equal("avg_npc_100_1", map[(1, 1)]);
    }

    // ── IsEffectiveCharSlot ─────────────────────────

    [Fact]
    public void IsEffectiveCharSlot_WithNameAndFocusR_ReturnsTrue()
    {
        var entry = new FormattedTextEntry
        {
            CommandSet = new StringDict { ["name"] = "avg_npc_1", ["focus"] = "r" }
        };
        Assert.True(NovelAligner.IsEffectiveCharSlot(entry));
    }

    [Fact]
    public void IsEffectiveCharSlot_FocusNone_ReturnsFalse()
    {
        var entry = new FormattedTextEntry
        {
            CommandSet = new StringDict { ["name"] = "avg_npc_1", ["focus"] = "none" }
        };
        Assert.False(NovelAligner.IsEffectiveCharSlot(entry));
    }

    [Fact]
    public void IsEffectiveCharSlot_NoName_ReturnsFalse()
    {
        var entry = new FormattedTextEntry
        {
            CommandSet = new StringDict { ["focus"] = "r" }
        };
        Assert.False(NovelAligner.IsEffectiveCharSlot(entry));
    }

    [Fact]
    public void IsEffectiveCharSlot_EmptyCommandSet_ReturnsFalse()
    {
        var entry = new FormattedTextEntry { CommandSet = new StringDict() };
        Assert.False(NovelAligner.IsEffectiveCharSlot(entry));
    }

    [Fact]
    public void IsEffectiveCharSlot_NullCommandSet_ReturnsFalse()
    {
        var entry = new FormattedTextEntry();
        Assert.False(NovelAligner.IsEffectiveCharSlot(entry));
    }

    // ── helpers ─────────────────────────────────────

    private static FormattedTextEntry MakeCharSlot(int index, string name, string focus)
        => new()
        {
            Index = index,
            Type = "charslot",
            CommandSet = new StringDict { ["name"] = name, ["focus"] = focus },
        };

    private static FormattedTextEntry MakeDialog(int index, string characterName, string dialog)
        => new()
        {
            Index = index,
            Type = "dialog",
            CharacterName = characterName,
            Dialog = dialog,
        };
}
