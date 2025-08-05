using System.Threading.Tasks;
using SqlSugar;
using ArkPlotWpf.Model;

namespace ArkPlotWpf.Data.Repositories;

/// <summary>
/// PrtsData 仓储类，提供 PrtsData 实体的特定业务操作
/// </summary>
public class PrtsDataRepository : BaseRepository<PrtsData>
{
    public PrtsDataRepository(SqlSugarClient? db = null) : base(db)
    {
    }

    #region 业务特定方法

    /// <summary>
    /// 根据标签查询 PrtsData
    /// </summary>
    /// <param name="tag">标签</param>
    /// <returns>匹配的 PrtsData</returns>
    public PrtsData GetByTag(string tag) =>
        FirstOrDefault(x => x.Tag == tag);

    /// <summary>
    /// 根据标签模糊查询 PrtsData
    /// </summary>
    /// <param name="tag">标签关键词</param>
    /// <returns>匹配的 PrtsData 列表</returns>
    public List<PrtsData> GetByTagLike(string tag) =>
        GetWhere(x => x.Tag.Contains(tag));

    /// <summary>
    /// 根据标签前缀查询 PrtsData
    /// </summary>
    /// <param name="prefix">标签前缀</param>
    /// <returns>匹配的 PrtsData 列表</returns>
    public List<PrtsData> GetByTagPrefix(string prefix) =>
        GetWhere(x => x.Tag.StartsWith(prefix));

    /// <summary>
    /// 获取所有标签
    /// </summary>
    /// <returns>标签列表</returns>
    public List<string> GetAllTags() =>
        _db.Queryable<PrtsData>().Select(x => x.Tag).ToList();

    /// <summary>
    /// 检查标签是否存在
    /// </summary>
    /// <param name="tag">标签</param>
    /// <returns>是否存在</returns>
    public bool TagExists(string tag) =>
        Any(x => x.Tag == tag);

    /// <summary>
    /// 根据标签更新数据
    /// </summary>
    /// <param name="tag">标签</param>
    /// <param name="data">新数据</param>
    /// <returns>是否更新成功</returns>
    public bool UpdateDataByTag(string tag, StringDict data) =>
        Update(x => new PrtsData { Data = data }, x => x.Tag == tag);

    /// <summary>
    /// 批量更新数据
    /// </summary>
    /// <param name="dataList">数据列表，包含标签和数据</param>
    /// <returns>是否更新成功</returns>
    public bool UpdateDataBatch(List<(string tag, StringDict data)> dataList)
    {
        try
        {
            return UseTransaction(() =>
            {
                foreach (var (tag, data) in dataList)
                {
                    UpdateDataByTag(tag, data);
                }
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取包含特定键的数据
    /// </summary>
    /// <param name="key">键名</param>
    /// <returns>匹配的 PrtsData 列表</returns>
    public List<PrtsData> GetByDataKey(string key) =>
        GetWhere(x => x.Data.ContainsKey(key));

    /// <summary>
    /// 获取包含特定值的数据
    /// </summary>
    /// <param name="value">值</param>
    /// <returns>匹配的 PrtsData 列表</returns>
    public List<PrtsData> GetByDataValue(string value) =>
        GetWhere(x => x.Data.ContainsValue(value));

    /// <summary>
    /// 清空所有数据
    /// </summary>
    /// <returns>是否清空成功</returns>
    public bool ClearAll() =>
        _db.Deleteable<PrtsData>().ExecuteCommand() > 0;

    #endregion

    #region 异步业务方法

    /// <summary>
    /// 异步根据标签查询 PrtsData
    /// </summary>
    /// <param name="tag">标签</param>
    /// <returns>匹配的 PrtsData</returns>
    public async Task<PrtsData> GetByTagAsync(string tag) =>
        await FirstOrDefaultAsync(x => x.Tag == tag);

    /// <summary>
    /// 异步根据标签模糊查询 PrtsData
    /// </summary>
    /// <param name="tag">标签关键词</param>
    /// <returns>匹配的 PrtsData 列表</returns>
    public async Task<List<PrtsData>> GetByTagLikeAsync(string tag) =>
        await GetWhereAsync(x => x.Tag.Contains(tag));

    /// <summary>
    /// 异步根据标签前缀查询 PrtsData
    /// </summary>
    /// <param name="prefix">标签前缀</param>
    /// <returns>匹配的 PrtsData 列表</returns>
    public async Task<List<PrtsData>> GetByTagPrefixAsync(string prefix) =>
        await GetWhereAsync(x => x.Tag.StartsWith(prefix));

    /// <summary>
    /// 异步获取所有标签
    /// </summary>
    /// <returns>标签列表</returns>
    public async Task<List<string>> GetAllTagsAsync() =>
        await _db.Queryable<PrtsData>().Select(x => x.Tag).ToListAsync();

    /// <summary>
    /// 异步根据标签更新数据
    /// </summary>
    /// <param name="tag">标签</param>
    /// <param name="data">新数据</param>
    /// <returns>是否更新成功</returns>
    public async Task<bool> UpdateDataByTagAsync(string tag, StringDict data) =>
        await UpdateAsync(x => new PrtsData { Data = data }, x => x.Tag == tag);

    /// <summary>
    /// 异步获取包含特定键的数据
    /// </summary>
    /// <param name="key">键名</param>
    /// <returns>匹配的 PrtsData 列表</returns>
    public async Task<List<PrtsData>> GetByDataKeyAsync(string key) =>
        await GetWhereAsync(x => x.Data.ContainsKey(key));

    /// <summary>
    /// 异步获取包含特定值的数据
    /// </summary>
    /// <param name="value">值</param>
    /// <returns>匹配的 PrtsData 列表</returns>
    public async Task<List<PrtsData>> GetByDataValueAsync(string value) =>
        await GetWhereAsync(x => x.Data.ContainsValue(value));

    /// <summary>
    /// 异步清空所有数据
    /// </summary>
    /// <returns>是否清空成功</returns>
    public async Task<bool> ClearAllAsync() =>
        await _db.Deleteable<PrtsData>().ExecuteCommandAsync() > 0;

    #endregion
}
