using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ArkPlotWpf.Model;

public partial class PlotRegs
{
    
    public List<Regex> RegList = new()
    {
        NameRegex(),
        TagSentenceRegex(),// 标签不是name的另外处理
        LineSentenceRegex(),// [Charater]、[Dialog]等无参标签就变成线
        ChineseSentenceRegex(),// 以“[”起头的，就一律当做引用。另，不一定是汉字
    };

    public List<SentenceMethod> Processors = new();
    public List<Func<string, string>> MethodList = new();
    private readonly JObject tagList;

    public PlotRegs(string jsonPath)
    {
        Processors.Add(new SentenceMethod(NameRegex(), Namlize));
        Processors.Add(new SentenceMethod(TagSentenceRegex(), Taglize));
        Processors.Add(new SentenceMethod(LineSentenceRegex(), Linize));
        Processors.Add(new SentenceMethod(ChineseSentenceRegex(), Chinize));
        MethodList.Add(Namlize);
        MethodList.Add(Taglize);
        MethodList.Add(Linize);
        MethodList.Add(Chinize);
        tagList = JObject.Parse(System.IO.File.ReadAllText(jsonPath));
    }

    [GeneratedRegex("(?<=(\\[name=\")|(\\[multiline\\(name=\")).*(?=\")", RegexOptions.Compiled)]
    private static partial Regex NameRegex();
    [GeneratedRegex("\\[name=\".*\"\\]", RegexOptions.Compiled)]
    private static partial Regex NameSentenceRegex();
    [GeneratedRegex("(?<=\\[)[A-Za-z]*(?=\\])", RegexOptions.Compiled)]
    private static partial Regex LineSentenceRegex();
    [GeneratedRegex("^[^\\[].*$", RegexOptions.Compiled)]
    private static partial Regex ChineseSentenceRegex();
    [GeneratedRegex("(?<=(\\[(?!name))).*(?=\\()", RegexOptions.Compiled)]
    private static partial Regex TagSentenceRegex();
    
    private string Namlize(string line)
    {
        var name = NameRegex().Match(line).Value;
        var nameLine = NameSentenceRegex().Replace(line, $"**{name}**`讲道：`");
        return nameLine + Environment.NewLine;
    }

    private string Taglize(string line)
    {
        var tag = TagSentenceRegex().Match(line).Value;
        tag = tag.ToLower();
        var tagNew = (string)tagList[tag]!;
        line = line.Replace("_", " ");
        if (tagNew == "") return tagNew;
        try
        {
            var tagReg = (string)tagList[tag+"_reg"]!;
            var newLine = tagNew + Regex.Match(line, tagReg).Value;
            return newLine + Environment.NewLine;
        }
        catch (System.Exception)
        {
            Console.WriteLine($"出错的句子\n{line}");
            throw ;
        }
    }

    private string Linize(string line)
    {
        return "\r\n\r\n---";
    }

    private string Chinize(string line)
    {
        return $"> {line}\r\n";
    }
}