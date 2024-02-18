using System.Linq;
using ArkPlotWpf.Model;
using ArkPlotWpf.Utilities.TagProcessingComponents;

namespace ArkPlotWpf.Utilities.WorkFlow;

/// <summary>
/// AkpParser 类用于解析明日方舟剧情文本文件，并将其转换为 Markdown 文件。
/// </summary>
public class AkpParser
{

    private readonly TagProcessor tagProcessor;
    private List<FormattedTextEntry> allEntries;
    const string SeparateLine = "---";

    DuplicateLineTracker prevLine = new(SeparateLine);
    public AkpParser(string jsonPath)
    {
        tagProcessor = new();
        tagProcessor.Rules.GetRegsFromJson(jsonPath);
    }

    /// <summary>
    /// 根据输入的剧情文本构建 Markdown 文档。
    /// </summary>
    /// <param name="formattedTextEntries"></param>
    /// <param name="lines">包含剧情文本的 StringBuilder 对象。</param>
    public void InitializeParser(List<FormattedTextEntry> formattedTextEntries)
    {
        allEntries = formattedTextEntries;
        // 每一章的第一个有效句一定是分隔线
        prevLine = new(SeparateLine);
        // foreach (var line in lines)
        // {
        //     line.MdText = ProcessSingleLine(line);
        // }
    }

    /// <summary>
    /// 初始化 Markdown 构建器。会将 plot builder 的地址转移为 input builder 的地址, 并清空 plot builder。随后清空 lines need url。
    /// </summary>
    /// <param name="inputBuilder">输入的字符串构建器。</param>
    public string ProcessSingleLine(string line)
    {
        var classifiedLine = ClassifyAndProcess(line);
        DuplicateLineTracker currentLine = new(classifiedLine);

        if (IsDupOrEmptyLine(currentLine)) return "";

        var newline = CombineDuplicateLines(currentLine);
        prevLine = currentLine;
        return newline.Line;
    }

    /// <summary>
    /// 对给定的行进行分类和处理。
    /// </summary>
    /// <param name="line">要处理的行。</param>
    /// <returns>处理后的行。</returns>
    private string ClassifyAndProcess(string line)
    {
        var sentenceProcessor = tagProcessor.Rules.RegexAndMethods
            .FirstOrDefault(proc => proc.Regex.Match(line).Success);
        if (sentenceProcessor == null) return line;
        var result = sentenceProcessor.Method(line);
        return result;
    }

    bool IsDupOrEmptyLine(DuplicateLineTracker newLine)
    {
        if (newLine.Line == "") return true;
        if (newLine.Line != prevLine.Line) return false;
        newLine.Counter++;
        return true;
    }

    DuplicateLineTracker CombineDuplicateLines(DuplicateLineTracker currentLine)
    {
        if (currentLine.Counter <= 1 || prevLine.Line == SeparateLine) return currentLine;
        // 先对输入量深拷贝。
        DuplicateLineTracker newLine = new(currentLine.Line);
        newLine.Counter = currentLine.Counter;
        // 合并重复的行数，比如: 音效：sword x 5
        newLine.Line.TrimEnd();
        newLine.Line = prevLine.Line + " × " + newLine.Counter;
        return newLine;
    }

    /// <summary>
    /// Represents a tracker for duplicate lines.
    /// </summary>
    private class DuplicateLineTracker
    {
        /// <summary>
        /// Gets or sets the line being tracked.
        /// </summary>
        public string Line;

        /// <summary>
        /// Gets or sets the counter for the number of duplicates.
        /// </summary>
        public int Counter = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateLineTracker"/> class with the specified line.
        /// </summary>
        /// <param name="line">The line to be tracked.</param>
        public DuplicateLineTracker(string line)
        {
            Line = line;
        }
    }
}
