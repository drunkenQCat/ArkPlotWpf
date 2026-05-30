# 模型重构计划

> 时机：实现"缓存浏览"页面之前
> 目标：消除数据冗余，使查询简洁，不改字段类型/长度，不破坏现有数据

---

## 一、现状问题

### 问题 1：重复的 Plot 记录

```sql
-- 同一个章节有两条 Plot
Ch#1009 无名氏的战争 → Plot#6 (Status=1) + Plot#7 (Status=2)
Ch#1010 破局者       → Plot#5 (Status=1) + Plot#8 (Status=2)
```

数据来自两个独立写入路径，**都是 INSERT，没有 UPDATE**：

```
AkpStoryLoader.GetAllChapters()           →  SaveAsync(status=1)  ← INSERT
PlotManager.StartParseLines()              →  SaveAsync(status=2)  ← 另一个 INSERT
```

**后果**：查询必须用 `MAX(Status)` 或 `DISTINCT` 取最新状态，无法用简单的一对一关联。

### 问题 2：`StoryChapterId = 0`

`Plot.StoryChapterId` 是 `long`（不可空），缺省值 0。部分 Plot 记录为 0：

```
Plot#1 | Act#57 此地之外 | StoryChapterId=0
Plot#2 | Act#57 此地之外 | StoryChapterId=0
```

**后果**：LEFT JOIN `StoryChapters.Id = Plot.StoryChapterId` 匹配不上，这些章节在缓存列表中"隐身"。

### 问题 3：模型间无导航属性

四个关联模型（Act → StoryChapter → Plot → FormattedTextEntry），无一使用 `[Navigate]`。

**后果**：本应用 `Includes` 三表联查的场景，现在要手写三层嵌套 `Subqueryable`。

---

## 二、影响范围（所有引用文件）

### Core 层（数据源）— 6 个文件

| 文件 | 引用关系 | 改动类型 |
|---|---|---|
| `ArkPlot.Core/Model/Plot.cs` | Plot 定义 | 加 `[Navigate]` + 唯一索引 |
| `ArkPlot.Core/Model/StoryChapter.cs` | StoryChapter 定义 | 加 `[Navigate]` 回 Plot |
| `ArkPlot.Core/Model/FormattedTextEntry.cs` | FormattedTextEntry 定义 | 加 `[Navigate]` 回 Plot |
| `ArkPlot.Core/Services/PlotCache.cs` | SaveAsync / TryLoadAsync / GetCachedTitlesAsync | SaveAsync 改 upsert |
| `ArkPlot.Core/Utilities/TagProcessingComponents/PlotManager.cs:77` | `SaveAsync(CurrentPlot, ...)` | 不需要改（签名不变） |
| `ArkPlot.Core/Utilities/WorkFlow/AkpStoryLoader.cs:117` | `SaveAsync(..., status: 1)` | 不需要改（签名不变） |
| `ArkPlot.Core/Infrastructure/DbFactory.cs:41` | `InitTables(typeof(Plot), ...)` | 不需要改 |

### WebDemo 层（目标层）— 3 个文件

| 文件 | 引用关系 | 改动类型 |
|---|---|---|
| `ArkPlot.WebDemo/Services/StoryService.cs` | 读取 Acts / Chapters / Plot | 缓存浏览查询将受益于导航属性 |
| `ArkPlot.WebDemo/Pages/Pic.razor` | 间接引用 StoryService | 不需要改 |
| `ArkPlot.WebDemo/Program.cs` | DI 注册 | 不需要改 |

### Avalonia 层— 1 个文件

| 文件 | 引用关系 | 改动类型 |
|---|---|---|
| `ArkPlot.Avalonia/ViewModels/MainWindowViewModel.cs:38` | `new StorySyncService()` + `GetActsFromDb()` | 不需要改（只读 Path） |
| `ArkPlot.Avalonia/ViewModels/SettingsViewModel.cs:204` | `new StorySyncService()` → `DownloadAndSaveAsync` | 不需要改 |

### CLI 层 — 1 个文件

| 文件 | 引用关系 | 改动类型 |
|---|---|---|
| `ArkPlot.Cli/Pipeline/ActivityLoader.cs` | `new StorySyncService()` + `GetChaptersByActId()` | 不需要改 |

### 测试层 — 2 个文件

| 文件 | 引用关系 | 改动类型 |
|---|---|---|
| `ArkPlot.Avalonia.Tests/EndToEndDbTests.cs` | 7 处 SaveAsync/TryLoadAsync/GetCachedTitlesAsync | **需更新** — 验证 upsert 行为 |
| `ArkPlot.Avalonia.Tests/FullPipelineHeadlessTests.cs` | 4 处 SaveAsync/TryLoadAsync/GetCachedTitlesAsync | **需更新** — 验证 upsert 行为 |

