using ArkPlot.Core.Model;
using Newtonsoft.Json;

namespace ArkPlot.Cli.Dump;

public record DumpResult(string DumpPath, string Stats, int PicDescCount);

public class PlotDump
{
    [JsonProperty("meta")]
    public required DumpMeta Meta { get; init; }

    [JsonProperty("text_variants")]
    public required List<FormattedTextEntryDump> TextVariants { get; init; }
}

public class DumpMeta
{
    [JsonProperty("activity")]
    public required string Activity { get; init; }

    [JsonProperty("chapter")]
    public required string Chapter { get; init; }

    [JsonProperty("title")]
    public required string Title { get; init; }

    [JsonProperty("dump_time")]
    public required string DumpTime { get; init; }

    [JsonProperty("total_entries")]
    public required int TotalEntries { get; init; }

    [JsonProperty("valid_md_entries")]
    public required int ValidMdEntries { get; init; }

    [JsonProperty("valid_typ_entries")]
    public required int ValidTypEntries { get; init; }
}

public class FormattedTextEntryDump
{
    [JsonProperty("index")]
    public int Index { get; init; }

    [JsonProperty("original_text")]
    public string OriginalText { get; init; } = "";

    [JsonProperty("md_text")]
    public string MdText { get; init; } = "";

    [JsonProperty("md_duplicate_counter")]
    public int MdDuplicateCounter { get; init; }

    [JsonProperty("typ_text")]
    public string TypText { get; init; } = "";

    [JsonProperty("type")]
    public string Type { get; init; } = "";

    [JsonProperty("is_tag_only")]
    public bool IsTagOnly { get; init; }

    [JsonProperty("character_name")]
    public string CharacterName { get; init; } = "";

    [JsonProperty("dialog")]
    public string Dialog { get; init; } = "";

    [JsonProperty("png_index")]
    public int PngIndex { get; init; }

    [JsonProperty("bg")]
    public string Bg { get; init; } = "";

    [JsonProperty("resource_urls")]
    public List<string> ResourceUrls { get; init; } = new();

    [JsonProperty("portraits")]
    public List<string> Portraits { get; init; } = new();

    [JsonProperty("portrait_focus")]
    public int PortraitFocus { get; init; }

    [JsonProperty("command_set")]
    public StringDict CommandSet { get; init; } = new();

    [JsonProperty("pic_desc")]
    public string PicDesc { get; init; } = "";
}
