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
    private readonly string _page;
    private readonly PrtsDataProcessor _prts = new();
    private int _counter;
    private bool _isCharacterNeedsOverride = true;
    private bool _isImageNeedsOverride = true;
    private bool _isTextNeedsOverride = true;
    private bool _isTweenNeedsOverride = true;
    private readonly List<FormattedTextEntry> _textList;

    private PortraitInfo _currentPortraits = new(new List<string>(){"https://pics/transparent.png"}, 0);
    private string _currentBg = "https://media.prts.wiki/8/8a/Avg_bg_bg_black.png";

    public PrtsPreloader(PlotManager plotManager)
    {
        var pageName = plotManager.CurrentPlot.Title;
        plotManager.CurrentPlot.Content.ToString().Split("\n");
        // 用来将章节名称替换成prts页面地址
        _page = pageName.Trim()
            .Replace(" 行动后", "/END")
            .Replace(" 行动前", "/BEG")
            .Replace(" 幕间", "/NBT");
        //.Replace(" ", "_");
        _textList = plotManager.CurrentPlot.TextVariants;
    }

    public void ParseAndCollectAssets()
    {
        foreach (var entry in _textList)
        {
            if (string.IsNullOrWhiteSpace(entry.OriginalText) || entry.OriginalText.TrimStart().StartsWith("//")) continue;

            OverrideCurrentText();
            var match = ArkPlotRegs.UniversalTagsRegex().Match(entry.OriginalText);
            if (!match.Success) continue;

            // var matchedWhole = match.Groups[0].Value; // The entire matched string
            var matchedTag = match.Groups[1].Value;
            var matchedCommands = match.Groups[2].Value;
            var matchedTagOnly = match.Groups[3].Value;
            entry.IsTagOnly = !string.IsNullOrEmpty(matchedTagOnly);
            var matchedCharName = match.Groups[4].Value;
            if (!string.IsNullOrEmpty(matchedCharName) && matchedCharName.StartsWith("name="))
            {
                entry.CharacterName = matchedCharName.Split('"')[1];
            }
            var matchedDialog = match.Groups[5].Value;
            entry.Dialog = matchedDialog;

            if (!string.IsNullOrEmpty(matchedTag))
            {
                entry.CommandSet = ProcessCommand(matchedTag.ToLower(), matchedCommands, out List<string> urlList);
                entry.Type = entry.CommandSet["type"];
                entry.ResourceUrls = urlList;
            }
            entry.PortraitsInfo = _currentPortraits;
            entry.Bg = _currentBg;
            _counter++;
        }
    }

    private StringDict ProcessCommand(string command, string parameters, out List<string> urls)
    {
        var commandDict = parameters.ToCommandSet();
        commandDict["type"] = command;
        urls = new();
        switch (command)
        {
            case "background":
            case "image":
            case "showitem":
                urls =ProcessImageCommand(commandDict);
                _currentBg = urls[0];
                break;
            // Additional command processing as needed
            case "backgroundtween":
            case "imagetween":
            case "largebgtween":
            case "largeimgtween":
                GetTweensToOverride(commandDict);
                goto case "character";
            case "character":
                urls = ProcessPortraitCommand(commandDict);
                _currentPortraits = GetCurrentPortraitsFromCharacter(commandDict, urls);
                break;
            case "charactercutin":
                urls = ProcessPortraitCommand(commandDict);
                _currentPortraits = GetCurrentPortraitsFromCutin(commandDict, urls);
                break;
            case "charslot":
                urls = ProcessPortraitCommand(commandDict);
                _currentPortraits = GetCurrentPortraitsFromSlot(commandDict, urls);
                break;
            case "gridbg":
            case "verticalbg":
            case "largebg":
            case "largeimg":
                urls = ProcessLargeImageCommand(commandDict);
                _currentBg = urls[0];
                break;
            case "playmusic":
            case "playsound":
                urls = ProcessSoundsCommand(commandDict);
                break;
        }
        return commandDict;
    }

    private PortraitInfo GetCurrentPortraitsFromCutin(StringDict commandDict, List<string> urls)
    {
        return new PortraitInfo(urls, 0);
    }

    private PortraitInfo GetCurrentPortraitsFromCharacter(StringDict commandDict, List<string> inputUrls)
    {
        List<string> urls = new(inputUrls);
        if(urls.Count == 0) return new PortraitInfo(new List<string>(){"https://pics/transparent.png"}, 0);
        if (commandDict.TryGetValue("focus", out string? position))
        {
            switch (position)
            {
                case "-1":
                    return new PortraitInfo(urls, -1);
                case "1":
                    return new PortraitInfo(urls, 1);
                case "2":
                    return new PortraitInfo(urls, 2);
                default:
                    return new PortraitInfo(urls, 0);
            }
        }
        return new PortraitInfo(urls, 0);
    }

    private PortraitInfo GetCurrentPortraitsFromSlot(StringDict commandDict, List<string> urls)
    {
        if (commandDict.TryGetValue("slot", out string? position))
        {
            switch (position)
            {
                case "m":
                    return new PortraitInfo(urls, 0);
                case "l":
                    return new PortraitInfo(urls, 1);
                case "r":
                    return new PortraitInfo(urls, 2);
                default:
                    return new PortraitInfo(urls, 0);
            }
        }
        return new PortraitInfo(urls, 0);
    }

    private void OverrideCurrentText()
    {
        if (!_isTextNeedsOverride) return;
        // 尝试获取'override'属性
        var isOverrideExists =
            _prts.Res.DataOverrideDocument.RootElement.TryGetProperty("override", out var overrideLineList);
        // 如果'override'属性不存在，则立即返回
        if (!isOverrideExists) return;

        // 尝试获取对应页的'override'内容
        var isOverPageImageExists =
            overrideLineList.TryGetProperty(_page, out var pageOverrideLineList) && isOverrideExists;
        // 如果对应页的'override'内容不存在，则立即返回
        if (!isOverPageImageExists)
        {
            _isTextNeedsOverride = false;
            return;
        }


        // 测试当前行是否需要覆盖？
        var isCurrentTextNeedOverride =
            pageOverrideLineList.TryGetProperty((_counter + 1).ToString(), out var lineOverrides) &&
            isOverPageImageExists;
        // 如果不需要覆盖，则立即返回
        if (!isCurrentTextNeedOverride) return;
        // Check if overrides exist for the current page and counter
        if (isCurrentTextNeedOverride && lineOverrides.ValueKind == JsonValueKind.Object)
            _textList[_counter].OriginalText = lineOverrides.EnumerateObject().First().ToString();
    }

    private List<string> ProcessImageCommand(StringDict commandDict)
    {
        GetImagesToOverride(commandDict);

        // Constructing the key for image lookup
        var isBg = commandDict.ContainsKey("type") &&
                   commandDict["type"].Equals("background", StringComparison.OrdinalIgnoreCase);
        var prefix = isBg ? "bg_" : "";
        var key = commandDict.ContainsKey("image") ? prefix + commandDict["image"].ToLower() : string.Empty;

        if (string.IsNullOrEmpty(key)) return new List<string>(); // Skip if key is not valid

        if (!_prts.Res.DataImage.ContainsKey(key))
        {
            // Log or handle the error where the key does not exist
            Console.WriteLine($"<image> Linked key [{key}] not exist.");
            return new List<string>();
        }

        // Adding the resolved image asset to assets
        var url = _prts.Res.DataImage[key];
        Assets.Add(new ResItem(key, url));
        return new List<string>{url};
    }

    private List<string> ProcessPortraitCommand(StringDict commandDict)
    {
        GetCharactersToOverride(commandDict);

        var names = new List<string>();
        if (commandDict.TryGetValue("name", out var name)) names.Add(name.ToLower());
        if (commandDict["type"] == "character" && commandDict.TryGetValue("name2", out var name2))
            names.Add(name2.ToLower());

        List<string> urls = new();
        foreach (var characterName in names)
        {
            // Placeholder for character asset key retrieval or formatting logic
            var url = _prts.GetPortraitUrl(
                characterName); // Assume this method resolves the asset key based on characterName
            urls.Add(url);
            Assets.Add(new ResItem(characterName, url));
        }

        return urls;
    }

    private List<string> ProcessLargeImageCommand(StringDict commandDict)
    {
        List<string> urls = new();
        if (!commandDict.TryGetValue("imagegroup",
                out var imageGroupValue)) return urls; // No image group specified, nothing to process

        // Splitting the imageGroupValue into individual image keys
        var images = imageGroupValue.Split('/');
        foreach (var img in images)
        {
            // Constructing the key for each image
            var key = commandDict["type"].EndsWith("bg", StringComparison.OrdinalIgnoreCase)
                ? "bg_" + img.ToLower()
                : img.ToLower();

            // Checking if the key exists in the DataChar collection and adding it to assets if it does
            if (!string.IsNullOrWhiteSpace(key) && _prts.Res.DataChar.ContainsKey(key))
            {
                var url = _prts.Res.DataChar[key];
                Assets.Add(new ResItem(key, url));
            }
            else
            {
                // Log or handle the error where the key does not exist
                Console.WriteLine($"<{commandDict["type"]}> Linked key [{key}] not exist.");
            }
        }
        return urls;
    }

    private void GetTweensToOverride(StringDict commandDict)
    {
        if (!_isTweenNeedsOverride) return;
        var isOverTweenExists = _prts.Res.DataOverrideDocument.RootElement.TryGetProperty("tween", out var tweens);
        if (!isOverTweenExists) return;

        var isOverPageTweenExists = tweens.TryGetProperty(_page, out var pageTweens) && isOverTweenExists;
        if (!isOverPageTweenExists)
        {
            _isTweenNeedsOverride = false;
            return;
        }


        var isTweenNeedOverride = pageTweens.TryGetProperty((_counter + 1).ToString(), out var tweenOverrides) &&
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

            _textList[_counter].OriginalText = SerializeCommandDict(commandDict);
        }
    }

    private List<string> ProcessSoundsCommand(StringDict commandDict)
    {
        List<string> audioKeys = new();
        List<string> urls = new();

        // If the command is "playmusic" and an intro is specified, add it to the list
        if (commandDict["type"] == "playmusic" && commandDict.TryGetValue("intro", out var intro)) audioKeys.Add(intro);

        // Always add the main key if it exists
        if (commandDict.TryGetValue("key", out var key)) audioKeys.Add(key);

        foreach (var audioKey in audioKeys)
        {
            var audioUrl = _prts.GetRealAudioUrl(audioKey);
            if (!string.IsNullOrWhiteSpace(audioUrl))
            {
                urls.Add(audioUrl);
                Assets.Add(new ResItem(audioKey, audioUrl));
            }
            else
                // Log or handle the error where the audio URL does not exist
                Console.WriteLine($"<audio> Linked key [{audioKey}] not exist.");
        }
        return urls;
    }

    private void GetCharactersToOverride(StringDict commandDict)
    {
        if (!_isCharacterNeedsOverride) return;
        var isOverCharacterExists = _prts.Res.DataOverrideDocument.RootElement.TryGetProperty("char", out var tweens);
        if (!isOverCharacterExists) return;

        var isOverPageCharacterExists = tweens.TryGetProperty(_page, out var pageCharacters) && isOverCharacterExists;
        if (!isOverPageCharacterExists)
        {
            _isCharacterNeedsOverride = false;
            return;
        }

        var isCharacterNeedOverride =
            pageCharacters.TryGetProperty((_counter + 1).ToString(), out var characterOverrides) &&
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

            _textList[_counter].OriginalText = SerializeCommandDict(commandDict);
        }
    }

    private void GetImagesToOverride(StringDict commandDict)
    {
        if (!_isImageNeedsOverride) return;
        // 尝试获取'image'属性
        var isOverImageExists = _prts.Res.DataOverrideDocument.RootElement.TryGetProperty("image", out var images);
        // 如果'image'属性不存在，则立即返回
        if (!isOverImageExists) return;

        // 尝试获取对应页的'image'属性
        var isOverPageImageExists = images.TryGetProperty(_page, out var pageImages) && isOverImageExists;
        // 如果对应页的'image'属性不存在，则立即返回
        if (!isOverPageImageExists)
        {
            _isImageNeedsOverride = false;
            return;
        }


        // 尝试获取需要覆盖的具体图片
        var isImageNeedOverride = pageImages.TryGetProperty((_counter + 1).ToString(), out var imageOverrides) &&
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

            _textList[_counter].OriginalText = SerializeCommandDict(commandDict);
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