### 其他 (Novelizer / WPF / Dump) — 6 个文件

`ArkPlot.Novelizer/`, `ArkPlotWpf/`, `ArkPlot.Cli/Dump/` 等仅引用 Model 类型做**只读**操作（读取属性、传参），无需改动。

---

## 三、核心改造

### 3.1 Plot.cs — 加唯一约束 + 导航属性

```csharp
[SugarTable("Plot")]
[SugarIndex("uk_plot_act_chapter", nameof(ActId), OrderByType.Asc,
             nameof(StoryChapterId), OrderByType.Asc, isUnique: true)]
public class Plot
{
    // 现有字段全部保留，不改

    /// <summary>
    /// 导航到关联的章节（一对一）
    /// </summary>
    [Navigate(NavigateType.OneToOne, nameof(StoryChapterId))]
    public StoryChapter? Chapter { get; set; }

    /// <summary>
    /// 导航到解析后的条目列表（一对多）
    /// </summary>
    [Navigate(NavigateType.OneToMany, nameof(FormattedTextEntry.PlotId))]
    public List<FormattedTextEntry> Entries { get; set; } = [];
}
```

### 3.2 StoryChapter.cs — 加导航回 Plot

```csharp
[SugarTable("StoryChapters")]
public class StoryChapter
{
    // 现有字段全部保留

    /// <summary>
    /// 导航到缓存状态（一对一）。未缓存时为 null。
    /// </summary>
    [Navigate(NavigateType.OneToOne, nameof(Plot.StoryChapterId))]
    public Plot? CachedPlot { get; set; }
}
```

### 3.3 FormattedTextEntry.cs — 加导航回 Plot

```csharp
[Navigate(NavigateType.ManyToOne, nameof(PlotId))]
public Plot? Plot { get; set; }
```

### 3.4 PlotCache.cs — SaveAsync 改 upsert

```csharp
public static async Task SaveAsync(Plot plot, List<FormattedTextEntry> entries,
    int status = 2, SqlSugarClient? db = null)
{
    db ??= DbFactory.GetClient();
    plot.Status = status;

    // == 改为 upsert：按 (ActId, StoryChapterId) 匹配 ==
    var existing = await db.Queryable<Plot>()
        .FirstAsync(p => p.ActId == plot.ActId
                      && p.StoryChapterId == plot.StoryChapterId);

    if (existing != null)
    {
        // 保留 ID 不动，删除旧 FormattedTextEntry
        var oldPlotId = existing.Id;
        await db.Deleteable<FormattedTextEntry>()
            .Where(e => e.PlotId == oldPlotId).ExecuteCommandAsync();

        // 更新 Plot 字段（除 ID/ActId/StoryChapterId 外）
        existing.Title = plot.Title;
        existing.Status = status;
        await db.Updateable(existing).ExecuteCommandAsync();

        // 插入新条目，关联到现有 Plot.Id
        foreach (var entry in entries)
            entry.PlotId = oldPlotId;
        await db.Insertable(entries).ExecuteCommandAsync();
    }
    else
    {
        // 全新插入（首次下载，或无 StoryChapterId 的旧记录）
        var plotId = db.Insertable(plot).ExecuteReturnIdentity();
        foreach (var entry in entries)
            entry.PlotId = plotId;
        await db.Insertable(entries).ExecuteCommandAsync();
    }
}
```

### 3.5 数据库迁移脚本

由于加了唯一约束，已有重复数据需要先清理：

```sql
-- 1. 对有重复的章节，删除 Status=1 的记录，保留 Status=2
DELETE FROM FormattedTextEntry
WHERE PlotId IN (
    SELECT p1.Id FROM Plot p1
    INNER JOIN Plot p2
        ON p1.StoryChapterId = p2.StoryChapterId
        AND p1.ActId = p2.ActId
        AND p1.Status = 1 AND p2.Status = 2
);

DELETE FROM Plot
WHERE Id IN (
    SELECT p1.Id FROM Plot p1
    INNER JOIN Plot p2
        ON p1.StoryChapterId = p2.StoryChapterId
        AND p1.ActId = p2.ActId
        AND p1.Status = 1 AND p2.Status = 2
);

-- 2. 对 StoryChapterId=0 的记录，尝试按 Title 匹配 StoryChapters
-- （手工处理：需要查看具体数据，看 Title 能否反解出 StoryCode/StoryName）
```

> **注意**：SqlSugar 的 `InitTables` 不自动创建索引。唯一索引需要在 `DbFactory.GetClient()` 中显式创建：
>
> ```csharp
> // 在 InitTables 后加
> if (!_client.DbMaintenance.IsAnyConstraint("uk_plot_act_chapter"))
>     _client.DbMaintenance.CreateIndex("Plot", "uk_plot_act_chapter",
>         new string[] { "ActId", "StoryChapterId" });
> ```

