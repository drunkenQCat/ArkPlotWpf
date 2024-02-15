using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArkPlotWpf.Data;
using ArkPlotWpf.Model;

namespace ArkPlotWpf.Utilities;

public class TagProcessor
{
    public readonly PlotRegs Regs = PlotRegs.Instance;
    private readonly PrtsDataProcessor prts = new();

    public TagProcessor()
    {
        Regs.RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.SpecialTagRegex(), ProcessTag));
    }

    private string ProcessTag(string line)
    {
        //
        // Extract the tag from the line
        string tag = ExtractTag(line);
        // Check if the tag exists; if not, log a notice and return the original line
        if (!IsValidTag(tag)) return NotifyInvalidTag(line, tag);

        // Attempt to replace the tag with a new one
        string newTag = SubStituteTag(tag);
        if (string.IsNullOrEmpty(newTag)) return string.Empty; // Early exit if no new tag

        // Extract and process the value associated with the tag
        string newValue = ExtractValue(line, tag);
        if (string.IsNullOrEmpty(newValue)) return HandleEmptyValue(newTag); // Handle empty values separately

        return ConstructResult(newTag, newValue);
    }

    private static string ExtractTag(string line)
    {
        var tag = ArkPlotRegs.SpecialTagRegex().Match(line).Value;
        tag = tag.ToLower();
        return tag;
    }

    private bool IsValidTag(string tag) => Regs.TagList[tag] != null;

    private string NotifyInvalidTag(string line, string tag)
    {
        NotificationBlock.Instance.OnNoMatchTag(new LineNoMatchEventArgs(line, tag));
        return line;
    }

    private string SubStituteTag(string tag) => (string)Regs.TagList[tag]!;

    private string ExtractValue(string line, string tag)
    {
        var newValueReg = (string)Regs.TagList[tag + "_reg"]!;
        var newValue = Regex.Match(line, newValueReg).Value;
        return newValue;
    }

    private string HandleEmptyValue(string newTag)
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

    private string ConstructResult(string newTag, string newValue)
    {
        // Additional processing on the new value
        string? mediaUrl = GetMediaUrl(newTag, newValue); // Get any associated media URL
        newValue = FindTheLongestWord(newValue); // Simplify the value to its longest word
        newValue = PlotRegsBasicHelper.RipDollar(newValue); // Remove any dollar signs

        // Construct the result line from the processed tag and value
        var resultLine = newTag + newValue;
        resultLine = ProcessFlashBack(resultLine, newTag, newValue); // Process any flashbacks
        resultLine = AttachToMediaUrl(resultLine, mediaUrl); // Attach media URL if present

        return resultLine; // Return the processed line
    }


    private string? GetMediaUrl(string newTag, string newValue)
    {
        var mediaType = GetMediaType(newTag);
        if (mediaType == null) return null;

        string? url = null;
        var newValueTrimed = newValue.Trim().ToLower();
        try
        {
            switch (mediaType)
            {
                case MediaType.Image:
                    // in csv, the background is bg_bg, fuck
                    if (newTag.Contains('景')) newValueTrimed = $"bg_{newValueTrimed}";
                    url = prts.Res.DataImage[newValueTrimed];
                    url = $"<img src=\"{url}\" alt=\"{newValueTrimed}\" loading=\"lazy\" style=\"max-height:350px\"/>";
                    break;
                case MediaType.Portrait:
                    url = GetPortraitUrl(newValueTrimed);
                    url = $"<img class=\"portrait\" src=\"{url}\" alt=\"{newValueTrimed}\" loading=\"lazy\" style=\"max-height:300px\"/>";
                    break;
                case MediaType.Music:
                    url = prts.GetRealAudioUrl(newValue);
                    url = $"<audio controls class=\"lazy-audio\" width=\"300\" alt=\"{newValueTrimed}\"><source src=\"{url}\" type=\"audio/mpeg\"></audio>";
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

    private (string, int) FindPortraitInLinkData(string keyData)
    {
        if (string.IsNullOrWhiteSpace(keyData))
        {
            Console.WriteLine("The input parameter is empty, has skipped the data.");
            return ("-1", -1);
        }

        var matchedCodeParts = ArkPlotRegs.CharPortraitCodeRegex().Match(keyData);
        if (!matchedCodeParts.Success)
        {
            Console.WriteLine("Can't get key from the input parameter, has skipped the data.");
            return ("-1", -1);
        }

        int? GetSubIndex(int index) => matchedCodeParts.Groups[index].Success ? int.Parse(matchedCodeParts.Groups[index].Value) : null;


        string portraitNameGroup = matchedCodeParts.Groups[1].Value;
        var emotionIndex = GetSubIndex(3);

        if (!prts.Res.PortraitLinkDocument.RootElement.TryGetProperty(portraitNameGroup, out JsonElement linkItem))
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
                    if (outputIndex >= linkItem.GetProperty("array").GetArrayLength())
                    {
                        Console.WriteLine($"The analyze key [{portraitNameGroup} : {outputIndex}] is out of range, use the default char to instead");
                        outputIndex = 0;
                    }
                    return (portraitNameGroup, outputIndex); // Adjusting because array index is zero-based
            }
        }
        (string portraitNameGroup, int) ProcessDollarSymbol()
        {
            string subIndex = "$" + (groupSubIndex ?? emotionIndex); // 组合group
            emotionIndex = groupIndex ?? emotionIndex;
            var arrayElements = linkItem.GetProperty("array")
                .EnumerateArray()
                .Select((element, index) => new { Name = element.GetProperty("name").GetString(), Index = index })
                .ToList();

            var matchingElements = arrayElements.Where(element => element.Name!.EndsWith(subIndex)).ToList();
            if (!matchingElements.Any())
            {
                Console.WriteLine($"No elements ending with {subIndex}.");
                return (portraitNameGroup, 0); // Using default index if no matching elements
            }
            emotionIndex = Math.Min(emotionIndex ?? 0, matchingElements.Count - 1);
            var targetElement = matchingElements.ElementAt((int)emotionIndex);

            // Return original name and adjusted index within the global array
            return (portraitNameGroup, arrayElements.IndexOf(targetElement));
        }
        // Default fallback if no special symbol is used
        return (portraitNameGroup, Math.Max(emotionIndex ?? 1 - 1, 0)); // Adjusting because array index is zero-based
    }

    public string GetPortraitUrl(string inputKey)
    {
        (string key, int index) = FindPortraitInLinkData(inputKey);
        if (!prts.Res.PortraitLinkDocument.RootElement.TryGetProperty(key, out JsonElement linkItem))
        {
            Console.WriteLine($"Character key [\"{key}\"] not exist, please check the link list");
            return prts.Res.DataChar["char_293_thorns_1"];
        }
        var newKey = linkItem.GetProperty("array")[index]
            .GetProperty("name")
            .GetString();
        if (newKey is null)
        {
            // Log error - character asset not found
            Console.WriteLine($"<character> Linked key [{key}] not exist.");
        }
        return prts.Res.DataChar[newKey is null ? "char_293_thorns_1" : newKey.ToLower()];
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

}
