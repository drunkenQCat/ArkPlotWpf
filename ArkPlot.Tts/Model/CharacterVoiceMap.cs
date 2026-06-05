using SqlSugar;

namespace ArkPlot.Tts.Model;

/// <summary>
/// 角色音色映射表。持久化 VoiceManager 的分配结果，保证跨运行一致。
/// </summary>
[SugarTable("CharacterVoiceMap")]
public class CharacterVoiceMap
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 100)]
    public string CharacterName { get; set; } = "";

    [SugarColumn(Length = 20, IsNullable = true)]
    public string? Gender { get; set; }

    [SugarColumn(Length = 100)]
    public string Voice { get; set; } = "";

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
