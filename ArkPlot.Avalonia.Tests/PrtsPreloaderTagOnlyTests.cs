using System.Linq;
using System.Text;
using ArkPlot.Arknights;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
using Xunit;

namespace ArkPlot.Avalonia.Tests;

public class PrtsPreloaderTagOnlyTests
{
    [Fact]
    public void Parse_TagOnlyCharslot_ClearsPortraitsForFollowingDialog()
    {
        var content = new StringBuilder(string.Join("\n", new[]
        {
            "[charslot]",
            "[name=\"B\"]after"
        }));

        var plotManager = new PlotManager("test", content);
        plotManager.InitializePlot();

        var preloader = new PrtsPreloader(plotManager);
        preloader.ParseAndCollectAssets();

        var entries = plotManager.CurrentPlot.TextVariants.Cast<FormattedTextEntry>().ToList();
        Assert.Equal("charslot", entries[0].Type);
        Assert.Contains("transparent.png", entries[1].Portraits[0]);
    }

    [Fact]
    public void Parse_TagOnlyCharacter_ClearsPortraitsForFollowingDialog()
    {
        var content = new StringBuilder(string.Join("\n", new[]
        {
            "[Character]",
            "[name=\"B\"]after"
        }));

        var plotManager = new PlotManager("test", content);
        plotManager.InitializePlot();

        var preloader = new PrtsPreloader(plotManager);
        preloader.ParseAndCollectAssets();

        var entries = plotManager.CurrentPlot.TextVariants.Cast<FormattedTextEntry>().ToList();
        Assert.Equal("character", entries[0].Type);
        Assert.Contains("transparent.png", entries[1].Portraits[0]);
    }
}

