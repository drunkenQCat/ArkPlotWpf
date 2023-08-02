using System.Linq;
using System.Text.RegularExpressions;
using ArkPlotWpf.Utilities;

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
        newValue = newValue.Replace("$", "").Trim();

        try
        {
            switch (mediaType)
            {
                case MediaType.Image:
                    // in csv, the background is bg_bg, fuck
                    if (newTag.Contains('景')) newValue = $"bg_{newValue}";
                    url = res.DataImage[newValue];
                    url = $"<img src=\"{url}\" alt=\"{newValue}\" style=\"max-height:350px\"/>";
                    break;
                case MediaType.Portrait:
                    url = res.DataChar[newValue];
                    url = $"<img class\"portrait\" src=\"{url}\" alt=\"{newValue}\" style=\"max-height:300\"/>";
                    break;
                case MediaType.Music:
                    url = res.DataAudio[newValue];
                    url = $"<audio controls width=\"300\" alt=\"{newValue}\"><source src=\"{url}\" type=\"audio/mpeg\"></audio>";
                    if (newTag.Contains('乐'))
                    {
                        var urlParts = url.Split(" ");
                        urlParts[0] += "class=\"music\"";
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
        if (mediaUrl != null) line = $"\r\n\r\n{line}\r\n\r\n{mediaUrl}\r\n\r\n";
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
                return "<musicstop/>\r\n";
            case "`立绘`":
                return "";
        }
        return newTag;
    }
}
