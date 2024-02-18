using System;
using System.Linq;
using System.Text.Json;
using ArkPlotWpf.Data;
using ArkPlotWpf.Model;
using ArkPlotWpf.Utilities.TagProcessingComponents;
using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;
using ResItem = System.Collections.Generic.KeyValuePair<string, string>;

// Define the alias
namespace ArkPlotWpf.Utilities.PrtsComponents;

public class PrtsPreloader
{
    public readonly PreloadSet Assets = new();
    public readonly string Page;
    private readonly PrtsDataProcessor prts = new();
    private int counter;
    private bool isCharacterNeedsOverride = true;
    private bool isImageNeedsOverride = true;
    private bool isTextNeedsOverride = true;
    private bool isTweenNeedsOverride = true;

    public PrtsPreloader(string pageName, IEnumerable<string> dataTxt)
    {
        // 用来将章节名称替换成prts页面地址
        Page = pageName.Trim()
            .Replace(" 行动后", "/END")
            .Replace(" 行动前", "/BEG")
            .Replace(" 幕间", "/NBT");
        //.Replace(" ", "_");
        PlotText = dataTxt.ToList();
    }

    public PrtsPreloader(PlotManager plotManager)
    {
        var pageName = plotManager.CurrentPlot.Title;
        IEnumerable<string> dataTxt = plotManager.CurrentPlot.Content.ToString().Split("\n");
        // 用来将章节名称替换成prts页面地址
        Page = pageName.Trim()
            .Replace(" 行动后", "/END")
            .Replace(" 行动前", "/BEG")
            .Replace(" 幕间", "/NBT");
        //.Replace(" ", "_");
        PlotText = dataTxt.ToList();
    }

    public List<string> PlotText { get; }

    public void ParseAndCollectAssets()
    {
        foreach (var txt in PlotText)
        {
            if (string.IsNullOrWhiteSpace(txt) || txt.TrimStart().StartsWith("//")) continue;

            OverrideCurrentText();
            var match = ArkPlotRegs.UniversalTagsRegex().Match(txt);
            if (!match.Success) continue;

            // Assigning named results based on your description
            // var matchedWhole = match.Groups[0].Value; // The entire matched string
            var matchedTag = match.Groups[1].Value;
            var matchedCommands = match.Groups[2].Value;
            // var matchedTagOnly = match.Groups[3].Value; 
            // var matchedDialog = match.Groups[4].Value; 

            if (!string.IsNullOrEmpty(matchedTag)) ProcessCommand(matchedTag.ToLower(), matchedCommands);
            counter++;
        }
    }

