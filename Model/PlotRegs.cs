using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ArkPlotWpf.Utilities;
using Newtonsoft.Json.Linq;

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
        RegexAndMethods.Add(new SentenceMethod(SpecialTagRegex(), ProcessTag));
        RegexAndMethods.Add(new SentenceMethod(SegmentRegex(), MakeLine));
        RegexAndMethods.Add(new SentenceMethod(CommentRegex(), MakeComment));
        tagList = JObject.Parse(System.IO.File.ReadAllText(jsonPath));
    }

    [GeneratedRegex("(?<=(\\[name=\")|(\\[multiline\\(name=\")).*(?=\")", RegexOptions.Compiled)]
    private static partial Regex NameRegex();
    
    [GeneratedRegex("\\[name.*\\]|\\[multiline.*\\]", RegexOptions.Compiled)]
    private static partial Regex RegexToSubName();
    
    [GeneratedRegex("(?<=\\[)[A-Za-z]*(?=\\])", RegexOptions.Compiled)]
    
    private static partial Regex SegmentRegex();
    
    [GeneratedRegex("^[^\\[].*$", RegexOptions.Compiled)]
    private static partial Regex CommentRegex();
    
    [GeneratedRegex("(?<=(\\[(?!name))).*(?=\\()", RegexOptions.Compiled)]
    private static partial Regex SpecialTagRegex();
    
    private string ProcessName(string line)
    {
        var name = NameRegex().Match(line).Value;
        var nameLine = RegexToSubName().Replace(line, $"**{name}**`讲道：`");
        return nameLine + Environment.NewLine;
    }

    private string ProcessTag(string line)
    {
        var tag = SpecialTagRegex().Match(line).Value;
        tag = tag.ToLower();
        if (tagList[tag] == null)
        {
            NotificationBlock.Instance.OnLineNoMatch(new LineNoMatchEventArgs(line, tag));
            return line;
        }
        var newTag = (string)tagList[tag]!;
        if (newTag == "") return newTag;
        line = line.Replace("_", " ");
        var tagReg = (string)tagList[tag+"_reg"]!;
        line = newTag + Regex.Match(line, tagReg).Value;
        return line + Environment.NewLine;
    }

    private string MakeLine(string line)
    {
        return "\r\n\r\n---";
    }

    private string MakeComment(string line)
    {
        return $"> {line}\r\n";
    }
}