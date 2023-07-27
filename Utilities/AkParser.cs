using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ArkPlotWpf.Model;

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
    
    private void ConvertToMarkdown(string plotText)
    {
        var lines = PlotSplitter(plotText);
        // 每一章的第一个有效句一定是分隔线
        var duplicateLineCount = 1; // initialize the duplicateLineCount of duplicated lines
        const string seperateLine = "\r\n\r\n---";
        var prevLine = seperateLine;
        void DescendDupLines(ref string newLine)
        {
            if (newLine == null) throw new ArgumentNullException(nameof(newLine));
            if (duplicateLineCount > 1 && prevLine != seperateLine)
                // 合并重复的行数，比如: 音效：sword x 5
            {
                newLine = prevLine + "×" + duplicateLineCount;
                return;
            }
            newLine = prevLine;
        }

        bool IsDupOrEmptyLine(string newLine)
        {
            if (newLine == "") return true;
            if (newLine != prevLine) return false;
            duplicateLineCount++;
            return true;

        }

        foreach (var line in lines)
        {
            var newLine = MatchType(line);
            if (IsDupOrEmptyLine(newLine)) continue;
            
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

    private string MatchType(string line)
    {
        var sentenceProcessor = plotRegs.RegexAndMethods
            .FirstOrDefault(proc => proc.Regex.Match(line).Success);
        if (sentenceProcessor == null)
        {
            return line;
        }
        var result = sentenceProcessor.Method(line);
        return result;
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

}