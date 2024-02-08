using Newtonsoft.Json.Linq;
using System.Linq;

namespace ArkPlotWpf.Utilities;

public class ReviewTableParser
{
    private string lang;
    public string Lang
    {
        get => lang;
        set
        {
            lang = value;
            LoadJson(lang);
        }
    }

    private string GetTableUrl()
    {
        if (lang == "zh_CN")
        {
            return $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/{lang}/gamedata/excel/story_review_table.json";
        }

        return $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/master/{lang}/gamedata/excel/story_review_table.json";
    }

    private string KGithubTableUrl => $"https://raw.kgithub.com/Kengxxiao/ArknightsGameData/master/{lang}/gamedata/excel/story_review_table.json";
    private JObject? reviewTable;

    public List<JToken> TitleList => GetSideStory();
    public List<JToken> MiniStory => GetMiniStory();
    public List<JToken> MainStory => GetMainStory();

    public ReviewTableParser(string language)
    {
        lang = language;
        LoadJson(lang);
    }

    public ReviewTableParser()
    {
        lang = "zh_CN";
    }

    private void LoadJson(string s)
    {
        var jsonContent = NetworkUtility.GetAsync(GetTableUrl()).GetAwaiter().GetResult();
        reviewTable = JObject.Parse(jsonContent);
    }

    public List<JToken> GetStories(string type)
    {
        var stories =
            from item in reviewTable?.Children().ToList()
            let obj = item.ToObject<JProperty>()!.Value
            let actType = obj["actType"]!.ToString()
            where actType == type
            select obj;
        return stories.ToList();
    }
    private List<JToken> GetMiniStory()
    {
        return GetStories("MINI_STORY");
    }
    private List<JToken> GetSideStory()
    {
        return GetStories("ACTIVITY_STORY");
    }
    private List<JToken> GetMainStory()
    {
        return GetStories("MAIN_STORY");
    }
}
