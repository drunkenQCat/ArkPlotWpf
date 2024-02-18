using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
// the character name and character portrait pair
using CharPortrait = System.Collections.Generic.Dictionary<string, string>;
using SList = System.Collections.Generic.List<string>;
using SListGroup = System.Collections.Generic.List<System.Collections.Generic.List<string>>;

namespace ArkPlotWpf.Utilities.WorkFlow;

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
    public readonly SListGroup LineGroups = new();
    public readonly List<PortraitGrp> PortraitGrps = new();
    private SList lineList;

    /// <summary>
    /// 初始化 MdReconstructor 类的新实例，接受一个字符串参数作为 Markdown 文本输入。
    /// 此构造函数将原始 Markdown 文本分割成单独的行，进行初步清洗以移除空行，然后根据
    /// 段落分组文本并处理包含角色立绘信息的段落。
    /// </summary>
    /// <param name="md">原始的 Markdown 文本。</param>
    public MdReconstructor(string md)
    {
        lineList = new SList(md.Split("\r\n"));
        RemoveEmptyLines();
        GroupLinesBySegment();
        ProcessPortraits();
    }

    /// <summary>
    /// 初始化 MdReconstructor 类的新实例，接受一个字符串集合作为输入，代表Markdown文本的各个行。
    /// 此构造函数直接使用提供的文本行进行处理，不需要进行初步的文本分割。它会按段落分组这些行，并对
    /// 包含角色立绘信息的段落进行特殊处理。
    /// </summary>
    /// <param name="lines">表示Markdown文本行的字符串集合。</param>
    public MdReconstructor(IEnumerable<string> lines)
    {
        lineList = lines.ToList();
        GroupLinesBySegment();
        ProcessPortraits();
    }

    /// <summary>
    /// 清洗文本行，移除所有空行。
    /// 以保证后续处理过程中不会被空行干扰，确保文档内容的连贯性和整洁性。
    /// </summary>
    private void RemoveEmptyLines()
    {
        var linesWithoutEmptyLine =
            from line in lineList
            where !string.IsNullOrEmpty(line)
            select line;
        lineList = linesWithoutEmptyLine.ToList();
    }

    private void GroupLinesBySegment()
    {
        SList temp = new();
        var isPortraitGroup = false;
        var grpIndex = 0;
        foreach (var item in lineList)
        {
            temp = EvaluateAndGroupTextItem(item);

            // <img class="portrait"
            //             ^         it's 13th alphabet
            if (IsPortrait(item)) isPortraitGroup = true;

            temp.Add(item);
        }

        SList EvaluateAndGroupTextItem(string item)
        {
            if (!item.StartsWith('-') || temp.Count < 16) return temp;

            temp.RemoveAll(line => line.StartsWith('-'));
            LineGroups.Add(new SList(temp));
            if (isPortraitGroup) AppendPortrait(grpIndex, temp);
            PrepareForNextGroup();
            return temp;
        }
        void PrepareForNextGroup()
        {
            temp.Clear();
            grpIndex++;
            isPortraitGroup = false;
        }
    }

    private static bool IsPortrait(string item)
    {
        return item.Length > 12 && item[12] == 'p';
    }

    private void AppendPortrait(int grpIndex, SList temp)
    {
        SList deepCopy = new(temp);
        var characters = ExtractCharacterInfo(temp);
        PortraitGrps.Add(new PortraitGrp(grpIndex, deepCopy, characters.ToArray()));
    }

    /// <summary>
    /// 从给定段落中提取角色立绘信息，生成一个包含角色索引和名称的数组。
    /// </summary>
    /// <param name="paragraphLines">一个段落，由一系列文本行组成，其中可能包含角色立绘信息。</param>
    /// <returns>
    /// 一个 <see cref="IndexedCharacter"/> 数组，每个元素代表段落中识别到的一个角色及其立绘信息。
    /// </returns>
    /// <remarks>
    /// ExtractCharacterInfo 方法遍历段落中的每一行，识别包含立绘信息的行及其对应的角色名称。对于每个识别到的角色立绘，
    /// 方法会生成一个 IndexedCharacter 实例，其中包含角色立绘的索引位置、名称和立绘标记。这个过程允许后续步骤准确地处理
    /// 和替换立绘信息，确保角色立绘在最终文档中被正确展示。
    /// </remarks>
    /// <example>
    /// 假设一个段落中包含以下文本行：
    /// <code>
    /// &lt;img class='portrait' src='角色1立绘.png'  &lt;---第index行,PortraitHtml
    /// `立绘`角色1
    /// **某个名字**说道：`……`   &lt;-----Name:某个名字
    /// </code>
    /// 调用 ExtractCharacterInfo 方法后，将返回一个包含单个 IndexedCharacter 实例的数组，该实例表示识别到的角色1及其立绘信息。
    /// </example>
    private IEnumerable<IndexedCharacter> ExtractCharacterInfo(IEnumerable<string> paragraphLines)
    {
        var characters = new List<IndexedCharacter>();
        int index = 0;

        var lines = paragraphLines.ToList();
        foreach (var line in lines)
        {
            if (!IsPortrait(line))
            {
                index++;
                continue;
            }

            var characterName = ExtractCharacterNameFromLines(index, lines);
            characters.Add(!string.IsNullOrWhiteSpace(characterName)
                ? new IndexedCharacter(index, characterName, line)
                // 标记为异常或未识别的角色
                : new IndexedCharacter(index, "Unknown", string.Empty));
            index++;
        }

        return characters;
    }

    private string ExtractCharacterNameFromLines(int idx, IEnumerable<string> lines)
    {
        // 实现提取角色名称的逻辑，根据实际的标记格式进行调整
        // 角色名一般在链接后面第二行。
        var nameLine = lines.ElementAtOrDefault(idx + 2);
        if (string.IsNullOrEmpty(nameLine)) return "";
        var match = Regex.Match(nameLine, @"\*\*(.+?)\*\*.*");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return string.Empty;
    }

    /// <summary>
    /// 处理所有包含角色立绘信息的段落。此方法遍历所有已识别的包含立绘标记的段落组，
    /// 对每个组执行清理和立绘图表的插入操作。
    /// </summary>
    /// <remarks>
    /// 对于每个包含角色立绘标记的段落组，此方法首先调用 <see cref="CleanPortraitLines"/> 方法来移除立绘标记行，
    /// 然后使用 <see cref="MakePortraitChart"/> 方法根据段落中的角色立绘标记生成立绘图表，并插入到相应的位置。
    /// 此过程确保了每个角色的立绘在最终的 Markdown 文档中被正确展示，同时保持了文档内容的整洁和组织性。
    /// </remarks>
    private void ProcessPortraits()
    {
        var tasks = new List<Task>();
        foreach (var group in PortraitGrps)
        {
            Task ProcessSingleGroup()
            {
                CleanPortraitLines(group);
                CharPortrait portraitLinks = GenerateChartMap(group.PortraitMarks);
                MakePortraitChart(group, portraitLinks);
                LineGroups[group.Index] = group.SList;
                return Task.CompletedTask;
            }

            tasks.Add(ProcessSingleGroup());
        }
        Task.WhenAll(tasks);
    }

    /// <summary>
    /// 清除指定段落组中的所有角色立绘标记行。对于每个标记行，此方法将移除立绘本身以及相关的标记行，
    /// 以便在段落中只保留文本内容。
    /// </summary>
    /// <param name="group">包含立绘标记的段落组，是一个 <see cref="PortraitGrp"/> 实例。</param>
    /// <remarks>
    /// 此方法首先遍历段落组 <paramref name="group"/> 中的立绘标记，并针对每个立绘标记执行清理操作。
    /// 清理操作包括移除表示立绘的 HTML 标签行以及立即跟随的标记行（如角色名称）。此外，如果在清理后仍存在
    /// 以“立绘”开头的行，这些行也会被移除，确保最终的段落文本中不包含任何立绘相关的标记或残留文本。
    /// </remarks>
    /// <example>
    /// 下面的例子展示了在处理段落中的立绘信息时，如何识别并清理相关的标记行。假设我们有以下包含立绘标记的文本段落：
    /// 
    /// <code>
    /// // 示例文本包含立绘标记和角色说话的文本
    /// &lt;img class="portrait"...
    /// `立绘`name
    /// **Name**`说道：`....
    /// </code>
    /// 
    /// 在这种正常情况下，我们会移除与立绘直接相关的两行（即包含 `&lt;img class="portrait"...` 和 ``立绘`name`` 的行），
    /// 以便在最终的 Markdown 文档中只保留角色的对话文本（如 **Name**`说道：`....`）。
    /// </example>
    private void CleanPortraitLines(PortraitGrp group)
    {
        var portraitMark = group.PortraitMarks;
        foreach (var mark in portraitMark.Reverse())
        {
            var portraitIndex = mark.Index;
            group.SList.RemoveRange(portraitIndex, 2);
        }

        RemoveLiHui(group);
    }

    /// <summary>
    /// 移除指定段落组中所有“立绘”标记行。此方法用于最后一步清理，确保段落文本中不包含任何残留的“立绘”标记。
    /// </summary>
    /// <param name="group">一个 <see cref="PortraitGrp"/> 实例，表示需要处理的段落组。</param>
    private static void RemoveLiHui(PortraitGrp group)
    {
        // `立绘`grani#1 
        //  
        //  
        for (var i = group.SList.Count - 1; i >= 0; i--)
            if (group.SList[i].Contains("立绘"))
                group.SList.RemoveAt(i);
    }

    /// <summary>
    /// 根据提供的角色数组生成一个包含角色名称和对应立绘链接的字典。
    /// </summary>
    /// <param name="characters">一个 <see cref="IndexedCharacter"/> 数组，包含需要处理的角色信息。</param>
    /// <returns>
    /// 一个 <see cref="CharPortrait"/> 字典，键为角色名称，值为角色立绘的HTML标记。
    /// </returns>
    /// <remarks>
    /// 此方法遍历 <paramref name="characters"/> 数组中的每个角色，为每个角色创建一个带有标题的立绘链接HTML标记。
    /// 如果角色名称为"Unknown"，则跳过该角色，不将其添加到字典中。这样处理是为了排除异常或未标记的角色。
    /// </remarks>
    private CharPortrait GenerateChartMap(IndexedCharacter[] characters)
    {
        var charaDict = new CharPortrait();
        foreach (var character in characters.Reverse())
        {
            if (character.Name == "Unknown") continue;
            charaDict[character.Name] = EmbedTitleInPortraitHtml(character.Name, character.PortraitHtml);
        }
        return charaDict;
    }

    private string EmbedTitleInPortraitHtml(string title, string portrait)
    {
        // Assuming the portrait string is an HTML image tag, find the position to insert the title attribute
        int insertPosition = portrait.IndexOf(' ') + 1; // Find the first space, which should be right after the tag name
        string titleAttribute = $"title=\"{title}\" ";

        // Insert the title attribute into the portrait string
        string enhancedPortrait = portrait.Insert(insertPosition, titleAttribute);

        // Wrap the enhanced portrait in a div with a "crop" class
        return $"<div class=\"crop\">{enhancedPortrait}</div>";
    }

    /// <summary>
    /// 为给定的组和角色立绘链接生成立绘图表。
    /// </summary>
    /// <param name="group">立绘组。</param>
    /// <param name="portraitLinks">角色立绘链接。</param>
    private void MakePortraitChart(PortraitGrp group, CharPortrait portraitLinks)
    {
        if (portraitLinks.Count == 0) return;
        var chartItems = string.Join("|", portraitLinks.Values);
        var chartHead = $"|{chartItems}|";
        var chartSeg = string.Concat(Enumerable.Repeat(" --- |", portraitLinks.Count));
        chartSeg = $"|{chartSeg}";
        var chartBody = $"{chartHead}\r\n{chartSeg}\r\n\r\n";
        group.SList.Insert(0, chartBody);
    }

    /// <summary>
    /// 获取处理后的 Markdown 文档内容。此属性将组织好的文本行按照Markdown格式规则重新构建为完整的文档，
    /// 包括正确处理的角色立绘段落和其他Markdown元素。
    /// </summary>
    /// <value>
    /// 字符串类型，表示重构后的完整 Markdown 文档。
    /// </value>
    /// <remarks>
    /// Result 属性通过将所有处理过的文本行组合，并在适当的位置插入分隔符（如段落分隔符"---"），
    /// 来生成最终的Markdown文档。这个过程确保了文档的结构和格式都按照Markdown的标准进行组织，
    /// 并且所有的角色立绘信息都被正确地展示。
    /// </remarks>
    public string Result
    {
        get
        {
            var lines = LineGroups.Select(grp => string.Join("\r\n\r\n", grp));
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
        foreach (var group in LineGroups)
        {
            builder.Append("\r\n\r\n---\r\n\r\n");
            builder.AppendJoin("\r\n\r\n", group);
        }
        builder.AppendLine();
    }

    public record PortraitGrp(int Index, SList SList, IndexedCharacter[] PortraitMarks);

    public record IndexedCharacter(int Index, string Name, string PortraitHtml);
}
