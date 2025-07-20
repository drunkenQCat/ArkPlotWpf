using ArkPlotWpf.Model;

namespace ArkPlotWpf.Data.Repositories;

/// <summary>
/// PrtsData专用的Repository接口
/// </summary>
public interface IPrtsDataRepository : IRepository<PrtsData, long>
{
    /// <summary>
    /// 根据标签获取PrtsData
    /// </summary>
    /// <param name="tag">数据标签</param>
    /// <returns>PrtsData实例，如果不存在则返回null</returns>
    PrtsData? GetByTag(string tag);

    /// <summary>
    /// 根据标签删除PrtsData
    /// </summary>
    /// <param name="tag">要删除的数据标签</param>
    /// <returns>是否删除成功</returns>
    bool DeleteByTag(string tag);

    /// <summary>
    /// 检查指定标签的数据是否存在
    /// </summary>
    /// <param name="tag">数据标签</param>
    /// <returns>是否存在</returns>
    bool Exists(string tag);
} 