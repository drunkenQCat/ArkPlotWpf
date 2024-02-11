using AngleSharp.Common;
using ArkPlotWpf.Utilities;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ArkPlotWpf.Model;

class ResourceCsv
{
    /// table of Background Images
    public readonly StringDict DataImage = new();
    /// table of Character Images
    public readonly StringDict DataChar = new();
    /// table of Sound Effects and Musics
    public readonly StringDict DataAudio = new();
    /// <summary>
    /// table of links in prts
    /// </summary>
    public readonly StringDict DataLink = new();
    public JsonDocument PortraitLinkDocument;
    /* public StringDict DataOverride = new(); */
    /* public StringDict DataLink = new(); */
    private readonly List<PrtsData> _allData;
    private static ResourceCsv? instance = null;
    public static ResourceCsv Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new ResourceCsv();
            }
            return instance;
        }
    }


    public ResourceCsv()
    {
        _allData = new(){
            new PrtsData( "Data_Image", DataImage ),
            new PrtsData( "Data_Char", DataChar  ),
            new PrtsData( "Data_Audio", DataAudio ),
            new PrtsData( "Data_Link", DataLink )
            /* { "Data_Override", DataOverride }, */
            /* { "Data_Link", DataLink } */
        };
    }

    public async Task GetAllCsv()
    {
        var tasks = _allData.Select(GetCsv).ToList();
        await Task.WhenAll(tasks);

    }

    private async Task GetCsv(PrtsData singleData)
    {
        // 所有csv都是从Prts的模板里扒下来的
        var prtsTemplateUrl = "https://prts.wiki/api.php?action=expandtemplates&format=json&text={{Widget:" + singleData.Tag + "}}";
        var query = await NetworkUtility.GetAsync(prtsTemplateUrl);
        var csv = ProcessQuery(query);
        if(csv is null) return;
        if (singleData.Tag == "Data_Link") {
            csv = Regex.Unescape(csv);
            PortraitLinkDocument = GetPortraitLinkUrl(csv);
            return;
        }
        var csvItems = LinesSplitter(csv);
        ParseItemList(singleData, csvItems);
    }

    private JsonDocument GetPortraitLinkUrl(string portraitLinkJson)
    {
        var jsonElement = JsonDocument.Parse(portraitLinkJson);
        return jsonElement;
    }

    private static void ParseItemList(PrtsData prts, IEnumerable<string> csvItems)
    {
        var csvDict = prts.Data;
        foreach (var item in csvItems)
        {
            var keyValue = item.Split(",");
            // for music, the data have to reprocess because it's fucking json
            var isJsonItem = JudgeJsonItemAndTrimKey(keyValue);
            var title = keyValue[0];
            if (isJsonItem)
            {
                var jsonItems = ParseSingleJsonItem(title);
                if (jsonItems == null) continue;
                keyValue = jsonItems;
            }
            if (keyValue.Length != 2) continue;

            if (!isJsonItem)
            {
                csvDict[title] = GetItemUrl(keyValue[1]);
                continue;
            }
            if (prts.Tag == "Data_Audio") csvDict[title] = GetMusicUrl(keyValue[1]);
        }
    }

    private static bool JudgeJsonItemAndTrimKey(string[] keyValue)
    {
        if (keyValue.Length < 2) return false;
        keyValue[1] = keyValue[1].Trim();
        // if value is empty string, it's json dictionary item
        //   "axia_name": "小小小天使"
        var isJsonItem = keyValue is [_, ""];
        return isJsonItem;
    }

    private static string GetMusicUrl(string url)
    {
        // from:
        // Sound_Beta_2/General/g_ui/g_ui_stagepush
        // to:
        // music/general/g_ui/g_ui_stagepush.mp3
        url = url.ToLower();
        // rip out https://
        var urlToken = url.Split('/');
        // [0] is "sound_beta_2"
        urlToken[0] = "music";
        return "https://static.prts.wiki/" + string.Join("/", urlToken) + ".mp3";
    }

    private static string[]? ParseSingleJsonItem(string jsonItem)
    {
        // for some history reason, the json contains some strange items, like:
        /*
         ```
         \"axia_name\": \"小小小天使\",\n  \"bg_width\": 0.5,\n  \"bg_height\": 1.5,\n\n  \"avatar_sys\": \"system_100_mys\",\n  \"avatar_doberm\": \"char_130_doberm\",\n  \"avatar_jesica\": \"char_235_jesica\",\n
         ```
         */
        // so we have to skip these useless items.
        if (!jsonItem.Contains("Sound")) return null;
        var items = jsonItem.Replace("\"", "").Split(':');
        items =
            (from i in items
             select i.Trim()).ToArray();

        return items;
    }

    private static string GetItemUrl(string v)
    {
        return $"https://prts.wiki{v}";
    }

    private string? ProcessQuery(string inputQuery)
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
    private IEnumerable<string> LinesSplitter(string plot)
    {
        return plot.Split("\n");
    }
}
