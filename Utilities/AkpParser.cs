using System.Linq;
using ArkPlotWpf.Utilities.TagProcessingComponents;

namespace ArkPlotWpf.Utilities;

/// <summary>
/// AkpParser 类用于解析明日方舟剧情文本文件，并将其转换为 Markdown 文件。
/// </summary>
internal class AkpParser
{

    private readonly TagProcessor tagProcessor;
    StringBuilder plotBuilder = new();
    DuplicateLineTracker prevLine = new(SeparateLine);
    const string SeparateLine = "---";
    private readonly List<DuplicateLineTracker> linesCollection = new();

    public AkpParser(string jsonPath)
    {
        tagProcessor = new();
        tagProcessor.Rules.GetRegsFromJson(jsonPath);
    }

    /// <summary>
    /// 根据输入的剧情文本构建 Markdown 文档。
    /// </summary>
    /// <param name="inputBuilder">包含剧情文本的 StringBuilder 对象。</param>
    public void BuildMarkdown(StringBuilder inputBuilder)
    {
        var lines = inputBuilder.ToString().Split("\n");
        // 每一章的第一个有效句一定是分隔线
        prevLine = new(SeparateLine);
        InitializeBuilder(inputBuilder);

        foreach (var line in lines)
        {
            ProcessSingleLine(line);
        }

        var output = from l in linesCollection
                     select l.Line;
        var reconstructor = new MdReconstructor(output);
        reconstructor.AppendResultToBuilder(plotBuilder);
    }

    /// <summary>
    /// 初始化 Markdown 构建器。会将 plot builder 的地址转移为 input builder 的地址, 并清空 plot builder。随后清空 lines need url。
    /// </summary>
    /// <param name="inputBuilder">输入的字符串构建器。</param>
    private void InitializeBuilder(StringBuilder inputBuilder)
    {
        plotBuilder = inputBuilder;
        plotBuilder.Clear();
        linesCollection.Clear();
    }

    private void ProcessSingleLine(string line)
    {
        var classifiedLine = ClassifyAndProcess(line);
        DuplicateLineTracker currentLine = new(classifiedLine);
        
        if (IsDupOrEmptyLine(currentLine)) return;
        
        var newline = CombineDuplicateLines(currentLine);
        prevLine = currentLine;
        AppendAndDetectLinesNeedUrl(newline);
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
    /// 将新行添加到集合中，并检测需要URL的行。
    /// </summary>
    /// <param name="newLine">要添加和处理的行。这是一个<see cref="DuplicateLineTracker"/>实例，
    /// 用于跟踪潜在重复行的出现次数和内容。</param>
    /// <remarks>
    /// 此方法处理提供的<paramref name="newLine"/>，以确定它或其部分是否需要关联的URL。
    /// 如果行不是以换行符('\n')开头，它将直接被添加到<c>linesCollection</c>集合中。
    /// 如果行以换行符开头，则根据换行符将其分割成子行，并将每个子行作为新的<see cref="DuplicateLineTracker"/>实例
    /// 添加到<c>linesCollection</c>集合中。
    /// </remarks>
    /// <example>
    /// 这里是如何调用<c>AppendAndDetectLinesNeedUrl</c>的示例：
    /// <code>
    /// var tracker = new DuplicateLineTracker("示例行\n另一行");
    /// AppendAndDetectLinesNeedUrl(tracker);
    /// </code>
    /// 这将把"示例行"和"另一行"添加到<c>linesCollection</c>集合中，假设这些行需要URL。
    /// </example>
    private void AppendAndDetectLinesNeedUrl(DuplicateLineTracker newLine)
    {
        if (newLine.Line[0] != '\n')
        {
            linesCollection.Add(newLine);
            return;
        }
        var newLineSplited = newLine.Line.TrimStart().Split('\n');
        foreach (string s in newLineSplited)
        {
            linesCollection.Add(new DuplicateLineTracker(s));
        }
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
