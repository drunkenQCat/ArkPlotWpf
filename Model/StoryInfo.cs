using System.Collections.Generic;

namespace ArkPlotWpf.Model;

public class ActInfo
{
    public string Lang;
    public string ActType;
    // public string Id;
    public string Name;
    public List<StoryInfo>? Chapters;

    public ActInfo(string lang, string actType, string name)
    {
        Lang = lang;
        ActType = actType;
        Name = name;
    }
}

public class StoryInfo
{
    public string Lang;
    public string Name;
    public string AvgTag;
    public string StoryTxt;
    public string Url => $"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/{Lang}/gamedata/story/{StoryTxt}.txt";
    public string Title => $"{Name} {AvgTag}";
}