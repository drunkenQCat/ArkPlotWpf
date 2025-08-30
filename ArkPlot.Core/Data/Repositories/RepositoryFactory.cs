using System;
using System.Threading.Tasks;
using SqlSugar;

namespace ArkPlot.Core.Data.Repositories;

/// <summary>
/// 仓储工厂类，提供统一的仓储访问入�?
/// </summary>
public static class RepositoryFactory
{
    private static readonly Lazy<PlotRepository> _plotRepository = new(() => new PlotRepository());
    private static readonly Lazy<FormattedTextEntryRepository> _formattedTextEntryRepository = new(() => new FormattedTextEntryRepository());
    private static readonly Lazy<PrtsDataRepository> _prtsDataRepository = new(() => new PrtsDataRepository());

    /// <summary>
    /// 获取 Plot 仓储实例
    /// </summary>
    public static PlotRepository Plot => _plotRepository.Value;

    /// <summary>
    /// 获取 FormattedTextEntry 仓储实例
    /// </summary>
    public static FormattedTextEntryRepository FormattedTextEntry => _formattedTextEntryRepository.Value;

    /// <summary>
    /// 获取 PrtsData 仓储实例
    /// </summary>
    public static PrtsDataRepository PrtsData => _prtsDataRepository.Value;

    /// <summary>
    /// 获取数据库连接实�?
    /// </summary>
    public static SqlSugarClient Db => DatabaseContext.GetDb();

    /// <summary>
    /// 执行事务操作
    /// </summary>
    /// <param name="action">事务操作</param>
    /// <returns>是否执行成功</returns>
    public static bool UseTransaction(Action action)
    {
        try
        {
            Db.Ado.UseTran(action);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"事务执行失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 异步执行事务操作
    /// </summary>
    /// <param name="action">事务操作</param>
    /// <returns>是否执行成功</returns>
    public static async Task<bool> UseTransactionAsync(Func<Task> action)
    {
        try
        {
            await Db.Ado.UseTranAsync(action);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异步事务执行失败: {ex.Message}");
            return false;
        }
    }
}
