
using System.Text.RegularExpressions;

namespace ArkPlotWpf.Model;

public partial class PlotRegs
{
    [GeneratedRegex("(?<=(\\[name=\")|(\\[multiline\\(name=\")).*(?=\")", RegexOptions.Compiled)]
    private static partial Regex NameRegex();

    [GeneratedRegex("\\[name.*\\]|\\[multiline.*\\]", RegexOptions.Compiled)]
    private static partial Regex RegexToSubName();

    [GeneratedRegex("(?<=\\[)[A-Za-z]*(?=\\])", RegexOptions.Compiled)]

    private static partial Regex SegmentRegex();

    [GeneratedRegex("^[^\\[].*$", RegexOptions.Compiled)]
    private static partial Regex CommentRegex();

    [GeneratedRegex("(?<=(\\[(?!name))).*(?=\\()", RegexOptions.Compiled)]
    private static partial Regex SpecialTagRegex();
}
