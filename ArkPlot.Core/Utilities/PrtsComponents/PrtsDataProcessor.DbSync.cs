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
    /// 入口：根据 GitHub commit SHA 判断是否需要刷新 PRTS 数据。
    /// SHA 相同且 DB 有数据 → 从 DB 加载；SHA 变化或首次 → 重新下载并落库。
    /// </summary>
    public async Task EnsureSyncedAsync(string lang = "zh_CN", string? commitSha = null)
    {
        var db = DbFactory.GetClient();
        var hasData = db.Queryable<PrtsResource>().Any();

        if (hasData && commitSha != null)
        {
            var storedSha = new StorySyncService().GetSyncState(lang)?.PrtsSyncedAtSha;
            if (commitSha == storedSha)
            {
                await LoadFromDbAsync(db);
                return;
            }
            // SHA 变了 → 重新下载
        }
        else if (hasData && commitSha == null)
        {
            // 未提供 SHA，兼容旧行为：有数据就加载
            await LoadFromDbAsync(db);
            return;
        }

        await GetAllData();
        await SaveToDbAsync(db, lang);

        // 记录 PRTS 同步时的 SHA
        if (commitSha != null)
            new StorySyncService().UpdatePrtsSyncSha(lang, commitSha);
    }

    /// <summary>
    /// 强制刷新：无视 SHA 门控，重新从 PRTS Wiki 下载全部数据并覆盖写入 DB。
    /// </summary>
    public async Task ForceRefreshAsync(string lang = "zh_CN")
    {
        var db = DbFactory.GetClient();
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

        // Data_Override → PrtsData 表
        db.Deleteable<PrtsData>().Where(d => d.Tag == "Data_Override").ExecuteCommand();
        var overrideJson = JsonSerializer.Serialize(Res.RideItems);
        await db.Insertable(new PrtsData
        {
            Tag = "Data_Override",
            Data = new StringDict { ["json"] = overrideJson }
        }).ExecuteCommandAsync();
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

        // Data_Override → RideItems + DataOverrideDocument
        var overrideRow = await db.Queryable<PrtsData>().FirstAsync(d => d.Tag == "Data_Override");
        if (overrideRow?.Data != null && overrideRow.Data.ContainsKey("json"))
        {
            var json = overrideRow.Data["json"];
            Res.RideItems = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json)
                ?? new Dictionary<string, Dictionary<string, object>>();
            Res.DataOverrideDocument = JsonDocument.Parse(json);
        }
        else
        {
            Res.DataOverrideDocument = JsonDocument.Parse("{}");
            Res.RideItems.Clear();
        }
    }

    private static StringDict ToStringDict(IEnumerable<PrtsResource> items)
    {
        var dict = new StringDict();
        foreach (var item in items)
            dict[item.ResourceKey] = item.ResourceUrl;
        return dict;
    }
}
