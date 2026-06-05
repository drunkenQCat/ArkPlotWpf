namespace ArkPlot.Tts;

/// <summary>
/// 中文音色池定义。
/// 按性别分组，旁白音色独占不参与角色分配。
/// </summary>
public static class VoicePool
{
    /// <summary>女声音色池（4个）。</summary>
    public static readonly string[] Female =
    [
        "zh-CN-XiaoyiNeural",
        "zh-CN-liaoning-XiaobeiNeural",
        "zh-TW-HsiaoChenNeural",
        "zh-TW-HsiaoYuNeural",
    ];

    /// <summary>男声音色池（4个）。</summary>
    public static readonly string[] Male =
    [
        "zh-CN-YunxiNeural",
        "zh-CN-YunjianNeural",
        "zh-CN-YunxiaNeural",
        "zh-CN-YunyangNeural",
    ];

    /// <summary>旁白专用音色（不参与角色分配）。</summary>
    public const string Narrator = "zh-CN-XiaoxiaoNeural";

    /// <summary>所有女声音色名（用于判断某音色是否为女声）。</summary>
    public static readonly HashSet<string> FemaleVoiceNames = new(Female);

    /// <summary>所有男声音色名。</summary>
    public static readonly HashSet<string> MaleVoiceNames = new(Male);

    /// <summary>判断一个音色是否为女声。</summary>
    public static bool IsFemaleVoice(string voice) =>
        FemaleVoiceNames.Contains(voice) || voice == Narrator;

    /// <summary>判断一个音色是否为男声。</summary>
    public static bool IsMaleVoice(string voice) =>
        MaleVoiceNames.Contains(voice);
}
