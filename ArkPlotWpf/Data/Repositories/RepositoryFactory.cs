namespace ArkPlotWpf.Data.Repositories;

/// <summary>
/// Repository工厂类，用于创建和管理Repository实例
/// </summary>
public static class RepositoryFactory
{
    private static readonly Dictionary<string, object> _repositories = new();

    /// <summary>
    /// 创建PrtsDataRepository实例
    /// </summary>
    /// <param name="connectionString">数据库连接字符串，如果为null则使用默认连接</param>
    /// <returns>PrtsDataRepository实例</returns>
    public static IPrtsDataRepository CreatePrtsDataRepository(string? connectionString = null)
    {
        var key = $"PrtsData_{connectionString ?? "default"}";
        
        if (!_repositories.ContainsKey(key))
        {
            _repositories[key] = new PrtsDataRepository(connectionString);
        }
        
        return (IPrtsDataRepository)_repositories[key];
    }

    /// <summary>
    /// 创建PrtsAssetsRepository实例
    /// </summary>
    /// <param name="connectionString">数据库连接字符串，如果为null则使用默认连接</param>
    /// <returns>PrtsAssetsRepository实例</returns>
    public static IPrtsAssetsRepository CreatePrtsAssetsRepository(string? connectionString = null)
    {
        var key = $"PrtsAssets_{connectionString ?? "default"}";
        
        if (!_repositories.ContainsKey(key))
        {
            _repositories[key] = new PrtsAssetsRepository(connectionString);
        }
        
        return (IPrtsAssetsRepository)_repositories[key];
    }

    /// <summary>
    /// 清除所有缓存的Repository实例
    /// </summary>
    public static void ClearCache()
    {
        _repositories.Clear();
    }

    /// <summary>
    /// 移除指定的Repository实例
    /// </summary>
    /// <param name="repositoryType">Repository类型</param>
    /// <param name="connectionString">连接字符串</param>
    public static void RemoveRepository(string repositoryType, string? connectionString = null)
    {
        var key = $"{repositoryType}_{connectionString ?? "default"}";
        _repositories.Remove(key);
    }
} 