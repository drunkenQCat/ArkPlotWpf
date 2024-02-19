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
    private List<FormattedTextEntry> allEntries = new();
    const string SeparateLine = "---";
    public bool IsInitialized = false;

    FormattedTextEntry prevLine = new() { MdText = SeparateLine };
    public AkpParser(string jsonPath)
    {
        tagProcessor = new();
        tagProcessor.Rules.GetRegsFromJson(jsonPath);
    }

    /// <summary>
    /// 在每一章开始解析之前，初始化解析器
    /// </summary>
    /// <param name="formattedTextEntries">包含剧情文本的 FormattedTextEntries 对象。</param>
    public void InitializeParser(List<FormattedTextEntry> formattedTextEntries)
    {
        allEntries = formattedTextEntries;
        // 每一章的第一个有效句一定是分隔线
        prevLine = new FormattedTextEntry { MdText = SeparateLine };
        IsInitialized = true;
    }

    /// <summary>
    /// 初始化 Markdown 构建器。会将 plot builder 的地址转移为 input builder 的地址, 并清空 plot builder。随后清空 lines need url。
    /// </summary>
    /// <param name="inputBuilder">输入的字符串构建器。</param>
    public string ProcessSingleLine(FormattedTextEntry line)
    {
        var classifiedLine = ClassifyAndProcess(line.OriginalText);
        /* FormattedTextEntry currentLine = new(classifiedLine); */
        FormattedTextEntry currentLine = new(line)
        {
            MdText = classifiedLine
        };

        if (IsDupOrEmptyLine(currentLine)) return "";

        var newline = CombineDuplicateLines(currentLine);
        prevLine = currentLine;
        return newline.MdText;
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

    bool IsDupOrEmptyLine(FormattedTextEntry newLine)
    {
        if (newLine.MdText == "") return true;
        if (newLine.MdText != prevLine.MdText) return false;
        newLine.MdDuplicateCounter++;
        return true;
    }

    FormattedTextEntry CombineDuplicateLines(FormattedTextEntry currentLine)
    {
        if (currentLine.MdDuplicateCounter <= 1 || prevLine.MdText == SeparateLine) return currentLine;
        // 先对输入量深拷贝。
        /* FormattedTextEntry newLine = new(currentLine.MdText); */
        FormattedTextEntry newLine = new(currentLine)
        {
            MdDuplicateCounter = currentLine.MdDuplicateCounter
        };
        // 合并重复的行数，比如: 音效：sword x 5
        newLine.MdText.TrimEnd();
        newLine.MdText = prevLine.MdText + " × " + newLine.MdDuplicateCounter;
        return newLine;
    }
}
