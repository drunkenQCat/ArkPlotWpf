using System.Linq;
using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.TagProcessingComponents;

namespace ArkPlot.Core.Utilities.WorkFlow;

/// <summary>
/// AkpParser 类用于解析明日方舟剧情文本文件，并将其转换为 Markdown 文件。
/// </summary>
public class AkpParser
{

    private readonly TagProcessor _tagProcessor;
    const string SeparateLine = "---";
    public bool IsInitialized;

    FormattedTextEntry _prevLine = new() { MdText = SeparateLine };
    public AkpParser(string jsonPath)
    {
        _tagProcessor = new();
        _tagProcessor.Rules.GetRegsFromJson(jsonPath);
    }

    /// <summary>
    /// 在每一章开始解析之前，初始化解析器
    /// </summary>
    public void InitializeParser()
    {
        // 每一章的第一个有效句一定是分隔线
        _prevLine = new FormattedTextEntry { MdText = SeparateLine };
        IsInitialized = true;
    }

    public string ProcessSingleLine(FormattedTextEntry line)
    {
        var classifiedLine = ClassifyAndProcess(line);
        /* FormattedTextEntry currentLine = new(classifiedLine); */
        FormattedTextEntry currentLine = new(line)
        {
            MdText = classifiedLine
        };

        if (IsDupOrEmptyLine(currentLine)) return "";

        var newline = CombineDuplicateLines(currentLine);
        _prevLine = currentLine;
        return newline.MdText;
    }

    /// <summary>
    /// 对给定的行进行分类和处理。
    /// </summary>
    /// <param name="line">要处理的行。</param>
    /// <returns>处理后的行。</returns>
    private string ClassifyAndProcess(FormattedTextEntry line)
    {
        var sentenceProcessor = _tagProcessor.Rules.RegexAndMethods
            .FirstOrDefault(proc => proc.Regex.Match(line.OriginalText).Success);
        if (sentenceProcessor == null) return line.OriginalText;
        var result = sentenceProcessor.Method(line);
        return result;
    }

    bool IsDupOrEmptyLine(FormattedTextEntry newLine)
    {
        if (newLine.MdText == "") return true;
        if (newLine.MdText != _prevLine.MdText) return false;
        newLine.MdDuplicateCounter++;
        return true;
    }

    FormattedTextEntry CombineDuplicateLines(FormattedTextEntry currentLine)
    {
        if (currentLine.MdDuplicateCounter <= 1 || _prevLine.MdText == SeparateLine) return currentLine;
        // 先对输入量深拷贝。
        /* FormattedTextEntry newLine = new(currentLine.MdText); */
        FormattedTextEntry newLine = new(currentLine)
        {
            MdDuplicateCounter = currentLine.MdDuplicateCounter
        };
        // 合并重复的行数，比如: 音效：sword x 5
        newLine.MdText.TrimEnd();
        newLine.MdText = _prevLine.MdText + " × " + newLine.MdDuplicateCounter;
        return newLine;
    }
}
