using ArkPlotWpf.Data.Entities;
using ArkPlotWpf.Model;
using System;

namespace ArkPlotWpf.Data.Mappers;

/// <summary>
/// Mapper工厂类，用于创建和管理Mapper实例
/// </summary>
public static class MapperFactory
{
    private static readonly Dictionary<Type, object> _mappers = new();

    /// <summary>
    /// 创建PrtsDataMapper实例
    /// </summary>
    /// <returns>PrtsDataMapper实例</returns>
    public static IMapper<PrtsData, PrtsDataEntity> CreatePrtsDataMapper()
    {
        var mapperType = typeof(PrtsDataMapper);
        
        if (!_mappers.ContainsKey(mapperType))
        {
            _mappers[mapperType] = new PrtsDataMapper();
        }
        
        return (IMapper<PrtsData, PrtsDataEntity>)_mappers[mapperType];
    }

    /// <summary>
    /// 获取指定类型的Mapper实例
    /// </summary>
    /// <typeparam name="TMapper">Mapper类型</typeparam>
    /// <returns>Mapper实例</returns>
    public static TMapper GetMapper<TMapper>() where TMapper : class, new()
    {
        var mapperType = typeof(TMapper);
        
        if (!_mappers.ContainsKey(mapperType))
        {
            _mappers[mapperType] = new TMapper();
        }
        
        return (TMapper)_mappers[mapperType];
    }

    /// <summary>
    /// 清除所有缓存的Mapper实例
    /// </summary>
    public static void ClearCache()
    {
        _mappers.Clear();
    }

    /// <summary>
    /// 移除指定类型的Mapper实例
    /// </summary>
    /// <typeparam name="TMapper">Mapper类型</typeparam>
    public static void RemoveMapper<TMapper>()
    {
        var mapperType = typeof(TMapper);
        _mappers.Remove(mapperType);
    }
} 