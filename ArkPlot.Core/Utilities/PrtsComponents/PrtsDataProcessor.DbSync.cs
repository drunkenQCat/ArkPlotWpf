using System.IO;
using System.Text.Json;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using SqlSugar;

namespace ArkPlot.Core.Utilities.PrtsComponents;

/// <summary>
/// DB 同步逻辑 partial：SHA 门控 → 下载落库 / 从库加载
/// </summary>
public partial class PrtsDataProcessor
{
    /// <summary>
    /// 入口：检查 PrtsResources 是否有数据 → 有则从 DB 加载，无则下载后落库。
    /// 何时下载由外部调用方根据 commit SHA 决定，本方法只负责执行。
    /// </summary>
    public async Task EnsureSyncedAsync(string lang = "zh_CN")
    {
        var db = DbFactory.GetClient();
        if (db.Queryable<PrtsResource>().Any())
        {
            await LoadFromDbAsync(db);
            return;
        }

        await GetAllData();
        await SaveToDbAsync(db, lang);
    }

    /// <summary>
    /// 将 PrtsAssets 的内存数据写入 DB。
    /// 先清该语言的旧数据再写入。
    /// </summary>
    private async Task SaveToDbAsync(SqlSugarClient db, string lang)
    {
        // Data_Image / Data_Char / Data_Audio → PrtsResources
        db.Deleteable<PrtsResource>()
            .Where(r => r.ResourceType == "Image" || r.ResourceType == "Char" || r.ResourceType == "Audio")
            .ExecuteCommand();

        var resources = new List<PrtsResource>();
        foreach (var kv in Res.DataImage)
            resources.Add(new PrtsResource { ResourceType = "Image", ResourceKey = kv.Key, ResourceUrl = kv.Value });
        foreach (var kv in Res.DataChar)
            resources.Add(new PrtsResource { ResourceType = "Char", ResourceKey = kv.Key, ResourceUrl = kv.Value });
        foreach (var kv in Res.DataAudio)
            resources.Add(new PrtsResource { ResourceType = "Audio", ResourceKey = kv.Key, ResourceUrl = kv.Value });

        await db.Insertable(resources).ExecuteCommandAsync();

        // Data_Link → PrtsPortraitLinks
        db.Deleteable<PrtsPortraitLink>().ExecuteCommand();

        var links = new List<PrtsPortraitLink>();
        foreach (var entry in Res.PortraitLinkDocument.RootElement.EnumerateObject())
        {
            var charCode = entry.Name;
            var array = entry.Value.GetProperty("array");
            var idx = 0;
            foreach (var item in array.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? "";
                var alias = item.TryGetProperty("alias", out var a) ? a.GetString() : null;
                links.Add(new PrtsPortraitLink
                {
                    CharacterCode = charCode,
                    PortraitName = name,
                    Alias = alias,
                    SortOrder = idx
                });
                idx++;
            }
        }
        await db.Insertable(links).ExecuteCommandAsync();
    }

    /// <summary>
    /// 从 DB 读取数据，重建 PrtsAssets.Instance 的内存结构。
    /// </summary>
    private async Task LoadFromDbAsync(SqlSugarClient db)
    {
        // PrtsResources → DataImage / DataChar / DataAudio
        var resources = await db.Queryable<PrtsResource>().ToListAsync();

        Res.DataImage = ToStringDict(resources.Where(r => r.ResourceType == "Image"));
        Res.DataChar = ToStringDict(resources.Where(r => r.ResourceType == "Char"));
        Res.DataAudio = ToStringDict(resources.Where(r => r.ResourceType == "Audio"));

        // 同步更新 AllData
        Res.AllData[0].Data = Res.DataImage;
        Res.AllData[1].Data = Res.DataChar;
        Res.AllData[2].Data = Res.DataAudio;

        // PrtsPortraitLinks → PortraitLinkDocument
        var links = await db.Queryable<PrtsPortraitLink>().ToListAsync();
        var grouped = links.GroupBy(l => l.CharacterCode);
        using var ms = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteStartObject();
        foreach (var group in grouped)
        {
            writer.WriteStartObject(group.Key);
            writer.WriteStartArray("array");
            foreach (var link in group.OrderBy(l => l.SortOrder))
            {
                writer.WriteStartObject();
                writer.WriteString("name", link.PortraitName);
                if (!string.IsNullOrEmpty(link.Alias))
                    writer.WriteString("alias", link.Alias);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        writer.Flush();
        ms.Position = 0;
        Res.PortraitLinkDocument = JsonDocument.Parse(ms);

        // Data_Override：不存库，保留空文档（运行时 PrtsPreloader 降级处理）
        Res.DataOverrideDocument = JsonDocument.Parse("{}");
        Res.RideItems.Clear();
    }

    private static StringDict ToStringDict(IEnumerable<PrtsResource> items)
    {
        var dict = new StringDict();
        foreach (var item in items)
            dict[item.ResourceKey] = item.ResourceUrl;
        return dict;
    }
}
