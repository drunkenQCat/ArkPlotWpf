using System;
using System.Text.Json;

namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public partial class TagProcessor
{
    private string GetImageUrl(string newTag, string newValueTrimed)
    {
        // in csv, the background is bg_bg, fuck
        if (newTag.Contains('景')) newValueTrimed = $"bg_{newValueTrimed}";
        var isImageExists = prts.Res.DataImage.TryGetValue(newValueTrimed, out var url);
        return isImageExists ? url! : "";
    }

    private string ConvertToImageTag(string newTag, string newValue)
    {
        var url = GetImageUrl(newTag, newValue);
        return $"<img  src=\"{url}\" alt=\"{newValue}\" loading=\"lazy\" style=\"max-height:350px\"/>";
    }

    private string ConvertToAudioTag(string newTag, string newValue)
    {
        string? url = prts.GetRealAudioUrl(newValue);
        url = $"<audio controls class=\"lazy-audio\" width=\"300\" alt=\"{newValue}\"><source src=\"{url}\" type=\"audio/mpeg\"></audio>";
        if (newTag.Contains('乐'))
        {
            var urlParts = url.Split(" ");
            urlParts[0] += " class=\"music\"";
            url = string.Join(" ", urlParts);
        }
        return url;
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

        // if finally nothing found, return Thorn's head
        newKey = newKey is null ? "char_293_thorns_1" : newKey.ToLower();
        var isPortraitExists = prts.Res.DataChar.TryGetValue(newKey, out var url);
        return isPortraitExists ? url! : "https://prts.wiki/images/d/d0/Avg_char_293_thorns_1.png";
    }
    private string ConvertToPortraitTag(string newValue)
    {
        var url = GetPortraitUrl(newValue);
        return $"<img class=\"portrait\" src=\"{url}\" alt=\"{newValue}\" loading=\"lazy\" style=\"max-height:300px\"/>";
    }
}
