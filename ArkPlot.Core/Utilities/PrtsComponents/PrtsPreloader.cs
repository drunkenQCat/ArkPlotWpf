using System.Text.Json;
using ArkPlot.Core.Data;
using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;
using ResItem = System.Collections.Generic.KeyValuePair<string, string>;

// Define the alias
namespace ArkPlot.Core.Utilities.PrtsComponents;

public partial class PrtsPreloader
{
    public readonly PreloadSet Assets = [];
    private readonly string _pageName;
    private readonly PrtsDataProcessor _prts = new();
    private int _counter;
    private bool _isCharacterNeedsOverride = true;
    private bool _isImageNeedsOverride = true;
    private bool _isTextNeedsOverride = true;
    private bool _isTweenNeedsOverride = true;
    private readonly List<FormattedTextEntry> _textList;

    private List<string> _currentPortraits = ["https://pics/transparent.png"];
    private int _currentPortraitFocus;
    // ά�� charsolt ��λ״̬��slot �� URL����ÿ�� charslot ����ֻ���¶�Ӧ��λ
    private readonly Dictionary<string, string> _slotUrls = new();
    private const string DefaultBg = "https://media.prts.wiki/8/8a/Avg_bg_bg_black.png";
    private string _currentBg = DefaultBg;

    public string PageName
    {
        get => _pageName;
    }
    public string Content = string.Empty;

    public PrtsPreloader(PlotManager plotManager)
    {
        var pageName = plotManager.CurrentPlot.Title;
        _ = plotManager.CurrentPlot.Content.ToString().Split("\n");
        // �������½������滻�� prts ҳ���ַ
        _pageName = pageName.Trim()
            .Replace(" �ж���", "/END")
            .Replace(" �ж�ǰ", "/BEG")
            .Replace(" Ļ��", "/NBT");
        //.Replace(" ", "_");
        _textList = plotManager.CurrentPlot.TextVariants;
        Content = plotManager.CurrentPlot.Content.ToString();
    }

