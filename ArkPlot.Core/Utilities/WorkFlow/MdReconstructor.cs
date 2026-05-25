using ArkPlot.Core.Model;
using System.Linq;
using System.Threading.Tasks;
using ArkPlot.Core.Services;
// the character name and character portrait pair
using CharacterChart = System.Collections.Generic.Dictionary<string, string>;
using EntryList = System.Collections.Generic.List<ArkPlot.Core.Model.FormattedTextEntry>;
using EntryGroups = System.Collections.Generic.List<System.Collections.Generic.List<ArkPlot.Core.Model.FormattedTextEntry>>;

namespace ArkPlot.Core.Utilities.WorkFlow;

/// <summary>
/// MdReconstructor 类用于构建和重构 Markdown 格式文档，通过解析原始文本、处理角色立绘标记、
/// 并按照一定的格式规则组织文本内容。这个类提供了一种自动化的方式来整理和格式化分散的文本片段，
/// 使其成为一个结构化且易于阅读的 Markdown 文档。
/// </summary>
/// <remarks>
/// 该类支持从原始的 Markdown 文本或分散的文本行中创建实例。它首先进行初步的文本清洗，移除不必要的空行，
/// 然后根据段落分组线条，将文本分组为多个部分。对于包含角色立绘信息的段落，MdReconstructor
/// 会自动处理相关的立绘标记，并在适当的位置插入立绘图表。
/// </remarks>
public class MdReconstructor
{
    private readonly EntryGroups lineGroups = new();
    private readonly List<PortraitGrp> portraitGroups = new();
    private EntryList lineList;
    private readonly List<int> portraitIndexes = new();
    private readonly PicDescService? _picDescService;
    // 用于跟踪已输出描述的图片 URL，确保每个 URL 只描述一次
    private readonly HashSet<string> _describedImages = new();

    /// <summary>
    /// 初始化 MdReconstructor 类的新实例。
    /// </summary>
    /// <param name="entries">原始的 FormattedTextEntry 列表。</param>
    /// <param name="picDescService">可选的图片描述服务，用于为 HTTP 图片链接写入 PicDesc。</param>
    /// <param name="describedImages">可选的已描述图片集合，跨章节共享以去重。为 null 时创建新实例。</param>
    public MdReconstructor(EntryList entries, PicDescService? picDescService = null, HashSet<string>? describedImages = null)
    {
        _picDescService = picDescService;
        _describedImages = describedImages ?? new HashSet<string>();
        lineList = new(entries);
        ProcessPicDescsAsync().GetAwaiter().GetResult();
        RemoveEmptyLines();
        GroupLinesBySegment();
        ProcessPortraits();
        RemovePortraitLines();
    }

    /// <summary>
    /// 遍历所有条目，为含有 HTTP 图片链接的条目写入 PicDesc。
    /// 每个图片链接只对应一条 desc，由 PicDescService 保证不重复。
    /// 有 Ollama 客户端时调用视觉模型生成描述；否则使用占位符。
    /// </summary>
    private async Task ProcessPicDescsAsync()
    {
        if (_picDescService == null) return;

        foreach (var entry in lineList)
        {
            if (entry.ResourceUrls.Count == 0) continue;

            // 为每个资源 URL 获取或创建 PicDesc（可能触发下载 + Ollama 描述）
            var picDescs = await _picDescService.GetOrCreatePicDescsAsync(entry.ResourceUrls);

            // 将所有 PicDesc 合并为一个字符串（用分号分隔）
            entry.PicDesc = string.Join("; ", picDescs.Values);
        }
    }

    /// <summary>
    /// 清洗文本行，移除所有空行。
    /// 以保证后续处理过程中不会被空行干扰，确保文档内容的连贯性和整洁性。
    /// </summary>
    private void RemoveEmptyLines()
    {
        var linesWithoutEmptyLine =
            from line in lineList
            where !string.IsNullOrWhiteSpace(line.MdText)
            select line;
        lineList = linesWithoutEmptyLine.ToList();
        int idx = 0;
        lineList.ForEach(line =>
        {
            line.Index = idx;
            idx++;
        });
    }

