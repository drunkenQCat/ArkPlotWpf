using System;
using System.Collections.Generic;
using System.Linq;
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
        // process the tag
        if (tagList[tag] == null)
        {
            NotificationBlock.Instance.OnLineNoMatch(new LineNoMatchEventArgs(line, tag));
            return line;
        }
        var newTag = (string)tagList[tag]!;
        if (newTag == "") return newTag;
        // process the value
        var newValueReg = (string)tagList[tag + "_reg"]!;
        var newValue = Regex.Match(line, newValueReg).Value;
        string? mediaUrl = GetMediaUrl(newTag, newValue);
        newValue = FindTheLongestWord(newValue);
        // process [multiline]
        if (newTag == "multiline")
        {
            newValue = newValue!.Replace("\\n", "\n");
            newValue = newValue.Replace("\\t", "\t");
        }

        line = newTag + newValue;
        if (mediaUrl != null) line += $"\r\n\r\n{mediaUrl}\r\n\r\n";
        return line + Environment.NewLine;
    }


    private string? GetMediaUrl(string newTag, string newValue)
    {
        var res = ResourceCsv.Instance;
        string? url = null;
        newValue = newValue.Replace("$", "").Trim();
        var mediaType = GetMediaType(newTag);
        if (mediaType == null) return null;

        try
        {
            switch (mediaType)
            {
                case MediaType.Image:
                    // in csv, the background is bg_bg, fuck
                    if(newTag.Contains("景")) url = $"bg_{url}";
                    url = res.DataImage[newValue];
                    url = $"<img src=\"{url}\" alt=\"{newValue}\" style=\"max-height:200px\"/>";
                    break;
                case MediaType.Portrait:
                    url = res.DataChar[newValue];
                    url = $"<img src=\"{url}\" alt=\"{newValue}\" style=\"max-height:200px\"/>";
                    break;
                case MediaType.Music:
                    url = res.DataAudio[newValue];
                    url = $"<audio alt=\"{newTag}\" src=\"{url}\" data-src=\"{url}\" controlslist=\"nodownload\" controls=\" \"preload=\"none\"></audio>";
                    break;
            }
        }
        catch
        {
            url = null;
        }

        return url;
    }

    private MediaType? GetMediaType(string newTag)
    {
        if (newTag.Contains("图") || newTag.Contains("景")) return MediaType.Image;
        if (newTag.Contains("绘")) return MediaType.Portrait;
        if (newTag.Contains("音")) return MediaType.Music;
        return null;
    }

    private static string? FindTheLongestWord(string value)
    {
        var newValue =
            (from word in value.Split("_")
             orderby word.Length descending
             select word).FirstOrDefault();
        return newValue;
    }

    private string MakeLine(string line)
    {
        return "\r\n\r\n---";
    }

    private string MakeComment(string line)
    {
        return $"> {line}\r\n";
    }

    private enum MediaType
    {
        Image,
        Portrait,
        Music
    }
}
