using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArkPlotWpf.Model;

namespace ArkPlotWpf.Utilities.PrtsComponents;

public partial class PrtsDataProcessor
{
    public readonly PrtsAssets Res = PrtsAssets.Instance;

    public async Task GetAllData()
    {
        var tasks = Res.AllData.Select(GetSingleData).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task GetSingleData(PrtsData singleData)
    {
        // 所有csv都是从Prts的模板里扒下来的
        var prtsTemplateUrl = "https://prts.wiki/api.php?action=expandtemplates&format=json&text={{Widget:" +
                              singleData.Tag + "}}";
        var query = await NetworkUtility.GetAsync(prtsTemplateUrl);
        var csv = ProcessQuery(query);
        if (csv is null) return;
        if (singleData.Tag == "Data_Link")
        {
            Res.PortraitLinkDocument = GetPortraitLinkDocument(csv);
            return;
        }

        if (singleData.Tag == "Data_Override")
        {
            var overrideTxt = csv;
            var overrideItems = LinesSplitter(overrideTxt);
            Res.DataOverrideDocument = ParseOverrideList(overrideItems);
            return;
        }

        var csvItems = LinesSplitter(csv);
        ParseItemList(singleData, csvItems);
    }

    private JsonDocument ParseOverrideList(IEnumerable<string> lines)
    {
        var publicDisabled = false; // Assuming this is a flag you need to track

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            var parts = line.Split(':', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
                continue;

            var key = parts[0].Trim().ToLower();
            var value = parts[1].Trim();
            switch (key)
            {
                case "title":
                    ParseTitle(value);
                    break;
                case "char":
                case "image":
                case "tween":
                case "override":
                    ParseKeyValueStructure(key, value);
                    break;
                case "disable":
                    ParseDisable(value, ref publicDisabled);
                    break;
            }
        }

        // Convert the ride dictionary to a JsonDocument
        var json = JsonSerializer.Serialize(Res.RideItems);
        var options = new JsonDocumentOptions { AllowTrailingCommas = true };
        return JsonDocument.Parse(json, options);
    }

    private void ParseTitle(string value)
    {
        var parts = value.Split('=', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            return;

        var p = parts[0];
        var n = parts[1];
        if (!Res.RideItems.ContainsKey("title")) Res.RideItems["title"] = new Dictionary<string, object>();
        Res.RideItems["title"][p] = n;
    }

    private void ParseKeyValueStructure(string key, string value)
    {
        var mainParts = value.Split(';', 2);
        if (mainParts.Length != 2)
            return;

        var locationParts = mainParts[0].Split(',');
        if (locationParts.Length != 2)
            return;

        var page = locationParts[0];
        var line = locationParts[1];
        var values = mainParts[1].Split(',');

        var obj = new Dictionary<string, string>();
        foreach (var pair in values)
        {
            var kv = pair.Split('=');
            if (kv.Length == 2)
                obj[kv[0]] = kv[1];
        }

        if (!Res.RideItems.ContainsKey(key)) Res.RideItems[key] = new Dictionary<string, object>();
        if (!Res.RideItems[key].ContainsKey(page)) Res.RideItems[key][page] = new Dictionary<string, object>();
        ((Dictionary<string, object>)Res.RideItems[key][page])[line] = obj;
    }

    private void ParseDisable(string value, ref bool publicDisabled)
    {
        if (publicDisabled) return;

        var parts = value.Split(';');
        if (parts is ["public", _])
        {
            // Assuming handling of the public disable note
            publicDisabled = true;
            // Directly setting the note for public disable
            if (!Res.RideItems.ContainsKey("disable")) Res.RideItems["disable"] = new Dictionary<string, object>();
            Res.RideItems["disable"]["note"] = parts[1];
        }
        else
        {
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length != 2 || string.IsNullOrWhiteSpace(kv[1])) continue;

                switch (kv[0])
                {
                    case "prefix":
                    case "title":
                        if (!Res.RideItems.ContainsKey("disable"))
                            Res.RideItems["disable"] = new Dictionary<string, object>();
                        if (!Res.RideItems["disable"].ContainsKey(kv[0]))
                            Res.RideItems["disable"][kv[0]] = new Dictionary<string, object>();
                        ((Dictionary<string, object>)Res.RideItems["disable"][kv[0]])[kv[1]] =
                            ""; // Assuming empty value signifies disabled
                        break;
                    case "note":
                        // Inline handling for "note" case
                        if (!Res.RideItems.ContainsKey("disable"))
                            Res.RideItems["disable"] = new Dictionary<string, object>();
                        if (!Res.RideItems["disable"].ContainsKey("note"))
                            Res.RideItems["disable"]["note"] = new Dictionary<string, string>();
                        // Assuming each note should be tied to a specific prefix or title, but since those aren't specified here,
                        // you might need to adjust how notes are associated or stored based on your application's logic.
                        ((Dictionary<string, string>)Res.RideItems["disable"]["note"])[parts[0]] = kv[1];
                        break;
                }
            }
        }
    }

