using ArkPlotWpf.Utilities;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArkPlotWpf.Model;

public partial class PlotRegs
{
    private readonly ResourceCsv res = ResourceCsv.Instance;
    private string GetRetainKeyword(string line, string tag)
    {
        var newValueReg = (string)tagList[tag + "_reg"]!;
        var newValue = Regex.Match(line, newValueReg).Value;
        return newValue;
    }

    private string GetNewTag(string tag) => (string)tagList[tag]!;
    private static string GetTagOfLine(string line)
    {
        var tag = SpecialTagRegex().Match(line).Value;
        tag = tag.ToLower();
        return tag;
    }
    private string? GetMediaUrl(string newTag, string newValue)
    {
        var mediaType = GetMediaType(newTag);
        if (mediaType == null) return null;

        string? url = null;
        newValue = newValue.Trim();
        if (newValue[0] == '$') newValue = newValue.Remove(0, 1);
        try
        {
            switch (mediaType)
            {
                case MediaType.Image:
                    // in csv, the background is bg_bg, fuck
                    if (newTag.Contains('景')) newValue = $"bg_{newValue}";
                    url = res.DataImage[newValue];
                    url = $"<img src=\"{url}\" alt=\"{newValue}\" loading=\"lazy\" style=\"max-height:350px\"/>";
                    break;
                case MediaType.Portrait:
                    if (res.DataChar.ContainsKey(newValue)) url = res.DataChar[newValue];
                    else
                    {
                        // Handle the case where newValue does not exist in the keys
                        var charName = FindCharNum(newValue);
                        var keyword = res.DataChar.Keys.FirstOrDefault(key => key.Contains(charName!));
                        url = keyword is null ? "https://prts.wiki/images/ak.png?8efd0" : res.DataChar[keyword];
                    }
                    url = $"<img class=\"portrait\" src=\"{url}\" alt=\"{newValue}\" loading=\"lazy\" style=\"max-height:300px\"/>";
                    break;
                case MediaType.Music:
                    url = res.DataAudio[newValue];
                    url = $"<audio controls class=\"lazy-audio\" width=\"300\" alt=\"{newValue}\"><source src=\"{url}\" type=\"audio/mpeg\"></audio>";
                    if (newTag.Contains('乐'))
                    {
                        var urlParts = url.Split(" ");
                        urlParts[0] += " class=\"music\"";
                        url = string.Join(" ", urlParts);
                    }
                    break;
            }
        }
        catch
        {
            url = null;
        }

        return url;
    }

    private string? FindCharNum(string newValue)
    {
        var splitedParts = newValue.Split('_');

        return (from part in splitedParts
                where Regex.Match(part, @"\d+").Success
                select part).FirstOrDefault();

    }

    private MediaType? GetMediaType(string newTag)
    {
        if (newTag.Contains('图') || newTag.Contains('景')) return MediaType.Image;
        if (newTag.Contains('绘')) return MediaType.Portrait;
        if (newTag.Contains('音')) return MediaType.Music;
        return null;
    }

    private bool IsTagExist(string tag) => tagList[tag] != null;


    private void NoticeNoMatchTag(string line, string tag) =>
        NotificationBlock.Instance.OnNoMatchTag(new LineNoMatchEventArgs(line, tag));


    private static string AttachToMediaUrl(string line, string? mediaUrl)
    {
        if (mediaUrl != null) line = $"\n{mediaUrl}\n{line}";
        return line;
    }

    private static string ProcessFlashBack(string line, string newTag, string? newValue)
    {
        if (newTag == "`滤镜效果`" && newValue == "Grayscale") line = "`回忆画面`";
        return line;
    }

    private static string FindTheLongestWord(string value)
    {
        var newValue =
            (from word in value.Split("_")
             orderby word.Length descending
             select word).FirstOrDefault();
        if (newValue == "path") Console.WriteLine();
        return newValue!;
    }

    private enum MediaType
    {
        Image,
        Portrait,
        Music
    }

    private string ProcessEmptyNewValue(string newTag)
    {
        switch (newTag)
        {
            case "`音乐停止`":
                return "<musicstop></musicstop>\r\n";
            case "`立绘`":
                return "";
            case "`图像`":
                return "";
        }
        return newTag;
    }
}
