namespace ArkPlotWpf.Data.Entities;

public class PlotEntity
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public long ActId { get; set; }
}