    private void GroupLinesBySegment()
    {
        EntryList temp = new();
        bool isPortraitGroup = false;

        foreach (var item in lineList)
        {
            if (IsItemOnlyDashes(item) || temp.Count < 16)
            {
                if (IsPortrait(item)) isPortraitGroup = true;
                temp.Add(item);
                continue;
            }

            // 如果上面的代码没有 continue，那就说明凑成一组，可以写了。
            RemoveLeadingDashes(temp);
            GroupRemainingText(temp, isPortraitGroup);
            // 为下一组做准备。
            temp = new EntryList();
            isPortraitGroup = false;
            temp.Add(item);
        }
    }

    bool IsItemOnlyDashes(FormattedTextEntry item)
    {
        return !item.MdText.StartsWith('-');
    }

    private void RemoveLeadingDashes(EntryList entries)
    {
        entries.ForEach(item =>
        {
            if (item.MdText.StartsWith('-')) item.MdText = "";
        });
    }

    private void GroupRemainingText(EntryList entries, bool isPortraitGroup)
    {
        if (entries.Count > 0)
        {
            var newGroup = new EntryList(entries);
            lineGroups.Add(newGroup);
            if (isPortraitGroup) AppendPortrait(newGroup);
        }
    }

    private static bool IsPortrait(FormattedTextEntry item)
    {
        return item.Type.Contains("Char") || item.Type.Contains("char");
    }

    private void AppendPortrait(EntryList grp)
    {
        var characters = ExtractCharacterInfo(grp);
        portraitGroups.Add(new PortraitGrp(grp, characters));
    }

    /// <summary>
    /// 从给定段落中提取角色立绘信息，生成一个包含角色索引和名称的数组。
    /// </summary>
    /// <param name="paragraphLines">一个段落，由一系列文本行组成，其中可能包含角色立绘信息。</param>
    /// <returns>
    /// 一个 <see cref="CharacterInfo"/> 数组，每个元素代表段落中识别到的一个角色及其立绘信息。
    /// </returns>
    /// <remarks>
    /// ExtractCharacterInfo 方法遍历段落中的每一行，识别包含立绘信息的行及其对应的角色名称。对于每个识别到的角色立绘，
    /// 方法会生成一个 IndexedCharacter 实例，其中包含角色立绘的索引位置、名称和立绘标记。这个过程允许后续步骤准确地处理
    /// 和替换立绘信息，确保角色立绘在最终文档中被正确展示。
    /// </remarks>
    /// <example>
    /// 假设一个段落中包含以下文本行：
    /// <code>
    /// &lt;img class='portrait' src='角色1立绘.png'  &lt;---第index行 PortraitHtml
    /// `立绘`角色1
    /// **某个名字**说道："{……}"   &lt;-----Name:某个名字
    /// </code>
    /// 调用 ExtractCharacterInfo 方法后，将返回一个包含单个 IndexedCharacter 实例的数组，该实例表示识别到的角色及其立绘信息。
    /// </example>
    private List<CharacterInfo> ExtractCharacterInfo(EntryList paragraphLines)
    {
        var characters = new List<CharacterInfo>();

        var lines = paragraphLines;
        foreach (var line in lines)
        {
            if (!IsPortrait(line)) continue;

            portraitIndexes.Add(line.Index);
            var characterName = ExtractCharacterNameFromLines(line);

            // 所有的 url 标签都附加两个换行符，一个描述。
            var url = line.MdText.Split("\r\n")[0];
            characters.Add(!string.IsNullOrWhiteSpace(characterName)
                ? new CharacterInfo(line, characterName, url)
                // 标记为异常或未识别的角色
                : new CharacterInfo(line, "Unknown", string.Empty));
        }
        return characters;
    }

    private string ExtractCharacterNameFromLines(FormattedTextEntry line)
    {
        // 实现提取角色名称的逻辑，根据实际的标记格式进行调整
        // 角色名一般在链接后面第二行。
        var nameEntry = lineList.ElementAtOrDefault(line.Index + 1);
        var canReadName = nameEntry is not null && !string.IsNullOrEmpty(nameEntry.CharacterName);
        if (!canReadName) return "";
        return nameEntry!.CharacterName;
    }

