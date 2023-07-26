using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ArkPlotWpf.Model;

public record Plot(string Title, string Content);

public class PlotCollection
{
    public List<KeyValuePair<string, string>> Collection;

    public PlotCollection(JToken story)
    {
        var plots = story["infoUnlockDatas"]?.ToObject<JArray>();
        var colle =
            from chapter in plots
            let title = $"{chapter["storyCode"]} {chapter["storyName"]} {chapter["avgTag"]}"
            let txt = chapter["storyTxt"].ToString()
            let plot = new KeyValuePair<string, string>(title, txt)
            select plot;
        Collection = colle.ToList();

    }
}