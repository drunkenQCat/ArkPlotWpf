using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Core.Services;

/// <summary>
/// Plot 缓存层：处理完成的章节可写库复用，避免重复下载和解析。
/// </summary>
public static class PlotCache
{
    /// <summary>
    /// 查询某活动下已缓存的章节标题。
    /// </summary>
    public static async Task<HashSet<string>> GetCachedTitlesAsync(long actId, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();
        var titles = await db.Queryable<Plot>()
            .Where(p => p.ActId == actId && p.Status == 2)
            .Select(p => p.Title)
            .ToListAsync();
        return new HashSet<string>(titles);
    }

    /// <summary>
    /// 尝试从缓存加载一个章节的 FormattedTextEntry。
    /// 返回 (Plot, List{FormattedTextEntry})，缓存未命中则返回 null。
    /// </summary>
    public static async Task<(Plot Plot, List<FormattedTextEntry> Entries)?> TryLoadAsync(long actId, string title, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();
        var plot = await db.Queryable<Plot>()
            .FirstAsync(p => p.ActId == actId && p.Title == title && p.Status == 2);
        if (plot == null) return null;

        var entries = await db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plot.Id)
            .OrderBy(e => e.Index)
            .ToListAsync();

        return (plot, entries);
    }

    /// <summary>
    /// 将章节写入缓存。
    /// status=1 表示仅下载未解析，status=2 表示解析完成可直接使用。
    /// </summary>
    public static async Task SaveAsync(Plot plot, List<FormattedTextEntry> entries, int status = 2, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();
        plot.Status = status;
        var plotId = db.Insertable(plot).ExecuteReturnIdentity();
        foreach (var entry in entries)
            entry.PlotId = plotId;
        await db.Insertable(entries).ExecuteCommandAsync();
    }
}
