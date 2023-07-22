using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ArkPlotWpf.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ArkPlotWpf.Utilities;

internal class AkParser
{

    private readonly PlotRegs plotRegs;
    public string? MarkDown { get; private set;}
    
    public AkParser(string plot, string jsonPath)
    {
        plotRegs = new PlotRegs(jsonPath);
        ConvertToMarkdown(plot);
    }

    private string MatchType(string line)
    {
        var processedResults =  plotRegs.Processors
            .Select(proc => new { proc, matched = proc.Regex.Match(line) })
            .Where(@t => @t.matched.Value != "")
            .Select(@t => @t.proc.Method(line));
        var results = processedResults.ToList();
        return !results.Any() ? line : results[0];
    }

    private static IEnumerable<string> PlotSplitter(string plot)
    {
        return plot.Split("\n");
    }

    private static string RipDollar(string text)
    {
        text = Regex.Replace(text, @"\$", "");
        return text;
    }
    private void ConvertToMarkdown(string plotText)
    {
        var lines = PlotSplitter(plotText);
        // 每一章的第一个有效句一定是分隔线
        var duplicateLineCount = 1; // initialize the duplicateLineCount of duplicated lines
        var prevLine = "\r\n\r\n---";
        void DescendDupLines(ref string? newLine)
        {
            if (prevLine == "\r\n\r\n---")
                newLine = prevLine;
            else if (duplicateLineCount == 1)
                newLine = prevLine;
            else if (duplicateLineCount > 1)
                newLine = prevLine + "×" + duplicateLineCount;
        }

        bool IsLineDup(string newLine)
        {
            if (newLine != prevLine) return false;
            if (newLine == "") return true;
            duplicateLineCount++;
            return true;

        }

        foreach (var line in lines)
        {
            var newLine = MatchType(line);
            if (IsLineDup(newLine)) continue;
            
            var currentLine = newLine;
            DescendDupLines(ref newLine);
            prevLine = currentLine;
            duplicateLineCount = 1; // initialize the duplicateLineCount of duplicated lines
            MarkDown = MarkDown + newLine + "\r\n";
        }

        if (MarkDown == null)
        {
            Console.WriteLine("什么都没写上去");
            Environment.Exit(1);
        }
        MarkDown = RipDollar(MarkDown);
    }

}