    /// <summary>
    /// 处理所有包含角色立绘信息的段落。此方法遍历所有已识别的包含立绘标记的段落组，
    /// 对每个组执行清理和立绘图表的插入操作。
    /// </summary>
    /// <remarks>
    /// 对于每个包含角色立绘标记的段落组，此方法首先调用 <see cref="RemovePortraitLines"/> 方法来移除立绘标记行，
    /// 然后使用 <see cref="MakePortraitChart"/> 方法根据段落中的角色立绘标记生成立绘图表，并插入到相应的位置。
    /// 此过程确保了每个角色的立绘在最终的 Markdown 文档中被正确展示，同时保持了文档内容的整洁和组织性。
    /// </remarks>
    private void ProcessPortraits()
    {
        var tasks = new List<Task>();
        foreach (var group in portraitGroups)
        {
            Task ProcessSingleGroup()
            {
                CharacterChart portraitLinks = GenerateChartDict(group.PortraitMarks);
                MakePortraitChart(group, portraitLinks);
                return Task.CompletedTask;
            }

            tasks.Add(ProcessSingleGroup());
        }
        Task.WhenAll(tasks);
    }

    /// <summary>
    /// 根据提供的角色数组生成一个包含角色名称和对应立绘链接的字典。
    /// </summary>
    /// <param name="characters">一个 <see cref="CharacterInfo"/> 数组，包含需要处理的角色信息。</param>
    /// <returns>
    /// 一个 <see cref="CharacterChart"/> 字典，键为角色名称，值为角色立绘的 HTML 标记。
    /// </returns>
    /// <remarks>
    /// 此方法遍历 <paramref name="characters"/> 数组中的每个角色，为每个角色创建一个带有标题的立绘链接 HTML 标记。
    /// 如果角色名称为 "Unknown"，则跳过该角色，不将其添加到字典中。这样处理是为了排除异常或未标记的角色。
    /// </remarks>
    private CharacterChart GenerateChartDict(List<CharacterInfo> characters)
    {
        var charaDict = new CharacterChart();
        foreach (var character in characters)
        {
            if (character.Name == "Unknown") continue;
            charaDict[character.Name] = EmbedTitleInPortraitHtml(character);
        }
        return charaDict;
    }

    private string EmbedTitleInPortraitHtml(CharacterInfo character)
    {
        // Assuming the portrait string is an HTML image tag, find the position to insert the title attribute
        // example:
        // <img class="portrait" src="https://prts.wiki/images/e/e0/Avg_char_220_grani_3.png" alt="char_220_grani#5" loading="lazy" style="max-height:300px">
        var htmlTag = new HtmlTagParser(character.PortraitHtml)
        {
            Attributes =
            {
                ["title"] = character.Name
            }
        };
        var portraitUrl = GetPortraitUrl(character.OriginalEntry);
        if (!string.IsNullOrEmpty(portraitUrl)) htmlTag.Attributes["src"] = portraitUrl;
        var enhancedPortrait = htmlTag.ReconstructHtml();
        // Wrap the enhanced portrait in a div with a "crop" class
        return $"<div class=\"crop\">{enhancedPortrait}</div>";
    }

    private string GetPortraitUrl(FormattedTextEntry characterOriginalEntry)
    {
        var url = "";
        // 如果带有 focus 的话，就说明有两个或以上的立绘。
        // [Character(name=\"avg_npc_003\",name2=\"char_220_grani#3\",focus=2)]
        _ = characterOriginalEntry.CommandSet.TryGetValue("focus", out string? focusIndex);
        if (int.TryParse(focusIndex, out var focusIdx))
        {
            if (focusIdx > 0 && focusIdx <= characterOriginalEntry.ResourceUrls.Count) return characterOriginalEntry.ResourceUrls[focusIdx - 1];
        }
        return url;
    }

    /// <summary>
    /// 为给定的组和角色立绘链接生成立绘图表。
    /// 每个角色立绘 URL 只描述一次，追加在表格下方。
    /// </summary>
    /// <param name="group">立绘组。</param>
    /// <param name="portraitLinks">角色立绘链接。</param>
    private void MakePortraitChart(PortraitGrp group, CharacterChart portraitLinks)
    {
        if (portraitLinks.Count == 0) return;
        var chartItems = string.Join("|", portraitLinks.Values);
        var chartHead = $"|{chartItems}|";
        var chartSeg = string.Concat(Enumerable.Repeat(" --- |", portraitLinks.Count));
        chartSeg = $"|{chartSeg}";
        var chartBody = $"{chartHead}\r\n{chartSeg}\r\n\r\n";

        // 生成立绘描述（每个 URL 只追加一次）
        var portraitDescs = GeneratePortraitDescriptions(group.PortraitMarks);

        var firstLine = group.SList.First();
        firstLine.MdText = firstLine.MdText.Insert(0, chartBody + portraitDescs);
    }

