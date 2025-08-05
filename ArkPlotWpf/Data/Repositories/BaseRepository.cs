using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SqlSugar;

namespace ArkPlotWpf.Data.Repositories;

/// <summary>
/// 基础仓储实现类，提供通用的 CRUD 操作
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public class BaseRepository<T> : IBaseRepository<T> where T : class, new()
{
    protected readonly SqlSugarClient _db;

    public BaseRepository(SqlSugarClient? db = null)
    {
        _db = db ?? DatabaseContext.GetDb();
    }

    #region 同步操作

    public int Add(T entity) => _db.Insertable(entity).ExecuteCommand();

    public int AddRange(IEnumerable<T> entities) => _db.Insertable(entities.ToList()).ExecuteCommand();

    public bool Delete(Expression<Func<T, bool>> where) => _db.Deleteable<T>().Where(where).ExecuteCommand() > 0;

    public bool DeleteById(dynamic id) => _db.Deleteable<T>().In(id).ExecuteCommand() > 0;

    public bool DeleteByIds(dynamic[] ids) => _db.Deleteable<T>().In(ids).ExecuteCommand() > 0;

    public bool Update(T entity) => _db.Updateable(entity).ExecuteCommand() > 0;

    public bool UpdateRange(IEnumerable<T> entities) => _db.Updateable(entities.ToList()).ExecuteCommand() > 0;

    public bool Update(Expression<Func<T, T>> set, Expression<Func<T, bool>> where) =>
        _db.Updateable<T>().SetColumns(set).Where(where).ExecuteCommand() > 0;

    public T GetById(dynamic id) => _db.Queryable<T>().InSingle(id);

    public List<T> GetAll() => _db.Queryable<T>().ToList();

    public List<T> GetWhere(Expression<Func<T, bool>> where) => _db.Queryable<T>().Where(where).ToList();

    public T FirstOrDefault(Expression<Func<T, bool>> where) => _db.Queryable<T>().First(where);

    public bool Any(Expression<Func<T, bool>> where) => _db.Queryable<T>().Any(where);

    public int Count(Expression<Func<T, bool>>? where = null) =>
        where == null ? _db.Queryable<T>().Count() : _db.Queryable<T>().Where(where).Count();

    public (List<T>, int) GetPage(int pageIndex, int pageSize, Expression<Func<T, bool>>? where = null)
    {
        var query = _db.Queryable<T>();
        if (where != null)
        {
            query = query.Where(where);
        }

        var total = query.Count();
        var list = query.ToPageList(pageIndex, pageSize);
        return (list, total);
    }

    public bool UseTransaction(Action action)
    {
        try
        {
            _db.Ado.UseTran(action);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"事务执行失败: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region 异步操作

    public async Task<int> AddAsync(T entity) => await _db.Insertable(entity).ExecuteCommandAsync();

    public async Task<int> AddRangeAsync(IEnumerable<T> entities) => await _db.Insertable(entities.ToList()).ExecuteCommandAsync();

    public async Task<bool> DeleteAsync(Expression<Func<T, bool>> where) => await _db.Deleteable<T>().Where(where).ExecuteCommandAsync() > 0;

    public async Task<bool> UpdateAsync(T entity) => await _db.Updateable(entity).ExecuteCommandAsync() > 0;

    public async Task<bool> UpdateAsync(Expression<Func<T, T>> set, Expression<Func<T, bool>> where) =>
        await _db.Updateable<T>().SetColumns(set).Where(where).ExecuteCommandAsync() > 0;

    public async Task<T> GetByIdAsync(dynamic id) => await _db.Queryable<T>().InSingleAsync(id);

    public async Task<List<T>> GetAllAsync() => await _db.Queryable<T>().ToListAsync();

    public async Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> where) => await _db.Queryable<T>().Where(where).ToListAsync();

    public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> where) => await _db.Queryable<T>().FirstAsync(where);

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> where) => await _db.Queryable<T>().AnyAsync(where);

    public async Task<int> CountAsync(Expression<Func<T, bool>>? where = null) =>
        where == null ? await _db.Queryable<T>().CountAsync() : await _db.Queryable<T>().Where(where).CountAsync();

    public async Task<(List<T>, int)> GetPageAsync(int pageIndex, int pageSize, Expression<Func<T, bool>>? where = null)
    {
        var query = _db.Queryable<T>();
        if (where != null)
        {
            query = query.Where(where);
        }
        
        var total = await query.CountAsync();
        var list = await query.ToPageListAsync(pageIndex, pageSize);
        return (list, total);
    }

    public async Task<bool> UseTransactionAsync(Func<Task> action)
    {
        try
        {
            await _db.Ado.UseTranAsync(action);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异步事务执行失败: {ex.Message}");
            return false;
        }
    }

    #endregion
}
