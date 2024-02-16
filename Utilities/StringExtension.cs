﻿using ArkPlotWpf.Model;
using System;
using System.Text.RegularExpressions;
using System.Linq;

namespace ArkPlotWpf.Utilities;

public static class StringExtensions
{
    public static StringDict ToCommandSet(this string input, string sep1 = ",", string sep2 = "=", bool isToLower = true)
    {
        // Prepare the regex pattern based on sep1 and sep2
        string commandPattern = $@"\s*(.*?)\s*{Regex.Escape(sep2)}\s*(?:['""](.*?)['""]|([\w.-]+))\s*{Regex.Escape(sep1)}?";
        var regex = new Regex(commandPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var matches = regex.Matches(input);

        var result = new StringDict();
        foreach (var match in matches.Where(match => match.Success))
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;

            if (isToLower)
            {
                key = key.ToLower();
            }

            if (!result.ContainsKey(key)) // Prevent duplicate keys
            {
                result[key] = value;
            }
        }

        // Handling the case where no matches are found but a single pair might exist
        if (result.Count != 0 || matches.Count != 0)
        {
            return result;
        }
        var singleMatch = Regex.Match(input, commandPattern);
        if (singleMatch.Success)
        {
            string key = singleMatch.Groups[1].Value;
            string value = singleMatch.Groups[2].Success ? singleMatch.Groups[2].Value : singleMatch.Groups[3].Value;

            if (isToLower) key = key.ToLower();
            result[key] = value;
        }

        return result;
    }
    public static string GetValue(this string input, string sep = ":")
    {
        var p = input.LastIndexOf(sep, StringComparison.Ordinal);
        if (p == -1) return "";
        return input[(p + sep.Length)..];
    }

    public static string GetKey(this string input, string sep = ":")
    {
        var p = input.LastIndexOf(sep, StringComparison.Ordinal);
        if (p == -1) return input;
        return input[..p];
    }

    public static string[] ToArray(this string input, string sep)
    {
        return input.Replace("\r", "").Split(new[] { sep }, StringSplitOptions.None);
    }

}
