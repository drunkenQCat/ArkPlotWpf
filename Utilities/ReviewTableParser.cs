using System.Linq;
using Newtonsoft.Json.Linq;

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
    private string TableUrl => $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/{lang}/gamedata/excel/story_review_table.json";
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
        var jsonContent = NetworkUtility.GetAsync(TableUrl).GetAwaiter().GetResult();
        reviewTable = JObject.Parse(jsonContent);
    }

    public List<JToken> GetStories(string type)
    {
        var stories =
          // TODO:解决可能出现的解引用问题
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
