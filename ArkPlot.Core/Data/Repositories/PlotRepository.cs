using System.Threading.Tasks;
using SqlSugar;
using ArkPlot.Core.Model;

namespace ArkPlot.Core.Data.Repositories;

/// <summary>
/// Plot 仓储类，提供 Plot 实体的特定业务操作
/// </summary>
public class PlotRepository : BaseRepository<Plot>
{
    public PlotRepository(SqlSugarClient? db = null) : base(db)
    {
    }

    #region 业务特定方法

    /// <summary>
    /// 根据标题模糊查询 Plot
    /// </summary>
    /// <param name="title">标题关键词</param>
    /// <returns>匹配的 Plot 列表</returns>
    public List<Plot> GetByTitle(string title) =>
        GetWhere(x => x.Title.Contains(title));

    /// <summary>
    /// 根据标题精确查询 Plot
    /// </summary>
    /// <param name="title">标题</param>
    /// <returns>匹配的 Plot</returns>
    public Plot GetByTitleExact(string title) =>
        FirstOrDefault(x => x.Title == title);

    /// <summary>
    /// 根据标题分页查询 Plot
    /// </summary>
    /// <param name="title">标题关键词</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>(Plot 列表, 总数量)</returns>
    public (List<Plot>, int) GetByTitlePaged(string title, int pageIndex, int pageSize) =>
        GetPage(pageIndex, pageSize, x => x.Title.Contains(title));

    /// <summary>
    /// 更新 Plot 标题
    /// </summary>
    /// <param name="id">Plot ID</param>
    /// <param name="newTitle">新标题</param>
    /// <returns>是否更新成功</returns>
    public bool UpdateTitle(long id, string newTitle) =>
        Update(x => new Plot { Title = newTitle }, x => x.Id == id);

    /// <summary>
    /// 更新 Plot 内容
    /// </summary>
    /// <param name="id">Plot ID</param>
    /// <param name="content">新内容</param>
    /// <returns>是否更新成功</returns>
    public bool UpdateContent(long id, StringBuilder content) =>
        Update(x => new Plot { Content = content }, x => x.Id == id);

    /// <summary>
    /// 获取所有标题
    /// </summary>
    /// <returns>标题列表</returns>
    public List<string> GetAllTitles() =>
        _db.Queryable<Plot>().Select(x => x.Title).ToList();

    /// <summary>
    /// 检查标题是否存在
    /// </summary>
    /// <param name="title">标题</param>
    /// <returns>是否存在</returns>
    public bool TitleExists(string title) =>
        Any(x => x.Title == title);

    #endregion

    #region 异步业务方法

    /// <summary>
    /// 异步根据标题模糊查询 Plot
    /// </summary>
    /// <param name="title">标题关键词</param>
    /// <returns>匹配的 Plot 列表</returns>
    public async Task<List<Plot>> GetByTitleAsync(string title) =>
        await GetWhereAsync(x => x.Title.Contains(title));

    /// <summary>
    /// 异步根据标题精确查询 Plot
    /// </summary>
    /// <param name="title">标题</param>
    /// <returns>匹配的 Plot</returns>
    public async Task<Plot> GetByTitleExactAsync(string title) =>
        await FirstOrDefaultAsync(x => x.Title == title);

    /// <summary>
    /// 异步更新 Plot 标题
    /// </summary>
    /// <param name="id">Plot ID</param>
    /// <param name="newTitle">新标题</param>
    /// <returns>是否更新成功</returns>
    public async Task<bool> UpdateTitleAsync(long id, string newTitle) =>
        await UpdateAsync(x => new Plot { Title = newTitle }, x => x.Id == id);

    /// <summary>
    /// 异步更新 Plot 内容
    /// </summary>
    /// <param name="id">Plot ID</param>
    /// <param name="content">新内容</param>
    /// <returns>是否更新成功</returns>
    public async Task<bool> UpdateContentAsync(long id, StringBuilder content) =>
        await UpdateAsync(x => new Plot { Content = content }, x => x.Id == id);

    /// <summary>
    /// 异步获取所有标题
    /// </summary>
    /// <returns>标题列表</returns>
    public async Task<List<string>> GetAllTitlesAsync() =>
        await _db.Queryable<Plot>().Select(x => x.Title).ToListAsync();

    #endregion
}
