namespace ArkPlotWpf.Data.Repositories;

/// <summary>
/// 通用Repository接口，定义基本的CRUD操作
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IRepository<TEntity, TKey>
{
    /// <summary>
    /// 根据主键获取实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <returns>实体，如果不存在则返回null</returns>
    TEntity? GetById(TKey id);

    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <returns>所有实体的列表</returns>
    IEnumerable<TEntity> GetAll();

    /// <summary>
    /// 添加实体
    /// </summary>
    /// <param name="entity">要添加的实体</param>
    /// <returns>新实体的主键</returns>
    TKey Add(TEntity entity);

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entity">要更新的实体</param>
    /// <returns>是否更新成功</returns>
    bool Update(TEntity entity);

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="id">要删除的实体主键</param>
    /// <returns>是否删除成功</returns>
    bool Delete(TKey id);

    /// <summary>
    /// 添加或更新实体
    /// </summary>
    /// <param name="entity">要添加或更新的实体</param>
    void AddOrUpdate(TEntity entity);
} 