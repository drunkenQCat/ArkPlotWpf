using ArkPlotWpf.Model;

namespace ArkPlotWpf.Data.Repositories;

/// <summary>
/// PrtsAssets专用的Repository接口
/// </summary>
public interface IPrtsAssetsRepository
{
    /// <summary>
    /// 保存PrtsAssets的所有数据到数据库
    /// </summary>
    /// <param name="prtsAssets">要保存的PrtsAssets实例</param>
    void Save(PrtsAssets prtsAssets);

    /// <summary>
    /// 从数据库加载PrtsAssets的所有数据
    /// </summary>
    /// <returns>加载了数据的PrtsAssets实例</returns>
    PrtsAssets Load();

    /// <summary>
    /// 根据标签获取特定的PrtsData
    /// </summary>
    /// <param name="tag">数据标签</param>
    /// <returns>PrtsData实例</returns>
    PrtsData? GetPrtsDataByTag(string tag);

    /// <summary>
    /// 更新特定的PrtsData
    /// </summary>
    /// <param name="prtsData">要更新的PrtsData</param>
    void UpdatePrtsData(PrtsData prtsData);

    /// <summary>
    /// 删除特定的PrtsData
    /// </summary>
    /// <param name="tag">要删除的数据标签</param>
    void DeletePrtsData(string tag);
} 