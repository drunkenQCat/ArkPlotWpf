namespace ArkPlotWpf.Model;

/// <summary>
/// 表示格式化文本条目，包含原始文本及其转换后的多种格式
/// </summary>
public class FormattedTextEntry
{
    /// <summary>
    /// 文本行的索引号
    /// </summary>
    public int Index;
    /// <summary>
    /// 原始文本内容
    /// </summary>
    public string OriginalText = "";
    /// <summary>
    /// 转换为 Markdown 格式的文本
    /// </summary>
    public string MdText = "";
    /// <summary>
    /// Markdown 文本重复计数器
    /// </summary>
    public int MdDuplicateCounter;
    /// <summary>
    /// 转换为 Typst 格式的代码
    /// </summary>
    public string TypText = "";
    /// <summary>
    /// 文本类型标识
    /// </summary>
    public string Type = "";
    /// <summary>
    /// 命令集合字典
    /// </summary>
    public StringDict CommandSet = new();
    /// <summary>
    /// 标识是否仅为标签内容
    /// </summary>
    public bool IsTagOnly { get; set; }
    /// <summary>
    /// 角色名称
    /// </summary>
    public string CharacterName = "";
    /// <summary>
    /// 对话内容
    /// </summary>
    public string Dialog { get; set; } = "";
    /// <summary>
    /// PNG 图片索引号
    /// </summary>
    public int PngIndex { get; set; }

    /// <summary>
    /// 资源 URL 列表
    /// </summary>
    public List<string> ResourceUrls = new();
    /// <summary>
    /// 角色立绘信息
    /// </summary>
    public PortraitInfo PortraitsInfo = new(new List<string>(), 0);
    /// <summary>
    /// 背景图片 URL
    /// </summary>
    public string Bg = "";

    /// <summary>
    /// 复制构造函数
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
    /// 默认构造函数
    /// </summary>
    public FormattedTextEntry()
    {
    }

    /// <summary>
    /// 验证数据完整性
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

        // 计数器验证
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