    private JsonDocument GetPortraitLinkDocument(string portraitLinkJson)
    {
        var jsonElement = JsonDocument.Parse(portraitLinkJson);
        return jsonElement;
    }

    private static void ParseItemList(PrtsData prts, IEnumerable<string> itemList)
    {
        var csvDict = prts.Data;
        if (prts.Tag == "Data_Audio")
        {
            ParseAudioJson(itemList);
        }
        else
        {
            ParseNormalCsv(itemList);
        }
        /*
         * Separated function to parse the Data_Audio json
         */
        void ParseAudioJson(IEnumerable<string> jsonItems)
        {
            foreach (var item in jsonItems)
            {
                // remove the useless spaces
                var keyValue = item.Trim();
                var audioJsonItem = ParseAudioJsonItem(keyValue);
                if (audioJsonItem == null) continue;
                csvDict[audioJsonItem[0]] = GetAudioLink(audioJsonItem[1]);
            }
        }
        /*
         * Separated function to parse csv data
         */
        void ParseNormalCsv(IEnumerable<string> csvItems)
        {
            foreach (var item in csvItems)
            {
                var keyValue = item.Trim().Split(",");
                // filter the non-csv items
                if (keyValue.Length != 2) continue;
                var title = keyValue[0];
                var url = keyValue[1];
                csvDict[title] = url;
            }
        }
    }

    private static string GetAudioLink(string url)
    {
        // from:
        // Sound_Beta_2/General/g_ui/g_ui_stagepush
        // to:
        // music/general/g_ui/g_ui_stagepush.mp3
        url = url.ToLower();
        var urlToken = url.Split('/');
        // [0] is "sound_beta_2"
        urlToken[0] = "audio";
        return PrtsAssets.AudioAssetsUrl + string.Join("/", urlToken) + ".mp3";
    }

    public string GetRealAudioUrl(string audioKey)
    {
        if (string.IsNullOrEmpty(audioKey))
            return "";

        var audioKeyLower = audioKey.ToLower();

        if (audioKey.StartsWith("$"))
            // 假设data.Audio是一个Dictionary<string, string>类型的字段或属性
            return Res.DataAudio.ContainsKey(audioKeyLower[1..]) ? Res.DataAudio[audioKeyLower[1..]] : "";

        if (audioKey.StartsWith("@")) return string.Concat(PrtsAssets.AudioAssetsUrl, audioKeyLower[1..]);
        return PrtsAssets.AudioAssetsUrl + audioKeyLower.Replace("sound_beta_2", "audio") + ".mp3";
    }

    private static string[]? ParseAudioJsonItem(string jsonItem)
    {
        // for some historical reason, the json contains some strange items, like:
        /*
         ```
         \"axia_name\": \"小小小天使\",\n  \"bg_width\": 0.5,\n  ...
         ```
         */
        // so we have to skip these useless items by the code below, to skip the non-sound items.
        if (!jsonItem.Contains("Sound")) return null;
        /*
         * the json item line is just like:
         * \"kingmouse_intro\": \"Sound_Beta_2/Music/act5d0/m_bat_kingmouse_intro\" (Audio Item)
         */
        var items = jsonItem.Replace("\"", "").Replace(",", "").Split(':');
        items =
            (from i in items
                select i.Trim()).ToArray();

        return items;
    }

    private static string? ProcessQuery(string inputQuery)
    {
        /* csv is the Element which id is wpTextbox1
         * and the csv is surrounded in <noincluedonly> like this:
         * <includeonly>20_i00,/images/8/8b/Avg_20_I00.png
            20_i01,/images/1/15/Avg_20_I01.png
            20_i02_1,/images/e/e7/Avg_20_I02_1.png
            20_i02_2,/images/2/2c/Avg_20_I02_2.png
            20_i02_3,/images/9/95/Avg_20_I02_3.png
            ...
            </includeonly>
         */
        var jsonElement = JsonDocument.Parse(inputQuery).RootElement.GetProperty("expandtemplates").GetProperty("*");
        var csvElement = jsonElement.GetString();
        return csvElement;
    }

    private static IEnumerable<string> LinesSplitter(string plot)
    {
        return plot.Split("\n");
    }
}