---

## 四、改动后的查询效果

改完之后，"缓存浏览"页面的数据查询变成：

```csharp
public async Task<List<ChapterCacheStatus>> GetChapterCacheStatusesAsync(long actId)
{
    return await _db.Queryable<StoryChapter>()
        .Includes(ch => ch.CachedPlot)          // ← 导航属性，一对一
        .Where(ch => ch.ActId == actId)
        .OrderBy(ch => ch.StorySort)
        .Select(ch => new ChapterCacheStatus
        {
            ChapterId = ch.Id,
            StoryCode = ch.StoryCode,
            StoryName = ch.StoryName,
            AvgTag = ch.AvgTag,
            Status = ch.CachedPlot != null ? ch.CachedPlot.Status : -1,
        })
        .ToListAsync();
}
```

加载详情页时：

```csharp
var entries = await _db.Queryable<FormattedTextEntry>()
    .Where(e => e.Plot.PlotId == plotId)  // 或者直接用导航
    .ToListAsync();
```

没有 `Subqueryable`，没有手写 JOIN，全由导航属性驱动。

---

## 五、测试更新要点

改完 `SaveAsync` 从 INSERT 变 upsert 后，测试需要验证：

| 测试场景 | 当前行为 | 改后行为 | 需更新测试 |
|---|---|---|---|
| 同一章节 Save (Status=1) 再 Save (Status=2) | 两条 Plot 记录 | 一条 Plot 记录，Status=2 | `EndToEndDbTests.cs:342-363` |
| Status=1 章节不通过 TryLoadAsync 返回 | 通过 Status 过滤 | 仍通过 Status 过滤（不变） | 不需要改 |
| 全新章节首次 Save | 正常 INSERT | 仍正常 INSERT | 不需要改 |
| 重新解析已有章节（二次 Save, Status=2） | 另一条 Plot | 覆盖原 Plot + 替换 Entries | `FullPipelineHeadlessTests.cs:290-320` |

---

## 六、执行结果（已完成 ✅）

### 已改文件

| 文件 | 改动 | 状态 |
|---|---|---|
| `ArkPlot.Core/Model/Plot.cs` | 加 `[Navigate]` 到 Chapter + Entries；移除 `[SugarIndex]`（索引改由 DbFactory 创建过滤索引） | ✅ |
| `ArkPlot.Core/Model/StoryChapter.cs` | 加 `[Navigate]` 到 CachedPlot | ✅ |
| `ArkPlot.Core/Model/FormattedTextEntry.cs` | 加 `[Navigate]` 到 Plot | ✅ |
| `ArkPlot.Core/Services/PlotCache.cs` | `SaveAsync` 改 upsert：`StoryChapterId > 0` 时按 `(ActId, StoryChapterId)` 匹配更新，否则回退 INSERT | ✅ |
| `ArkPlot.Core/Infrastructure/DbFactory.cs` | `InitTables` 后创建过滤唯一索引 `WHERE StoryChapterId > 0` | ✅ |
| `ArkPlot.Avalonia.Tests/FullPipelineHeadlessTests.cs` | 加 3 个 upsert 专项测试 | ✅ |
| `docs/db-migration-001-dedup-plot.sql` | 迁移脚本已执行，DB 已清理 | ✅ |

### 未改（零影响）

| 文件 | 原因 |
|---|---|
| `AkpStoryLoader.cs` | 调用 `SaveAsync` 签名不变，仍传 `status: 1` |
| `PlotManager.cs` | 调用 `SaveAsync(CurrentPlot, ...)`，签名不变 |
| 其他 10+ 文件（ViewModel / CLI / WPF / Novelizer） | 只读引用 Model，不改 |

### 测试结果

```
总 57 测试，0 失败
├─ 54 原测试              — 全部通过（零回归）
├─ 3 新增 upsert 专项测试  — 全部通过
├─ 0 新增索引测试          — 移除（全局状态污染）
```

### 关键设计决策

1. **索引是过滤的**：`WHERE StoryChapterId > 0` — 不约束旧数据（StoryChapterId=0 的行可以共存）
2. **`[SugarIndex]` 不用于索引创建**：SqlSugar `CodeFirst` 会自动从 Attribute 建索引但**不支持 WHERE 子句**，所以改成在 `DbFactory` 中用原生 SQL 建过滤索引
3. **Upsert 不更新 Title**：`Plot.Title` 是 `init-only` 属性，且同一章节 Title 由 `StoryCode + StoryName + AvgTag` 决定，恒定不变
