using System.IO;
using ArkPlot.Core.Data;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using Newtonsoft.Json.Linq;

namespace ArkPlot.Core.Model;

/// <summary>
/// 表示每个章节文字替换规则以及替换方法的类。
/// </summary>
public class PlotRules
{

    /// <summary>
    /// 获取或设置标签列表。
    /// </summary>
    public JObject TagList;

    /// <summary>
    /// 获取或设置包含正则表达式和方法的列表。
    /// </summary>
    public readonly List<SentenceMethod> RegexAndMethods = new();

    /// <summary>
    /// 获取 PlotRegs 类的单例实例。
    /// </summary>
    public static PlotRules Instance { get; } = new();

    /// <summary>
    /// 初始化 PlotRegs 类的新实例。
    /// </summary>
    private PlotRules()
    {
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.NameRegex(), PlotRegsBasicHelper.ProcessDialog));
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.SegmentRegex(), PlotRegsBasicHelper.MakeLine));
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.CommentRegex(), PlotRegsBasicHelper.MakeComment));
        TagList = JObject.Parse("{}");
    }

    /// <summary>
    /// 从 JSON 文件中读取正则表达式和方法。
    /// </summary>
    /// <param name="jsonPath">JSON 文件的路径。</param>
    public void GetRegsFromJson(string jsonPath)
    {
        TagList = JObject.Parse(File.ReadAllText(jsonPath));
    }
}