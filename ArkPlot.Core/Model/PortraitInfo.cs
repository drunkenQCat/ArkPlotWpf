namespace ArkPlot.Core.Model;

// for FocusOn,
// -1 is no focus
// 0 is middle and single portrait
// 1 is left and two portraits
// 2 is right and three portraits
public record PortraitInfo(List<string> Portraits, int FocusOn);
