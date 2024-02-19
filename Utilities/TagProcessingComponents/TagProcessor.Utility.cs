using System;
using System.Linq;
using ArkPlotWpf.Model;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public partial class TagProcessor
{
    private string? ConvertValueToHtmlTag(string newTag, string newValue)
    {
        var mediaType = GetMediaType(newTag);
        if (mediaType == null) return null;

        var htmlTaggedLine = "";
        newValue = newValue.Trim().ToLower();
        htmlTaggedLine = mediaType switch
        {
            MediaType.Image => ConvertToImageTag(newTag, newValue),
            MediaType.Portrait => ConvertToPortraitTag(newValue),
            MediaType.Music => ConvertToAudioTag(newTag, newValue),
            _ => htmlTaggedLine
        };
        return htmlTaggedLine;
    }


    private MediaType? GetMediaType(string newTag)
    {
        if (newTag.Contains('图') || newTag.Contains('景')) return MediaType.Image;
        if (newTag.Contains('绘')) return MediaType.Portrait;
        if (newTag.Contains('音')) return MediaType.Music;
        return null;
    }

    private static string AttachToMediaUrl(string line, string? mediaUrl)
    {
        if (mediaUrl != null) line = $"{mediaUrl}\r\n\r\n{line}";
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
}