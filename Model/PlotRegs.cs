using Newtonsoft.Json.Linq;
using ArkPlotWpf.Data;
using ArkPlotWpf.Utilities.TagProcessingComponents;

namespace ArkPlotWpf.Model;

public class PlotRegs
{
    public readonly List<SentenceMethod> RegexAndMethods = new();
    public JObject TagList;

    public static PlotRegs  Instance { get; } = new();


    public void GetRegsFromJson(string jsonPath)
    {
        TagList = JObject.Parse(System.IO.File.ReadAllText(jsonPath));
    }

    private PlotRegs()
    {
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.NameRegex(), PlotRegsBasicHelper.ProcessName));
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.SegmentRegex(), PlotRegsBasicHelper.MakeLine));
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.CommentRegex(), PlotRegsBasicHelper.MakeComment));
        TagList = JObject.Parse("{}");
    }
}
