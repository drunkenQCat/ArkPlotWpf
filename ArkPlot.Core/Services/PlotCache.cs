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
    /// 将章节写入缓存（upsert）。
    /// status=1 表示仅下载未解析，status=2 表示解析完成可直接使用。
    /// 当 StoryChapterId > 0 时按 (ActId, StoryChapterId) 匹配现有记录，
    /// 有则更新 Status + 替换 FormattedTextEntry，无则 INSERT。
    /// StoryChapterId = 0 时保持旧 INSERT 行为（兼容旧数据路径）。
    /// </summary>
    public static async Task SaveAsync(Plot plot, List<FormattedTextEntry> entries, int status = 2, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();
        plot.Status = status;

        if (plot.StoryChapterId > 0)
        {
            // upsert 路径：按 (ActId, StoryChapterId) 查找已有记录
            var existing = await db.Queryable<Plot>()
                .FirstAsync(p => p.ActId == plot.ActId && p.StoryChapterId == plot.StoryChapterId);

            if (existing != null)
            {
                // 删除旧 FormattedTextEntry
                await db.Deleteable<FormattedTextEntry>()
                    .Where(e => e.PlotId == existing.Id).ExecuteCommandAsync();

                // 更新 Plot Status（Title 为 init-only，且同一章节 Title 恒定不变）
                existing.Status = status;
                await db.Updateable(existing).ExecuteCommandAsync();

                // 插入新条目，关联到现有 Plot.Id
                foreach (var entry in entries)
                    entry.PlotId = existing.Id;
                await db.Insertable(entries).ExecuteCommandAsync();
                return;
            }
        }

        // 回退路径：全新 INSERT（StoryChapterId = 0，或首次写入新章节）
        var plotId = db.Insertable(plot).ExecuteReturnIdentity();
        foreach (var entry in entries)
            entry.PlotId = plotId;
        await db.Insertable(entries).ExecuteCommandAsync();
    }
}
