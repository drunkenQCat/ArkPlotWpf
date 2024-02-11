using ArkPlotWpf.Utilities;
using System;
using System.Linq;
using System.Text.Json;
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
                    url = GetPortraitUrl(newValue);
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

    public (string, int) FindPortraitInLinkData(string keyData)
    {
        if (string.IsNullOrWhiteSpace(keyData))
        {
            Console.WriteLine("The input parameter is empty, has skipped the data.");
            return ("-1", -1);
        }

        var matchedCodeParts = CharPortraitCodeRegex().Match(keyData);
        if (!matchedCodeParts.Success)
        {
            Console.WriteLine("Can't get key from the input parameter, has skipped the data.");
            return ("-1", -1);
        }

        int? GetSubIndex(int index) => matchedCodeParts!.Groups[index].Success ? int.Parse(matchedCodeParts.Groups[index].Value) : null;
 

        string portraitNameGroup = matchedCodeParts.Groups[1].Value!;
        var emotionIndex = GetSubIndex(3);

        if (!res.PortraitLinkDocument.RootElement.TryGetProperty(portraitNameGroup, out JsonElement linkItem))
        {
            Console.WriteLine($"The appointed key [{portraitNameGroup}] not exist, has skipped the data.");
            return ("-1", -1);
        }

        var groupIndex = GetSubIndex(4);
        var groupSubIndex = GetSubIndex(5);
        if (groupIndex is not null && groupSubIndex is not null) return ProcessDollarSymbol();

        if (matchedCodeParts.Groups[2].Success)
        {
            var symbol = matchedCodeParts.Groups[2].Value;

            switch (symbol)
            {
                case "@":
                    for (int idx = 0; idx < linkItem.GetProperty("array").GetArrayLength(); idx++)
                    {
                        var currentElement = linkItem.GetProperty("array")[idx];
                        if (currentElement.GetProperty("alias").GetString() == emotionIndex.ToString())
                        {
                            return (portraitNameGroup, idx);
                        }
                    }
                    Console.WriteLine("Data analyze error, use the default char to instead.");
                    return (portraitNameGroup, 0);
                case "$":
                    return ProcessDollarSymbol();
                case "#":
                    int outputIndex = emotionIndex ?? 0;
                    if(outputIndex >= linkItem.GetProperty("array").GetArrayLength())
                    {
                        Console.WriteLine($"The analyze key [{portraitNameGroup} : {outputIndex}] is out of range, use the default char to instead");
                        outputIndex = 0;
                    }
                    return (portraitNameGroup, outputIndex); // Adjusting because array index is zero-based
                default:
                    break;
            }
        }
        (string portraitNameGroup, int) ProcessDollarSymbol()
        {
            string subIndex = "$" + (groupSubIndex  ?? emotionIndex); // 组合group
            emotionIndex = groupIndex != null ? groupIndex : emotionIndex;
            var arrayElements = linkItem.GetProperty("array")
                .EnumerateArray()
                .Select((element, index) => new { Name = element.GetProperty("name").GetString(), Index = index })
                .ToList();

            if(arrayElements is null) return ("-1", -1);
            var matchingElements = arrayElements.Where(element => element.Name!.EndsWith(subIndex)).ToList();
            if (!matchingElements.Any())
            {
                Console.WriteLine($"No elements ending with {subIndex}.");
                return (portraitNameGroup, 0); // Using default index if no matching elements
            }
            emotionIndex = Math.Min(emotionIndex??0, matchingElements.Count - 1);
            var targetElement = matchingElements.ElementAt(emotionIndex??0);

            // Return original name and adjusted index within the global array
            return (portraitNameGroup, arrayElements.IndexOf(targetElement));
        }
        // Default fallback if no special symbol is used
        return (portraitNameGroup, Math.Max(emotionIndex ?? 1 - 1, 0)); // Adjusting because array index is zero-based
    }

    string GetPortraitUrl(string inputKey)
    {
        (string key, int index) = FindPortraitInLinkData(inputKey);
        if (!res.PortraitLinkDocument.RootElement.TryGetProperty(key, out JsonElement linkItem))
        {
            Console.WriteLine($"Character key [\"{key}\"] not exist, please check the link list");
            return key;
        }
        var newKey =  linkItem.GetProperty("array")[index]
            .GetProperty("name")
            .GetString();
        return res.DataChar[newKey?? "char_293_thorns_1"];
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
