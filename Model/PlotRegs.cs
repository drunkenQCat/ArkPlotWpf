using Newtonsoft.Json.Linq;
using System;
using System.Text.RegularExpressions;

namespace ArkPlotWpf.Model;

public partial class PlotRegs
{

    public List<Regex> RegList = new()
    {
        NameRegex(),
        /*
         (?<=(\\[name=\")|(\\[multiline\\(name=\"))【以“name”或者“multiline”开头】
         .*【取其中的所有文字】
         (?=\")【以双引号结尾】
         - 用来提取角色名字，以及辨认句子类型
        */
        
        SpecialTagRegex(),
        /*
         (?<=(\[(?!name)))【以“[”开头，但不以“[name”开头】
         .*【取所有文字】
         (?=\()【以“(”结尾】
         - 标签不是name的,另外通过tags.json中的内容处理
        */
        SegmentRegex(),// [Character]、[Dialog]等无参标签就变成线
        CommentRegex(),// 不以“[”起头的，就一律当做引用。
    };

    public readonly List<SentenceMethod> RegexAndMethods = new();
    private readonly JObject tagList;

    public PlotRegs(string jsonPath)
    {
        RegexAndMethods.Add(new SentenceMethod(NameRegex(), ProcessName));
        RegexAndMethods.Add(new SentenceMethod(SegmentRegex(), MakeLine));
        RegexAndMethods.Add(new SentenceMethod(SpecialTagRegex(), ProcessTag));
        RegexAndMethods.Add(new SentenceMethod(CommentRegex(), MakeComment));
        tagList = JObject.Parse(System.IO.File.ReadAllText(jsonPath));
    }
    public PlotRegs()
    {
        RegexAndMethods.Add(new SentenceMethod(NameRegex(), ProcessName));
        RegexAndMethods.Add(new SentenceMethod(SegmentRegex(), MakeLine));
        RegexAndMethods.Add(new SentenceMethod(SpecialTagRegex(), ProcessTag));
        RegexAndMethods.Add(new SentenceMethod(CommentRegex(), MakeComment));
        tagList = JObject.Parse("[]");
    }


    private string ProcessName(string line)
    {
        var name = NameRegex().Match(line).Value;
        if (name == "？？？") name = "神秘人士";
        var nameLine = RegexToSubName().Replace(line, $"**{name}**`讲道：`");
        if (line.Contains("multiline"))
            return ProcessMultiLine(nameLine) + Environment.NewLine;
        return nameLine;
    }

    private static string ProcessMultiLine(string? newValue)
    {
        newValue = newValue!.Replace("\\n", "\n");
        newValue = newValue.Replace("\\t", "\t");
        return newValue!;
    }

    private string ProcessTag(string line)
    {
        string tag = GetTagOfLine(line);
        // process the tag
        if (!IsTagExist(tag))
        {
            NoticeNoMatchTag(line, tag);
            return line;
        }
        string newTag = GetNewTag(tag);
        if (newTag == "`立绘`") Console.WriteLine();
        if (string.IsNullOrEmpty(newTag)) return string.Empty;
        // process the value
        string newValue = GetRetainKeyword(line, tag);
        if (string.IsNullOrEmpty(newValue)) return ProcessEmptyNewValue(newTag);

        // afterprocesing the newValue
        string? mediaUrl = GetMediaUrl(newTag, newValue);
        newValue = FindTheLongestWord(newValue);
        newValue = RipDollar(newValue);
        var resultLine = newTag + newValue;
        resultLine = ProcessFlashBack(resultLine, newTag, newValue);
        resultLine = AttachToMediaUrl(resultLine, mediaUrl);
        return resultLine;
    }

    private static string RipDollar(string text)
    {
        text = Regex.Replace(text, @"\$", "");
        return text;
    }


    private string MakeLine(string line)
    {
        return "---";
    }

    private string MakeComment(string line)
    {
        return $"> {line}";
    }

}
