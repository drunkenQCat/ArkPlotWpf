namespace ArkPlotWpf.Model;

public class PrtsData
{
    public readonly string Tag;
    /* public StringDict Data; */
    public StringDict Data;
    public PrtsData(string tag, StringDict data)
    {
        Tag = tag;
        Data = data;
    }
}

