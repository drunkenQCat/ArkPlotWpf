namespace ArkPlot.Core.Model;

/// <summary>
/// 用于临时存储标签、正则表达式和新标签的类。
/// </summary>
public record TagReplacementRule(string Tag, string Reg, string NewTag);