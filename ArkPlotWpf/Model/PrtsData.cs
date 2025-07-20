namespace ArkPlotWpf.Model;

/// <summary>
/// 表示用于临时存储PRTS数据的容器。
/// </summary>
public class PrtsData
{
    /* public StringDict Data; */
    public readonly StringDict Data;
    public readonly string Tag;

    /// <summary>
    /// 使用指定的标签初始化 <see cref="PrtsData"/> 类的不使用字典的实例。
    /// </summary>
    /// <param name="tag">与PRTS数据关联的标签。</param>
    public PrtsData(string tag)
    {
        Tag = tag;
        Data = new StringDict();
    }

    /// <summary>
    /// 使用指定的标签和数据初始化 <see cref="PrtsData"/> 类的新实例。
    /// </summary>
    /// <param name="tag">与PRTS数据关联的标签。</param>
    /// <param name="data">PRTS数据。</param>
    public PrtsData(string tag, StringDict data)
    {
        Tag = tag;
        Data = data;
    }
}