using SqlSugar;
using System.Text.Json;

namespace ArkPlot.Core.Model;

/// <summary>
/// 表示格式化文本条目，包含原始文本及其转换后的多种格式
/// </summary>
[SugarTable("FormattedTextEntry")]
public class FormattedTextEntry
{
    /// <summary>
    /// 文本行的索引�?
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    /// <summary>
    /// 文本行的索引�?
    /// </summary>
    [SugarColumn(ColumnDataType = "INTEGER")]
    public int Index { get; set; }
    /// <summary>
    /// 原始文本内容
    /// </summary>
    [SugarColumn(Length = 1000)]
    public string OriginalText { get; set; } = "";

    [SugarColumn(Length = 1000)]
    public string MdText { get; set; } = "";

    public int MdDuplicateCounter { get; set; }

    [SugarColumn(Length = 1000)]
    public string TypText { get; set; } = "";

    [SugarColumn(Length = 50)]
    public string Type { get; set; } = "";

    [SugarColumn(IsIgnore = true)]
    public StringDict CommandSet { get; set; } = new();

    [SugarColumn(ColumnDataType = "TEXT")]
    public string CommandSetJson
    {
        get => JsonSerializer.Serialize(CommandSet);
        set => CommandSet = string.IsNullOrEmpty(value) ? new StringDict() : JsonSerializer.Deserialize<StringDict>(value) ?? new StringDict();
    }

    public bool IsTagOnly { get; set; }

    [SugarColumn(Length = 100)]
    public string CharacterName { get; set; } = "";

    [SugarColumn(Length = 1000)]
    public string Dialog { get; set; } = "";

    public int PngIndex { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ResourceUrlsJson
    {
        get => JsonSerializer.Serialize(ResourceUrls);
        set => ResourceUrls = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
    }

    [SugarColumn(IsIgnore = true)]
    public List<string> ResourceUrls { get; set; } = new();

    [SugarColumn(IsIgnore = true)]
    public PortraitInfo PortraitsInfo { get; set; } = new(new List<string>(), 0);

    [SugarColumn(ColumnDataType = "TEXT")]
    public string PortraitsInfoJson
    {
        get => JsonSerializer.Serialize(PortraitsInfo);
        set => PortraitsInfo = JsonSerializer.Deserialize<PortraitInfo>(value) ?? new PortraitInfo(new List<string>(), 0);
    }

    [SugarColumn(Length = 500)]
    public string Bg { get; set; } = "";

    /// <summary>
    /// 复制构造函�?
    /// </summary>
    /// <param name="entry">要复制的 FormattedTextEntry 实例</param>
    public FormattedTextEntry(FormattedTextEntry entry)
    {
        Index = entry.Index;
        OriginalText = entry.OriginalText;
        MdText = entry.MdText;
        MdDuplicateCounter = entry.MdDuplicateCounter;
        TypText = entry.TypText;
        Type = entry.Type;
        CommandSet = new(entry.CommandSet);
        IsTagOnly = entry.IsTagOnly;
        ResourceUrls = new(entry.ResourceUrls);
        CharacterName = entry.CharacterName;
        Dialog = entry.CharacterName;
        Bg = entry.Bg;
        PortraitsInfo = entry.PortraitsInfo;
    }

    /// <summary>
    /// 默认构造函�?
    /// </summary>
    public FormattedTextEntry()
    {
    }

    /// <summary>
    /// 验证数据完整�?
    /// </summary>
    /// <returns>验证结果</returns>
    public bool Validate()
    {
        // 基本验证
        if (string.IsNullOrEmpty(OriginalText) && string.IsNullOrEmpty(MdText) && string.IsNullOrEmpty(TypText))
        {
            return false; // 至少需要有一种格式的文本
        }

        // 索引验证
        if (Index < 0)
        {
            return false;
        }

        // 计数器验�?
        if (MdDuplicateCounter < 0)
        {
            return false;
        }

        // PNG索引验证
        if (PngIndex < 0)
        {
            return false;
        }

        return true;
    }
}
