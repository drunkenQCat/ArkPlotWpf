using ArkPlotWpf.Data.Entities;
using ArkPlotWpf.Model;
using System.Text.Json;

namespace ArkPlotWpf.Data.Mappers;

public class PrtsDataMapper : IMapper<PrtsData, PrtsDataEntity>
{
    public PrtsDataEntity ToEntity(PrtsData model)
    {
        if (model == null)
            return null!;
            
        return new PrtsDataEntity
        {
            Tag = model.Tag,
            DataJson = JsonSerializer.Serialize(model.Data, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            })
        };
    }

    public PrtsData ToModel(PrtsDataEntity entity)
    {
        if (entity == null)
            return null!;
            
        StringDict data;
        
        try
        {
            if (string.IsNullOrEmpty(entity.DataJson))
            {
                data = new StringDict();
            }
            else
            {
                data = JsonSerializer.Deserialize<StringDict>(entity.DataJson) ?? new StringDict();
            }
        }
        catch (JsonException)
        {
            // 如果JSON解析失败，返回空的StringDict
            data = new StringDict();
        }
        
        return new PrtsData(entity.Tag, data);
    }

}

// 静态扩展方法类，保持向后兼容性
public static class PrtsDataMapperExtensions
{
    public static PrtsDataEntity ToEntity(this PrtsData model)
    {
        var mapper = new PrtsDataMapper();
        return mapper.ToEntity(model);
    }

    public static PrtsData ToModel(this PrtsDataEntity entity)
    {
        var mapper = new PrtsDataMapper();
        return mapper.ToModel(entity);
    }
} 