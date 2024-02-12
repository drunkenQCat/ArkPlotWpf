using ArkPlotWpf.Model;
using System.Linq;

namespace ArkPlotWpf.Utilities;

internal class AkParser
{

    private readonly PlotRegs plotRegs;

    public AkParser(string jsonPath)
    {
        plotRegs = new PlotRegs(jsonPath);
    }

    public void ConvertToMarkdown(StringBuilder plotBuilder)
    {
        var s = plotBuilder.ToString();
        var lines = s.Split("\n");
        // 每一章的第一个有效句一定是分隔线
        const string separateLine = "---";
        LineCounter prevLine = new(separateLine);
        plotBuilder.Clear();

        var lineCounters = new List<LineCounter>();

        bool IsDupOrEmptyLine(LineCounter newLine)
        {
            if (newLine.Line == "") return true;
            if (newLine.Line != prevLine.Line) return false;
            newLine.Counter++;
            return true;
        }

        void DescendDupLines(LineCounter newLine)
        {
            if (newLine.Counter <= 1 || prevLine.Line == separateLine) return;
            // 合并重复的行数，比如: 音效：sword x 5
            newLine.Line.TrimEnd();
            newLine.Line = prevLine.Line + " × " + newLine.Counter;
        }

        foreach (var line in lines)
        {
            var matched = ClassifyAndProcess(line);
            LineCounter newLine = new(matched);
            if (IsDupOrEmptyLine(newLine)) continue;
            var _ = newLine;
            DescendDupLines(newLine);
            prevLine = _;
            LinkDetect(newLine, lineCounters);
        }

        var output = from l in lineCounters
                     select l.Line;
        var reconstructor = new MdReconstructor(output);
        reconstructor.GetResultToBuilder(plotBuilder);
    }

    private static void LinkDetect(LineCounter newLine, List<LineCounter> lineCounters)
    {
        if (newLine.Line[0] != '\n')
        {
            lineCounters.Add(newLine);
            return;
        }
        var newLineSplited = newLine.Line.TrimStart().Split('\n');
        foreach (string s in newLineSplited)
        {
            lineCounters.Add(new LineCounter(s));
        }
    }

    private string ClassifyAndProcess(string line)
    {
        var sentenceProcessor = plotRegs.RegexAndMethods
            .FirstOrDefault(proc => proc.Regex.Match(line).Success);
        if (sentenceProcessor == null) return line;
        var result = sentenceProcessor.Method(line);
        return result;
    }

    private class LineCounter
    {
        public string Line;

        public int Counter = 1;

        public LineCounter(string line)
        {
            Line = line;
        }
    }
}
