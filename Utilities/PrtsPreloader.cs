using System;
using System.Text.Json;

using ArkPlotWpf.Model;
// Define the alias
using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;
using ResItem = System.Collections.Generic.KeyValuePair<string, string>;

namespace ArkPlotWpf.Utilities;

public class PrtsPreloader
{
    private PreloadSet assets = new();
    private readonly ResourceCsv resources;
    private readonly string page;
    private int counter = 0;
    private PlotRegs portraitProcessor = new PlotRegs();

    public PrtsPreloader(string pageName)
    {
        resources = ResourceCsv.Instance;
        // 用来将章节名称替换成prts页面地址
        page = pageName.Trim()
            .Replace(" 行动后", "/END")
            .Replace(" 行动前", "/BEG")
            .Replace(" 幕间", "/NBT")
            .Replace(" ", "_");
    }

    public PreloadSet ParseAndCollectAssets(IEnumerable<string> dataTxt)
    {
        foreach (var txt in dataTxt)
        {
            if (string.IsNullOrWhiteSpace(txt) || txt.TrimStart().StartsWith("//")) continue;

            var match = PlotRegs.UniversalTagsRegex().Match(txt);
            if (!match.Success) continue;

            // Assigning named results based on your description
            var matchedWhole = match.Groups[0].Value; // The entire matched string
            var matchedTag = match.Groups[1].Value; 
            var matchedCommands = match.Groups[2].Value; 
            var matchedTagOnly = match.Groups[3].Value; 
            var matchedDialog = match.Groups[4].Value; 

            if (!string.IsNullOrEmpty(matchedTag))
            {
                ProcessCommand(matchedTag.ToLower(), matchedCommands);
            }
            counter++;
        }

        return assets;
    }

    private void ProcessCommand(string command, string parameters)
    {
        var commandDict = parameters.ToObject();
        switch (command)
        {
            case "background":
            case "image":
            case "showitem":
                ProcessImageCommand(commandDict);
                break;
            // Additional command processing as needed
            case "character":
            case "charactercutin":
            case "charslot":
                ProcessPortraitCommand(commandDict);
					break;
            case "gridbg":
            case "verticalbg":
            case "largebg":
            case "largeimg":
                ProcessLargeImageCommand(commandDict);
					break;
            case "playmusic":
            case "playsound":
                ProcessSoundsCommand(commandDict);
                break;



        }
    }

    private void ProcessSoundsCommand(StringDict commandDict)
    {
        List<string> audioKeys = new();

        // If the command is "playmusic" and an intro is specified, add it to the list
        if (commandDict["type"] == "playmusic" && commandDict.TryGetValue("intro", out var intro))
        {
            audioKeys.Add(intro);
        }

        // Always add the main key if it exists
        if (commandDict.TryGetValue("key", out var key))
        {
            audioKeys.Add(key);
        }

        foreach (var audioKey in audioKeys)
        {
            // Placeholder for translating an audio identifier into a URL
            string audioUrl = ResourceCsv.GetMusicUrl(audioKey); // Assume this method resolves the URL based on audioKey
            if (!string.IsNullOrWhiteSpace(audioUrl))
            {
                assets.Add(new ResItem(audioKey, audioUrl));
            }
            else
            {
                // Log or handle the error where the audio URL does not exist
                Console.WriteLine($"<audio> Linked key [{audioKey}] not exist.");
            }
        }
    }

    private void ProcessLargeImageCommand(StringDict commandDict)
    {
        if (!commandDict.TryGetValue("imagegroup", out var imageGroupValue))
        {
            return; // No image group specified, nothing to process
        }

        // Splitting the imageGroupValue into individual image keys
        var images = imageGroupValue.Split('/');
        foreach (var img in images)
        {
            // Constructing the key for each image
            string key = commandDict["type"].EndsWith("bg", StringComparison.OrdinalIgnoreCase) ? "bg_" + img.ToLower() : img.ToLower();

            // Checking if the key exists in the DataChar collection and adding it to assets if it does
            if (!string.IsNullOrWhiteSpace(key) && resources.DataChar.ContainsKey(key))
            {
                var url = ResourceCsv.GetItemUrl(resources.DataChar[key]);
                assets.Add(new ResItem(key, url));
            }
            else
            {
                // Log or handle the error where the key does not exist
                Console.WriteLine($"<{commandDict["type"]}> Linked key [{key}] not exist.");
            }
        }
    }

    private void ProcessPortraitCommand(StringDict commandDict)
    {
        JsonElement? charOverrides = null;

        if (resources.DataOverrideDocument.RootElement.TryGetProperty("char", out var chars) &&
            chars.TryGetProperty(page, out var pageChars) &&
            pageChars.TryGetProperty((counter + 1).ToString(), out var specificChar))
        {
            charOverrides = specificChar;
        }

        if (charOverrides.HasValue && charOverrides.Value.ValueKind == JsonValueKind.Object)
        {
            if (charOverrides.Value.TryGetProperty("name", out var nameOverride))
            {
                commandDict["name"] = nameOverride.GetString() ?? commandDict["name"];
            }
            if (commandDict.ContainsKey("type") && commandDict["type"] == "character" && charOverrides.Value.TryGetProperty("name2", out var name2Override))
            {
                commandDict["name2"] = name2Override.GetString() ?? commandDict["name2"];
            }
        }

        List<string> names = new List<string>();
        if (commandDict.TryGetValue("name", out var name))
        {
            names.Add(name.ToLower());
        }
        if (commandDict.ContainsKey("type") && commandDict["type"] == "character" && commandDict.TryGetValue("name2", out var name2))
        {
            names.Add(name2.ToLower());
        }

        foreach (var characterName in names)
        {
            // Placeholder for character asset key retrieval or formatting logic
            string key = portraitProcessor.GetPortraitUrl(characterName); // Assume this method resolves the asset key based on characterName

            if (!resources.DataChar.ContainsKey(key))
            {
                // Log error - character asset not found
                Console.WriteLine($"<character> Linked key [{key}] not exist.");
                continue;
            }

            var url = ResourceCsv.GetItemUrl(resources.DataChar[key]);
            assets.Add(new ResItem(key, url));
        }
    }



    private void ProcessImageCommand(StringDict commandDict)
    {
        JsonElement imageOverrides;

        // Check if overrides exist for the current page and counter
        if (resources.DataOverrideDocument.RootElement.TryGetProperty("image", out var images) &&
            images.TryGetProperty(page, out var pageImages) &&
            pageImages.TryGetProperty((counter + 1).ToString(), out imageOverrides) &&
            imageOverrides.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in imageOverrides.EnumerateObject())
            {
                commandDict[property.Name] = property.Value.GetString();
            }
        }

        // Constructing the key for image lookup
        string prefix = commandDict.ContainsKey("type") && commandDict["type"].Equals("background", StringComparison.OrdinalIgnoreCase) ? "bg_" : "";
        string key = commandDict.ContainsKey("image") ? prefix + commandDict["image"].ToLower() : string.Empty;

        if (string.IsNullOrEmpty(key))
        {
            return; // Skip if key is not valid
        }

        if (!resources.DataChar.ContainsKey(key))
        {
            // Log or handle the error where the key does not exist
            Console.WriteLine($"<character> Linked key [{key}] not exist.");
            return;
        }

        // Adding the resolved image asset to assets
        var url = ResourceCsv.GetItemUrl(resources.DataImage[key]);
        assets.Add(new ResItem(key, url));
    }


    // Additional helper methods for processing other commands
}

