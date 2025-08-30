using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ArkPlot.Core.Data.Repositories;

/// <summary>
/// 基础仓储接口，定义通用�?CRUD 操作
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface IBaseRepository<T> where T : class, new()
{
    #region 同步操作

    /// <summary>
    /// 添加单个实体
    /// </summary>
    /// <param name="entity">要添加的实体</param>
    /// <returns>影响的行�?/returns>
    int Add(T entity);

    /// <summary>
    /// 批量添加实体
    /// </summary>
    /// <param name="entities">要添加的实体集合</param>
    /// <returns>影响的行�?/returns>
    int AddRange(IEnumerable<T> entities);

    /// <summary>
    /// 根据条件删除实体
    /// </summary>
    /// <param name="where">删除条件</param>
    /// <returns>是否删除成功</returns>
    bool Delete(Expression<Func<T, bool>> where);

    /// <summary>
    /// 根据主键删除实体
    /// </summary>
    /// <param name="id">主键�?/param>
    /// <returns>是否删除成功</returns>
    bool DeleteById(dynamic id);

    /// <summary>
    /// 根据主键批量删除实体
    /// </summary>
    /// <param name="ids">主键值数�?/param>
    /// <returns>是否删除成功</returns>
    bool DeleteByIds(dynamic[] ids);

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entity">要更新的实体</param>
    /// <returns>是否更新成功</returns>
    bool Update(T entity);

    /// <summary>
    /// 批量更新实体
    /// </summary>
    /// <param name="entities">要更新的实体集合</param>
    /// <returns>是否更新成功</returns>
    bool UpdateRange(IEnumerable<T> entities);

    /// <summary>
    /// 根据条件更新实体
    /// </summary>
    /// <param name="set">更新表达�?/param>
    /// <param name="where">更新条件</param>
    /// <returns>是否更新成功</returns>
    bool Update(Expression<Func<T, T>> set, Expression<Func<T, bool>> where);

    /// <summary>
    /// 根据主键获取实体
    /// </summary>
    /// <param name="id">主键�?/param>
    /// <returns>实体对象</returns>
    T GetById(dynamic id);

    /// <summary>
    /// 获取所有实�?
    /// </summary>
    /// <returns>实体列表</returns>
    List<T> GetAll();

    /// <summary>
    /// 根据条件查询实体
    /// </summary>
    /// <param name="where">查询条件</param>
    /// <returns>实体列表</returns>
    List<T> GetWhere(Expression<Func<T, bool>> where);

    /// <summary>
    /// 根据条件获取第一个实�?
    /// </summary>
    /// <param name="where">查询条件</param>
    /// <returns>实体对象，如果不存在则返�?null</returns>
    T FirstOrDefault(Expression<Func<T, bool>> where);

    /// <summary>
    /// 检查是否存在满足条件的实体
    /// </summary>
    /// <param name="where">查询条件</param>
    /// <returns>是否存在</returns>
    bool Any(Expression<Func<T, bool>> where);

    /// <summary>
    /// 统计满足条件的实体数�?
    /// </summary>
    /// <param name="where">查询条件</param>
    /// <returns>实体数量</returns>
    int Count(Expression<Func<T, bool>>? where = null);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="where">查询条件</param>
    /// <returns>(实体列表, 总数�?</returns>
    (List<T>, int) GetPage(int pageIndex, int pageSize, Expression<Func<T, bool>>? where = null);

    /// <summary>
    /// 执行事务操作
    /// </summary>
    /// <param name="action">事务操作</param>
    /// <returns>是否执行成功</returns>
    bool UseTransaction(Action action);

    #endregion

    #region 异步操作

    /// <summary>
    /// 异步添加单个实体
    /// </summary>
    /// <param name="entity">要添加的实体</param>
    /// <returns>影响的行�?/returns>
    Task<int> AddAsync(T entity);

    /// <summary>
    /// 异步批量添加实体
    /// </summary>
    /// <param name="entities">要添加的实体集合</param>
    /// <returns>影响的行�?/returns>
    Task<int> AddRangeAsync(IEnumerable<T> entities);

    /// <summary>
    /// 异步根据条件删除实体
    /// </summary>
    /// <param name="where">删除条件</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteAsync(Expression<Func<T, bool>> where);

    /// <summary>
    /// 异步更新实体
    /// </summary>
    /// <param name="entity">要更新的实体</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateAsync(T entity);

    /// <summary>
    /// 异步根据条件更新实体
    /// </summary>
    /// <param name="set">更新表达�?/param>
    /// <param name="where">更新条件</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateAsync(Expression<Func<T, T>> set, Expression<Func<T, bool>> where);

    /// <summary>
    /// 异步根据主键获取实体
    /// </summary>
    /// <param name="id">主键�?/param>
    /// <returns>实体对象</returns>
    Task<T> GetByIdAsync(dynamic id);

    /// <summary>
    /// 异步获取所有实�?
    /// </summary>
    /// <returns>实体列表</returns>
    Task<List<T>> GetAllAsync();

    /// <summary>
    /// 异步根据条件查询实体
    /// </summary>
    /// <param name="where">查询条件</param>
    /// <returns>实体列表</returns>
    Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> where);

    /// <summary>
    /// 异步根据条件获取第一个实�?
    /// </summary>
    /// <param name="where">查询条件</param>
    /// <returns>实体对象，如果不存在则返�?null</returns>
    Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> where);

    /// <summary>
    /// 异步检查是否存在满足条件的实体
    /// </summary>
    /// <param name="where">查询条件</param>
    /// <returns>是否存在</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> where);

    /// <summary>
    /// 异步统计满足条件的实体数�?
    /// </summary>
    /// <param name="where">查询条件</param>
    /// <returns>实体数量</returns>
    Task<int> CountAsync(Expression<Func<T, bool>>? where = null);

    /// <summary>
    /// 异步分页查询
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="where">查询条件</param>
    /// <returns>(实体列表, 总数�?</returns>
    Task<(List<T>, int)> GetPageAsync(int pageIndex, int pageSize, Expression<Func<T, bool>>? where = null);

    /// <summary>
    /// 异步执行事务操作
    /// </summary>
    /// <param name="action">事务操作</param>
    /// <returns>是否执行成功</returns>
    Task<bool> UseTransactionAsync(Func<Task> action);

    #endregion
}
