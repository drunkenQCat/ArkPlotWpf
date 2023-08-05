using System;
using System.Text.RegularExpressions;

namespace ArkPlotWpf.Model;

public record SentenceMethod(Regex Regex, Func<string, string> Method);