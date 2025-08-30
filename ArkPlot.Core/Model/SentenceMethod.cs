using System;
using System.Text.RegularExpressions;

namespace ArkPlot.Core.Model;

/// <summary>
/// 表示一个文本处理规则，包含正则表达式模式和对应的处理方法�?
/// </summary>
/// <param name="Regex">用于匹配文本的正则表达式</param>
/// <param name="Method">处理匹配文本的方法，接收 FormattedTextEntry 参数并返回处理后的字符串</param>
public record SentenceMethod(Regex Regex, Func<FormattedTextEntry, string> Method);
