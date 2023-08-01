using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

namespace ArkPlotWpf.Model;

class ResourceCsv
{
    /// table of Background Images
    public StringDict DataImage = new();
    /// table of Character Images
    public StringDict DataChar = new();
    /// table of Sound Effects and Musics
    public StringDict DataAudio = new();
    /* public StringDict DataOverride = new(); */
    /* public StringDict DataLink = new(); */
    readonly List<PrtsData> allData;
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
        allData = new(){
            new PrtsData( "Data_Image", DataImage ),
            new PrtsData(  "Data_Char", DataChar  ),
            new PrtsData( "Data_Audio", DataAudio)
            /* { "Data_Override", DataOverride }, */
            /* { "Data_Link", DataLink } */
        };
    }

    public async Task GetAllCsv()
    {
        List<Task> tasks = new();
        foreach (var data in allData)
        {
            tasks.Add(GetCsv(data));
        }
        await Task.WhenAll(tasks);

    }



    private async Task GetCsv(PrtsData singleData)
    {
        // 所有csv都是从Prts的模板里扒下来的
        var prtsTemplateUrl = $"https://prts.wiki/index.php?title=Widget:{singleData.Tag}&action=edit";
        var config = Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(prtsTemplateUrl);
        var csv = StripHtml(document);
        var csvItems = LinesSplitter(csv);
        ParseCsvItems(singleData.Data, csvItems);
    }

    private static void ParseCsvItems(StringDict csvDict, IEnumerable<string> csvItems)
    {
        foreach (var item in csvItems)
        {
            var keyValue = item.Split(",");
            // for music, the data have to reprocess because it's fucking json
            var isJsonItem = JudgeJsonItem(keyValue);
            if (isJsonItem)
            {
                var jsonItems = ParseSingleJsonItem(keyValue[0]);
                if (jsonItems == null) continue;
                keyValue = jsonItems;
            }
            if (keyValue.Length == 2) csvDict[keyValue[0]] =
              isJsonItem ? GetMusicUrl(keyValue[1]) : GetItemUrl(keyValue[1]);
        }
    }

    private static bool JudgeJsonItem(string[] keyValue)
    {
        if (keyValue.Length < 2) return false;
        keyValue[1] = keyValue[1].Trim();
        bool isJsonItem = keyValue is [_, ""];
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

    private string StripHtml(IDocument inputDom)
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
        var csvElement = inputDom.QuerySelector("#wpTextbox1");
        return Regex.Replace(csvElement!.TextContent, "<[a-zA-Z/].*?>", String.Empty);
    }
    private IEnumerable<string> LinesSplitter(string plot)
    {
        return plot.Split("\n");
    }
}
