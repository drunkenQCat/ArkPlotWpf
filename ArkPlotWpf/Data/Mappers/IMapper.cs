namespace ArkPlotWpf.Data.Mappers;

/// <summary>
/// 通用Mapper接口，定义模型和实体之间的转换方法
/// </summary>
/// <typeparam name="TModel">模型类型</typeparam>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface IMapper<TModel, TEntity>
{
    /// <summary>
    /// 将模型转换为实体
    /// </summary>
    /// <param name="model">要转换的模型</param>
    /// <returns>转换后的实体</returns>
    TEntity ToEntity(TModel model);

    /// <summary>
    /// 将实体转换为模型
    /// </summary>
    /// <param name="entity">要转换的实体</param>
    /// <returns>转换后的模型</returns>
    TModel ToModel(TEntity entity);
} 