    private void ProcessCommand(string command, string parameters)
    {
        var commandDict = parameters.ToCommandSet();
        commandDict["type"] = command;
        switch (command)
        {
            case "background":
            case "image":
            case "showitem":
                ProcessImageCommand(commandDict);
                break;
            // Additional command processing as needed
            case "backgroundtween":
            case "imagetween":
            case "largebgtween":
            case "largeimgtween":
                GetTweensToOverride(commandDict);
                goto case "character";
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

    private void OverrideCurrentText()
    {
        if (!isTextNeedsOverride) return;
        // 尝试获取'override'属性
        var isOverrideExists =
            prts.Res.DataOverrideDocument.RootElement.TryGetProperty("override", out var overrideLineList);
        // 如果'override'属性不存在，则立即返回
        if (!isOverrideExists) return;

        // 尝试获取对应页的'override'内容
        var isOverPageImageExists =
            overrideLineList.TryGetProperty(Page, out var pageOverrideLineList) && isOverrideExists;
        // 如果对应页的'override'内容不存在，则立即返回
        if (!isOverPageImageExists)
        {
            isTextNeedsOverride = false;
            return;
        }


        // 测试当前行是否需要覆盖？
        var isCurrentTextNeedOverride =
            pageOverrideLineList.TryGetProperty((counter + 1).ToString(), out var lineOverrides) &&
            isOverPageImageExists;
        // 如果不需要覆盖，则立即返回
        if (!isCurrentTextNeedOverride) return;
        // Check if overrides exist for the current page and counter
        if (isCurrentTextNeedOverride && lineOverrides.ValueKind == JsonValueKind.Object)
            PlotText[counter] = lineOverrides.EnumerateObject().First().ToString();
    }

    private void ProcessImageCommand(StringDict commandDict)
    {
        GetImagesToOverride(commandDict);

        // Constructing the key for image lookup
        var isBg = commandDict.ContainsKey("type") &&
                   commandDict["type"].Equals("background", StringComparison.OrdinalIgnoreCase);
        var prefix = isBg ? "bg_" : "";
        var key = commandDict.ContainsKey("image") ? prefix + commandDict["image"].ToLower() : string.Empty;

        if (string.IsNullOrEmpty(key)) return; // Skip if key is not valid

        if (!prts.Res.DataImage.ContainsKey(key))
        {
            // Log or handle the error where the key does not exist
            Console.WriteLine($"<image> Linked key [{key}] not exist.");
            return;
        }

        // Adding the resolved image asset to assets
        var url = prts.Res.DataImage[key];
        Assets.Add(new ResItem(key, url));
    }

    private void ProcessPortraitCommand(StringDict commandDict)
    {
        GetCharactersToOverride(commandDict);

        var names = new List<string>();
        if (commandDict.TryGetValue("name", out var name)) names.Add(name.ToLower());
        if (commandDict["type"] == "character" && commandDict.TryGetValue("name2", out var name2))
            names.Add(name2.ToLower());

        foreach (var characterName in names)
        {
            // Placeholder for character asset key retrieval or formatting logic
            var url = prts.GetPortraitUrl(
                characterName); // Assume this method resolves the asset key based on characterName
            Assets.Add(new ResItem(characterName, url));
        }
    }

    private void ProcessLargeImageCommand(StringDict commandDict)
    {
        if (!commandDict.TryGetValue("imagegroup",
                out var imageGroupValue)) return; // No image group specified, nothing to process

        // Splitting the imageGroupValue into individual image keys
        var images = imageGroupValue.Split('/');
        foreach (var img in images)
        {
            // Constructing the key for each image
            var key = commandDict["type"].EndsWith("bg", StringComparison.OrdinalIgnoreCase)
                ? "bg_" + img.ToLower()
                : img.ToLower();

            // Checking if the key exists in the DataChar collection and adding it to assets if it does
            if (!string.IsNullOrWhiteSpace(key) && prts.Res.DataChar.ContainsKey(key))
            {
                var url = prts.Res.DataChar[key];
                Assets.Add(new ResItem(key, url));
            }
            else
            {
                // Log or handle the error where the key does not exist
                Console.WriteLine($"<{commandDict["type"]}> Linked key [{key}] not exist.");
            }
        }
    }

    private void GetTweensToOverride(StringDict commandDict)
    {
        if (!isTweenNeedsOverride) return;
        var isOverTweenExists = prts.Res.DataOverrideDocument.RootElement.TryGetProperty("tween", out var tweens);
        if (!isOverTweenExists) return;

        var isOverPageTweenExists = tweens.TryGetProperty(Page, out var pageTweens) && isOverTweenExists;
        if (!isOverPageTweenExists)
        {
            isTweenNeedsOverride = false;
            return;
        }


        var isTweenNeedOverride = pageTweens.TryGetProperty((counter + 1).ToString(), out var tweenOverrides) &&
                                  isOverPageTweenExists;
        if (!isTweenNeedOverride) return;
        if (isTweenNeedOverride && tweenOverrides.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in tweenOverrides.EnumerateObject())
            {
                var itemToOverride = property.Name;
                var originalValue = commandDict[itemToOverride];
                commandDict[itemToOverride] = property.Value.GetString() ?? originalValue;
            }

            PlotText[counter] = SerializeCommandDict(commandDict);
        }
    }

    private void ProcessSoundsCommand(StringDict commandDict)
    {
        List<string> audioKeys = new();

        // If the command is "playmusic" and an intro is specified, add it to the list
        if (commandDict["type"] == "playmusic" && commandDict.TryGetValue("intro", out var intro)) audioKeys.Add(intro);

        // Always add the main key if it exists
        if (commandDict.TryGetValue("key", out var key)) audioKeys.Add(key);

        foreach (var audioKey in audioKeys)
        {
            var audioUrl = prts.GetRealAudioUrl(audioKey);
            if (!string.IsNullOrWhiteSpace(audioUrl))
                Assets.Add(new ResItem(audioKey, audioUrl));
            else
                // Log or handle the error where the audio URL does not exist
                Console.WriteLine($"<audio> Linked key [{audioKey}] not exist.");
        }
    }

    private void GetCharactersToOverride(StringDict commandDict)
    {
        if (!isCharacterNeedsOverride) return;
        var isOverCharacterExists = prts.Res.DataOverrideDocument.RootElement.TryGetProperty("char", out var tweens);
        if (!isOverCharacterExists) return;

        var isOverPageCharacterExists = tweens.TryGetProperty(Page, out var pageCharacters) && isOverCharacterExists;
        if (!isOverPageCharacterExists)
        {
            isCharacterNeedsOverride = false;
            return;
        }

        var isCharacterNeedOverride =
            pageCharacters.TryGetProperty((counter + 1).ToString(), out var characterOverrides) &&
            isOverPageCharacterExists;
        if (!isCharacterNeedOverride) return;
        if (isCharacterNeedOverride && characterOverrides.ValueKind == JsonValueKind.Object)
        {
            var originalValue = commandDict["name"];
            var isName1Exists = characterOverrides.TryGetProperty("name", out var name1);
            commandDict["name"] = isName1Exists ? name1.ToString() : originalValue;
            if (commandDict["type"] == "character")
            {
                var originalValue2 = commandDict["name2"];
                var isName2Exists = characterOverrides.TryGetProperty("name2", out var name2);
                commandDict["name2"] = isName2Exists ? name2.ToString() : originalValue2;
            }

            PlotText[counter] = SerializeCommandDict(commandDict);
        }
    }

    private void GetImagesToOverride(StringDict commandDict)
    {
        if (!isImageNeedsOverride) return;
        // 尝试获取'image'属性
        var isOverImageExists = prts.Res.DataOverrideDocument.RootElement.TryGetProperty("image", out var images);
        // 如果'image'属性不存在，则立即返回
        if (!isOverImageExists) return;

        // 尝试获取对应页的'image'属性
        var isOverPageImageExists = images.TryGetProperty(Page, out var pageImages) && isOverImageExists;
        // 如果对应页的'image'属性不存在，则立即返回
        if (!isOverPageImageExists)
        {
            isImageNeedsOverride = false;
            return;
        }


        // 尝试获取需要覆盖的具体图片
        var isImageNeedOverride = pageImages.TryGetProperty((counter + 1).ToString(), out var imageOverrides) &&
                                  isOverPageImageExists;
        // 如果需要覆盖的图片不存在，则立即返回
        if (!isImageNeedOverride) return;
        // Check if overrides exist for the current page and counter
        if (isImageNeedOverride && imageOverrides.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in imageOverrides.EnumerateObject())
            {
                var itemToOverride = property.Name;
                var originalValue = commandDict[itemToOverride];
                // Deep copy the override property
                commandDict[itemToOverride] = property.Value.GetString() ?? originalValue;
            }

            PlotText[counter] = SerializeCommandDict(commandDict);
        }
    }

    // Additional helper methods for processing other commands
    private static string SerializeCommandDict(Dictionary<string, string> commands)
    {
        var res = new List<string>();
        foreach (var cmd in commands) res.Add($"{cmd.Key}=\"{cmd.Value}\"");
        return $"[{commands["type"]}({string.Join(", ", res)})]";
    }
}
