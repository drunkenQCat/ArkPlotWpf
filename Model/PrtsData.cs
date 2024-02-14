namespace ArkPlotWpf.Model;

public class PrtsData
{
    public readonly string Tag;
    /* public StringDict Data; */
    public readonly StringDict Data;

    public PrtsData(string v)
    {
        Tag = v;
        Data = new StringDict();
    }

    public PrtsData(string tag, StringDict data)
    {
        Tag = tag;
        Data = data;
    }
}