    /// <summary>
    /// 为立绘组生成描述文本，每张图片 URL 只描述一次。
    /// 格式：*[立绘·角色名：...]*
    /// </summary>
    private string GeneratePortraitDescriptions(List<CharacterInfo> portraitMarks)
    {
        if (_picDescService == null) return "";

        var sb = new StringBuilder();
        foreach (var mark in portraitMarks)
        {
            // 跳过 Unknown 角色
            if (mark.Name == "Unknown") continue;

            // 获取立绘 URL 列表
            var urls = mark.OriginalEntry.ResourceUrls;
            if (urls.Count == 0) continue;

            foreach (var url in urls)
            {
                // 已描述过的 URL 跳过
                if (_describedImages.Contains(url)) continue;

                // 获取描述
                var desc = mark.OriginalEntry.PicDesc;
                if (string.IsNullOrWhiteSpace(desc))
                {
                    // 如果 PicDesc 还没填充，尝试从服务获取
                    var descTask = _picDescService.GetOrCreatePicDescAsync(url);
                    desc = descTask.GetAwaiter().GetResult();
                }

                if (string.IsNullOrWhiteSpace(desc) || desc.StartsWith("[PIC_DESC:") || desc.StartsWith("[DESC_ERROR:"))
                    continue;

                _describedImages.Add(url);
                sb.AppendLine($"*[立绘·{mark.Name}：{desc}]*");
            }
        }

        if (sb.Length > 0)
            sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// 在制作完成表格之后，原本的立绘便要删除。
    /// </summary>
    private void RemovePortraitLines()
    {
        foreach (var digit in portraitIndexes)
        {
            lineList[digit].MdText = "";
        }
    }

    /// <summary>
    /// 获取处理后的 Markdown 文档内容。此属性将组织好的文本行按照 Markdown 格式规则重新构建为完整的文档，
    /// 包括正确处理的角色立绘段落和其他 Markdown 元素。
    /// </summary>
    /// <value>
    /// 字符串类型，表示重构后的完整 Markdown 文档。
    /// </value>
    /// <remarks>
    /// Result 属性通过将所有处理过的文本行组合，并在适当的位置插入分隔符（如段落分隔符 "---"），
    /// 来生成最终的 Markdown 文档。这个过程确保了文档的结构和格式都按照 Markdown 的标准进行组织，
    /// 并且所有的角色立绘信息都被正确地展示。
    /// </remarks>
    public string Result
    {
        get
        {
            var lines = lineGroups.Select(grp => string.Join("\r\n\r\n", grp));
            return "\r\n" + string.Join("\r\n\r\n---\r\n\r\n", lines) + "\r\n";
        }
    }

    /// <summary>
    /// 将结果追加到字符串构建器中。
    /// </summary>
    /// <param name="builder">字符串构建器。</param>
    public void AppendResultToBuilder(StringBuilder builder)
    {
        builder.AppendLine();
        foreach (var group in lineGroups)
        {
            builder.Append("\r\n\r\n---\r\n\r\n");
            builder.AppendJoin("\r\n\r\n", GetRawMdLines(group));
        }
        builder.AppendLine();
    }


    List<string> GetRawMdLines(EntryList grp)
    {
        var mdList = new List<string>();
        foreach (var entry in grp)
        {
            if (string.IsNullOrWhiteSpace(entry.MdText)) continue;

            mdList.Add(entry.MdText);

            // 为非立绘的图片条目追加描述（每个 URL 只描述一次）
            if (entry.ResourceUrls.Count > 0 && !string.IsNullOrEmpty(entry.PicDesc) && _picDescService != null)
            {
                foreach (var url in entry.ResourceUrls)
                {
                    if (_describedImages.Contains(url)) continue;

                    var desc = entry.PicDesc;
                    // 过滤占位符、错误信息、空值、纯分号
                    if (string.IsNullOrWhiteSpace(desc)
                        || desc.Trim() == ";"
                        || desc.StartsWith("[PIC_DESC:")
                        || desc.StartsWith("[DESC_ERROR:"))
                        continue;

                    _describedImages.Add(url);
                    mdList.Add($"*[插图：{desc}]*");
                }
            }
        }
        return mdList;
    }

    private record PortraitGrp(EntryList SList, List<CharacterInfo> PortraitMarks);

    private record CharacterInfo(FormattedTextEntry OriginalEntry, string Name, string PortraitHtml);
}
