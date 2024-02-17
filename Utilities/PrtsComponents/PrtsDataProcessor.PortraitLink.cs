using System;
using System.Linq;
using System.Text.RegularExpressions;
using ArkPlotWpf.Data;

namespace ArkPlotWpf.Utilities.PrtsComponents;

/// <summary>
///     Partial class for processing portrait data in the PrtsDataProcessor.
/// </summary>
public partial class PrtsDataProcessor
{
    /// <summary>
    ///     Retrieves the URL of a portrait based on the input key.
    /// </summary>
    /// <param name="inputKey">The key used to search for the portrait.</param>
    /// <returns>The URL of the portrait if found, otherwise a default URL.</returns>
    public string GetPortraitUrl(string inputKey)
    {
        (var key, var index) = FindPortraitInLinkData(inputKey);
        if (!Res.PortraitLinkDocument.RootElement.TryGetProperty(key, out var linkItem))
        {
            Console.WriteLine($"Character key [\"{key}\"] not exist, please check the link list");
            return Res.DataChar["char_293_thorns_1"];
        }

        var newKey = linkItem.GetProperty("array")[index]
            .GetProperty("name")
            .GetString();
        if (newKey is null)
            // Log error - character asset not found
            Console.WriteLine($"<character> Linked key [{key}] not exist.");

        // if finally nothing found, return Thorn's head
        newKey = newKey is null ? "char_293_thorns_1" : newKey.ToLower();
        var isPortraitExists = Res.DataChar.TryGetValue(newKey, out var url);
        return isPortraitExists ? url! : "https://wiki/images/d/d0/Avg_char_293_thorns_1.png";
    }

    /// <summary>
    ///     Finds the portrait in the link data based on the provided key data.
    /// </summary>
    /// <param name="keyData">The key data used to find the portrait.</param>
    /// <returns>
    ///     A tuple containing the found portrait code and its index, or ("-1", -1) if the key data is empty or no key is
    ///     found.
    /// </returns>
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

        return ProcessMatchedCodeParts(matchedCodeParts);
    }

    private (string, int) ProcessMatchedCodeParts(Match matchedCodeParts)
    {
        var portraitNameGroup = matchedCodeParts.Groups[1].Value;
        var emotionIndex = GetSubIndex(3);

        if (!Res.PortraitLinkDocument.RootElement.TryGetProperty(portraitNameGroup, out var linkItem))
        {
            Console.WriteLine($"The appointed key [{portraitNameGroup}] not exist, has skipped the data.");
            return ("-1", -1);
        }

        var groupIndex = GetSubIndex(4);
        var groupSubIndex = GetSubIndex(5);
        if (groupIndex is not null && groupSubIndex is not null) return ProcessDollarSymbol();

        if (!matchedCodeParts.Groups[2].Success) return (portraitNameGroup, Math.Max(emotionIndex ?? 1 - 1, 0));
        var symbol = matchedCodeParts.Groups[2].Value;

        switch (symbol)
        {
            case "@":
                return ProcessAtSymbol();
            case "$":
                return ProcessDollarSymbol();
            case "#":
                var outputIndex = ProcessHashSymbol();
                return (portraitNameGroup, outputIndex);
            default:
                return (portraitNameGroup,
                    Math.Max(emotionIndex ?? 1 - 1, 0)); // Adjusting because array index is zero-based
        }

        /*
         * different conditions to process
         */
        (string portraitNameGroup, int) ProcessDollarSymbol()
        {
            var subIndex = "$" + (groupSubIndex ?? emotionIndex); // 组合group
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

        (string, int) ProcessAtSymbol()
        {
            for (var idx = 0; idx < linkItem.GetProperty("array").GetArrayLength(); idx++)
            {
                var currentElement = linkItem.GetProperty("array")[idx];
                if (currentElement.GetProperty("alias").GetString() == emotionIndex.ToString())
                    return (portraitNameGroup, idx);
            }

            Console.WriteLine("Data analyze error, use the default char to instead.");
            return (portraitNameGroup, 0);
        }

        int ProcessHashSymbol()
        {
            var outputIndex = emotionIndex ?? 0;
            if (outputIndex >= linkItem.GetProperty("array").GetArrayLength())
            {
                Console.WriteLine(
                    $"The analyze key [{portraitNameGroup} : {outputIndex}] is out of range, use the default char to instead");
                outputIndex = 0;
            }

            return outputIndex;
        }

        /*
         * A utility method 
         */
        int? GetSubIndex(int index)
        {
            return matchedCodeParts.Groups[index].Success
                ? int.Parse(matchedCodeParts.Groups[index].Value)
                : null;
        }
    }
}