    public void ParseAndCollectAssets()
    {
        foreach (var entry in _textList)
        {
            if (string.IsNullOrWhiteSpace(entry.OriginalText) || entry.OriginalText.TrimStart().StartsWith("//")) continue;

            OverrideCurrentText();
            var match = ArkPlotRegs.UniversalTagsRegex().Match(entry.OriginalText);
            if (!match.Success)
            {
                entry.Dialog = entry.OriginalText;
                entry.Portraits = _currentPortraits;
                entry.PortraitFocus = _currentPortraitFocus;
                entry.Bg = _currentBg;
                continue;
            }
            ;

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
                if (entry.Type == "sticker") entry.Dialog = GetStickerText(entry);
                if (entry.Type == "subtitle" && entry.CommandSet.TryGetValue("text", out var subtitle))
                {
                    entry.Dialog = subtitle ?? "";
                }

                entry.ResourceUrls = urlList;

                // charslot focus="none"��˵���߲��ڻ����У������ɶ�������
                if (entry.Type == "charslot" && entry.ResourceUrls.Count == 0
                    && entry.CommandSet.TryGetValue("focus", out var fn) && fn == "none")
                    entry.SkipPortraitOutput = true;

                // CommandSet ���������� focus ����ȡ name ���� name2
                // �����������character/charactercutin/charslot�������� CharacterCode
                // �������background/playsound �ȣ�������գ�������һ����Ŀ�� code й©
                var isPortraitType = entry.Type is "character" or "charactercutin" or "charslot";
                if (isPortraitType)
                {
                    var focusName = "name";
                    if (entry.CommandSet.TryGetValue("focus", out var focusVal) &&
                        focusVal == "2" && entry.CommandSet.ContainsKey("name2"))
                    {
                        focusName = "name2";
                    }
                    if (entry.CommandSet.TryGetValue(focusName, out var rawName))
                    {
                        entry.CharacterCode = _prts.GetCharacterCode(rawName.ToLower());
                    }
                }
                else
                {
                    entry.CharacterCode = null;
                }
            }
            else if (!string.IsNullOrEmpty(matchedTagOnly))
            {
                var tagOnly = matchedTagOnly.Trim().ToLowerInvariant();
                if (tagOnly is "charslot" or "character")
                {
                    entry.CommandSet = ProcessCommand(tagOnly, "", out List<string> urlList);
                    entry.Type = entry.CommandSet["type"];
                    entry.ResourceUrls = urlList;
                    entry.CharacterCode = null;
                }
            }
            entry.Portraits = _currentPortraits;
            entry.PortraitFocus = _currentPortraitFocus;
            entry.Bg = _currentBg;
            _counter++;
        }
    }

    private StringDict ProcessCommand(string command, string parameters, out List<string> urls)
    {
        var commandDict = parameters.ToCommandSet();
        commandDict["type"] = command;
        urls = [];
        switch (command)
        {
            case "background":
            case "image":
            case "showitem":
                urls = ProcessImageCommand(commandDict);
                if (urls.Count != 0 && !string.IsNullOrEmpty(urls[0]))
                    _currentBg = urls[0];
                else
                    _currentBg = DefaultBg;
                break;
            // Additional command processing as needed
            case "backgroundtween":
            case "imagetween":
            case "largebgtween":
            case "largeimgtween":
                GetTweensToOverride(commandDict);
                goto case "character";
            case "character":
                if (!commandDict.TryGetValue("name", out var cName) || string.IsNullOrEmpty(cName))
                {
                    _slotUrls.Clear();
                    _currentPortraits = ["https://pics/transparent.png"];
                    _currentPortraitFocus = 0;
                    break;
                }
                urls = ProcessPortraitCommand(commandDict);
                (_currentPortraits, _currentPortraitFocus) = GetCurrentPortraitsFromCharacter(commandDict, urls);
                break;
            case "charactercutin":
                urls = ProcessPortraitCommand(commandDict);
                (_currentPortraits, _currentPortraitFocus) = GetCurrentPortraitsFromCutin(commandDict, urls);
                break;
            case "charslot":
                if (!commandDict.TryGetValue("slot", out var slotVal) || string.IsNullOrEmpty(slotVal))
                {
                    _slotUrls.Clear();
                    _currentPortraits = ["https://pics/transparent.png"];
                    _currentPortraitFocus = 0;
                    break;
                }
                urls = ProcessPortraitCommand(commandDict);
                (_currentPortraits, _currentPortraitFocus) = GetCurrentPortraitsFromSlot(commandDict, urls);
                // focus="none"��˵���߲��ڻ����У���� ResourceUrls�������� PicDesc��
                if (commandDict.TryGetValue("focus", out var csFocus) && csFocus == "none")
                    urls.Clear();
                break;
            case "gridbg":
            case "verticalbg":
            case "largebg":
            case "largeimg":
                urls = ProcessLargeImageCommand(commandDict);
                if (urls.Count != 0) _currentBg = urls[0];
                break;
            case "playmusic":
            case "playsound":
                urls = ProcessSoundsCommand(commandDict);
                break;
        }
        return commandDict;
    }

    private static string GetStickerText(FormattedTextEntry line)
    {
        if (!line.CommandSet.TryGetValue("text", out var text) || string.IsNullOrEmpty(text)) return "";
        var textWithoutSlashN = text.Replace(@"\n", "");
        if (!textWithoutSlashN.StartsWith('<')) return textWithoutSlashN;
        // if start with "<", it just like:
        // <i>����ܽ˹�������¡�������������˵�����ˣ�</i>
        // then just replace the tag with empty
        return RoundedWithTagRegex().Replace(textWithoutSlashN, "");
    }


    private static (List<string>, int) GetCurrentPortraitsFromCutin(StringDict commandDict, List<string> urls)
    {
        return (urls, 0);
    }

    private static (List<string>, int) GetCurrentPortraitsFromCharacter(StringDict commandDict, List<string> inputUrls)
    {
        List<string> urls = new(inputUrls);
        if (urls.Count == 0) return (["https://pics/transparent.png"], 0);
        if (commandDict.TryGetValue("focus", out string? position))
        {
            var focus = position switch
            {
                "-1" => -1,
                "1" => 1,
                "2" => 2,
                _ => 0
            };
            return (urls, focus);
        }
        return (urls, 0);
    }

    private (List<string>, int) GetCurrentPortraitsFromSlot(StringDict commandDict, List<string> urls)
    {
        if (!commandDict.TryGetValue("slot", out string? slot))
            return (urls, 0);

        bool isFocusNone = commandDict.TryGetValue("focus", out var focus) && focus == "none";

        // ���²�λ״̬���� name ��ʾ��ոò�λ��focus="none" ��Ӱ���λ��ֻ��û�˸�����
        bool isRemoving = !commandDict.TryGetValue("name", out var name) || string.IsNullOrEmpty(name);

        if (isRemoving)
            _slotUrls.Remove(slot);
        else if (urls.Count > 0)
            _slotUrls[slot] = urls[0];

        // focus="none"���������ڳ��ϣ���˵���߲��ڻ�����
        // ���ֲ�λ���䣬PortraitFocus=0�����˸�����
        // ResourceUrls ������� ProcessCommand �д���
        var merged = MergeSlotUrls(out int currentFocus, isFocusNone ? null : slot);

        if (merged.Count == 0)
            return (["https://pics/transparent.png"], 0);

        return (merged, currentFocus);
    }

    private List<string> MergeSlotUrls(out int currentFocus, string? focusSlot = null)
    {
        var merged = new List<string>();
        currentFocus = 0;
        string[] slotOrder = ["m", "l", "r"];
        foreach (var s in slotOrder)
        {
            if (_slotUrls.TryGetValue(s, out var url))
            {
                if (s == focusSlot) currentFocus = merged.Count;
                merged.Add(url);
            }
        }
        return merged;
    }

    private void OverrideCurrentText()
    {
        if (!_isTextNeedsOverride) return;
        // ���Ի�ȡ'override'����
        var isOverrideExists =
            _prts.Res.DataOverrideDocument.RootElement.TryGetProperty("override", out var overrideLineList);
        // ���'override'���Բ����ڣ�����������
        if (!isOverrideExists) return;

        // ���Ի�ȡ��Ӧҳ��'override'����
        var isOverPageImageExists =
            overrideLineList.TryGetProperty(_pageName, out var pageOverrideLineList) && isOverrideExists;
        // �����Ӧҳ��'override'���ݲ����ڣ�����������
        if (!isOverPageImageExists)
        {
            _isTextNeedsOverride = false;
            return;
        }


        // ���Ե�ǰ���Ƿ���Ҫ���ǣ�
        var isCurrentTextNeedOverride =
            pageOverrideLineList.TryGetProperty((_counter + 1).ToString(), out var lineOverrides) &&
            isOverPageImageExists;
        // �������Ҫ���ǣ�����������
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
        var key = commandDict.TryGetValue("image", out string? value) ? prefix + value.ToLower() : string.Empty;

        if (string.IsNullOrEmpty(key)) return []; // Skip if key is not valid

        if (!_prts.Res.DataImage.TryGetValue(key, out string? url))
        {
            // Log or handle the error where the key does not exist
            Console.WriteLine($"<image> Linked key [{key}] not exist.");
            return [];
        }

        Assets.Add(new ResItem(key, url));
        return [url];
    }

    private List<string> ProcessPortraitCommand(StringDict commandDict)
    {
        GetCharactersToOverride(commandDict);

        // �ռ���ɫ��
        var names = new List<string>();
        if (commandDict.TryGetValue("name", out var name)) names.Add(name.ToLower());
        if (commandDict["type"] == "character" && commandDict.TryGetValue("name2", out var name2))
            names.Add(name2.ToLower());

        // �ռ� URLs��CharacterCode ����ѭ���� CommandSet["name"] ֱ�ӻ�ȡ��
        List<string> urls = [];
        foreach (var characterName in names)
        {
            var url = _prts.GetPortraitUrl(characterName);
            urls.Add(url);
            Assets.Add(new ResItem(characterName, url));
        }

        return urls;
    }

    private List<string> ProcessLargeImageCommand(StringDict commandDict)
    {
        List<string> urls = [];
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
            if (!string.IsNullOrWhiteSpace(key) && _prts.Res.DataChar.TryGetValue(key, out string? url))
            {
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

        var isOverPageTweenExists = tweens.TryGetProperty(_pageName, out var pageTweens) && isOverTweenExists;
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
        List<string> audioKeys = [];
        List<string> urls = [];

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

        var isOverPageCharacterExists = tweens.TryGetProperty(_pageName, out var pageCharacters) && isOverCharacterExists;
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
        // ���Ի�ȡ'image'����
        var isOverImageExists = _prts.Res.DataOverrideDocument.RootElement.TryGetProperty("image", out var images);
        // ���'image'���Բ����ڣ�����������
        if (!isOverImageExists) return;

        // ���Ի�ȡ��Ӧҳ��'image'����
        var isOverPageImageExists = images.TryGetProperty(_pageName, out var pageImages) && isOverImageExists;
        // �����Ӧҳ��'image'���Բ����ڣ�����������
        if (!isOverPageImageExists)
        {
            _isImageNeedsOverride = false;
            return;
        }


        // ���Ի�ȡ��Ҫ���ǵľ���ͼƬ
        var isImageNeedOverride = pageImages.TryGetProperty((_counter + 1).ToString(), out var imageOverrides) &&
                                  isOverPageImageExists;
        // �����Ҫ���ǵ�ͼƬ�����ڣ�����������
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
    private static string SerializeCommandDict(StringDict commands)
    {
        var res = new List<string>();
        foreach (var cmd in commands) res.Add($"{cmd.Key}=\"{cmd.Value}\"");
        return $"[{commands["type"]}({string.Join(", ", res)})]";
    }

    [System.Text.RegularExpressions.GeneratedRegex("<.*?>")]
    private static partial System.Text.RegularExpressions.Regex RoundedWithTagRegex();
}
