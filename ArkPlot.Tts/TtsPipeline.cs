using ArkPlot.Tts.Alignment;

namespace ArkPlot.Tts;

/// <summary>TTS 输入模式。</summary>
public enum TtsInputMode
{
    /// <summary>小说化 md 文件，按章节生成多个 MP3。</summary>
    NovelChapter,

    /// <summary>对齐后的 JSON 文件，生成单个 MP3。</summary>
    AlignedJson,

    /// <summary>原始 FormattedTextEntry 列表（主管线用），生成单个 MP3。</summary>
    RawEntries,
}

/// <summary>TTS 管线请求。</summary>
public record TtsRequest(
    TtsInputMode Mode,
    string InputPath,
    string OutputDir,
    string Rate = "+0%",
    string Volume = "+0%",
    int? SegmentLimit = null,
    bool DebugVoiceOnly = false,
    int RequestDelayMs = 1000
);

/// <summary>TTS 管线结果。</summary>
public record TtsPipelineResult(
    int TotalSegments,
    int SynthesizedCount,
    int CacheHitCount,
    int SkippedCount,
    List<string> OutputFiles
);

/// <summary>TTS 管线中的单个片段。</summary>
internal record TtsSegment(
    string Text,
    string Voice,
    string Label,
    string? ChapterTitle = null
);
