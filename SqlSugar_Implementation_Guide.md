# 轻量级 SqlSugar 实现指南 - 业务模型直接兼容

## 核心思路
**不创建实体类，直接使用业务模型**，通过 SqlSugar 的 `EntityService` 和 `Mapper` 实现业务模型与数据库的无缝映射。

## 1. 极简配置

### 1.1 单文件配置

```csharp
// SqlSugarSetup.cs
using SqlSugar;
using System.Text.Json;

namespace ArkPlotWpf.Data;

public static class Db
{
    private static SqlSugarClient _db;
    
    public static SqlSugarClient Instance => _db ??= Create();
    
    private static SqlSugarClient Create()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=arkplot.db",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                SerializeService = new JsonNetSerializer()
            }
        });
        
        // 自动建表（基于业务模型）
        db.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(Model.PrtsData),
            typeof(Model.Plot),
            typeof(Model.FormattedTextEntry)
        );
        
        return db;
    }
}

// JSON 序列化适配器
public class JsonNetSerializer : ISerializeService
{
    public string SerializeObject(object value) => JsonSerializer.Serialize(value);
    public T DeserializeObject<T>(string value) => JsonSerializer.Deserialize<T>(value);
}
```

## 2. 业务模型零改造

### 2.1 添加轻量特性

只需要在现有业务模型上加几个特性，**不改动原有代码逻辑**：

```csharp
// 原业务模型保持不变，只加特性
using SqlSugar;

namespace ArkPlotWpf.Model;

[SugarTable("PrtsData")]
public class PrtsData
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; } // 新增：数据库主键
    
    [SugarColumn(IsNullable = false)]
    public readonly StringDict Data;
    
    [SugarColumn(IsNullable = false, Length = 100)]
    public readonly string Tag;

    // 原有构造函数保持不变
    public PrtsData(string tag)
    {
        Tag = tag;
        Data = new StringDict();
    }

    public PrtsData(string tag, StringDict data)
    {
        Tag = tag;
        Data = data;
    }
}

[SugarTable("Plot")]
public class Plot
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    
    [SugarColumn(Length = 200)]
    public string Title { get; init; }
    
    [SugarColumn(ColumnDataType = "TEXT")]
    public StringBuilder Content { get; init; }
    
    [SugarColumn(IsIgnore = true)] // 忽略复杂类型
    public List<FormattedTextEntry> TextVariants = [];
}

[SugarTable("FormattedTextEntry")]
public class FormattedTextEntry
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    
    public int Index { get; set; }
    
    [SugarColumn(Length = 1000)]
    public string OriginalText { get; set; } = "";
    
    [SugarColumn(Length = 1000)]
    public string MdText { get; set; } = "";
    
    public int MdDuplicateCounter { get; set; }
    
    [SugarColumn(Length = 1000)]
    public string TypText { get; set; } = "";
    
    [SugarColumn(Length = 50)]
    public string Type { get; set; } = "";
    
    [SugarColumn(IsIgnore = true)] // 复杂类型用 JSON 存储
    public StringDict CommandSet { get; set; } = new();
    
    [SugarColumn(ColumnDataType = "TEXT")] // 存储 JSON
    public string CommandSetJson
    {
        get => JsonSerializer.Serialize(CommandSet);
        set => CommandSet = string.IsNullOrEmpty(value) 
            ? new StringDict() 
            : JsonSerializer.Deserialize<StringDict>(value);
    }
}
```

## 3. 极简仓储层

### 3.1 通用仓储（一个类搞定所有）

```csharp
namespace ArkPlotWpf.Data;

public class Repo<T> where T : class, new()
{
    private readonly SqlSugarClient _db = Db.Instance;
    
    // 增
    public int Add(T entity) => _db.Insertable(entity).ExecuteCommand();
    public int AddRange(IEnumerable<T> entities) => _db.Insertable(entities.ToList()).ExecuteCommand();
    
    // 删
    public bool Delete(Expression<Func<T, bool>> where) => _db.Deleteable<T>().Where(where).ExecuteCommand() > 0;
    public bool DeleteById(dynamic id) => _db.Deleteable<T>().In(id).ExecuteCommand() > 0;
    
    // 改
    public bool Update(T entity) => _db.Updateable(entity).ExecuteCommand() > 0;
    public bool Update(Expression<Func<T, T>> set, Expression<Func<T, bool>> where) => 
        _db.Updateable<T>().SetColumns(set).Where(where).ExecuteCommand() > 0;
    
    // 查
    public T GetById(dynamic id) => _db.Queryable<T>().InSingle(id);
    public List<T> GetAll() => _db.Queryable<T>().ToList();
    public List<T> GetWhere(Expression<Func<T, bool>> where) => _db.Queryable<T>().Where(where).ToList();
    public T FirstOrDefault(Expression<Func<T, bool>> where) => _db.Queryable<T>().First(where);
    public bool Any(Expression<Func<T, bool>> where) => _db.Queryable<T>().Any(where);
    
    // 分页
    public (List<T>, int) GetPage(int pageIndex, int pageSize, Expression<Func<T, bool>> where = null) => 
        _db.Queryable<T>()
           .WhereIF(where != null, where)
           .ToPageList(pageIndex, pageSize);
}
```

### 3.2 专用仓储（按需扩展）

```csharp
namespace ArkPlotWpf.Data;

public class PrtsDataRepo : Repo<Model.PrtsData>
{
    public Model.PrtsData GetByTag(string tag) => FirstOrDefault(x => x.Tag == tag);
    
    public bool AddOrUpdate(string tag, StringDict data)
    {
        var existing = GetByTag(tag);
        if (existing != null)
        {
            return Update(
                x => new Model.PrtsData { Data = data }, 
                x => x.Tag == tag
            );
        }
        else
        {
            return Add(new Model.PrtsData(tag, data)) > 0;
        }
    }
    
    public bool DeleteByTag(string tag) => Delete(x => x.Tag == tag);
}

public class PlotRepo : Repo<Model.Plot>
{
    public List<Model.Plot> GetByTitle(string title) => GetWhere(x => x.Title.Contains(title));
}
```

## 4. 实际使用示例

### 4.1 初始化（一行代码）

```csharp
// 程序启动时调用一次
Db.Instance; // 触发初始化
```

### 4.2 CRUD 操作

```csharp
using ArkPlotWpf.Data;
using ArkPlotWpf.Model;

// 直接使用业务模型
var prtsRepo = new PrtsDataRepo();
var plotRepo = new PlotRepo();

// 增
var data = new PrtsData("character_001", new StringDict { {"hp", "100"}, {"atk", "50"} });
prtsRepo.Add(data);

var plot = new Plot("第一章", new StringBuilder("故事开始..."));
plotRepo.Add(plot);

// 查
var charData = prtsRepo.GetByTag("character_001");
var allPlots = plotRepo.GetAll();

// 改
prtsRepo.AddOrUpdate("character_001", new StringDict { {"hp", "150"} });

// 删
prtsRepo.DeleteByTag("character_001");
```

### 4.3 复杂查询

```csharp
// 使用 SqlSugar 的强大查询能力
var db = Db.Instance;

// 直接对业务模型查询
var results = db.Queryable<PrtsData>()
    .Where(x => x.Tag.Contains("character"))
    .ToList();

// 分页
var (list, total) = db.Queryable<Plot>()
    .Where(x => x.Title.Contains("章节"))
    .ToPageList(1, 10);

// 事务
var result = db.Ado.UseTran(() =>
{
    db.Insertable(new Plot("新章节", new StringBuilder("内容"))).ExecuteCommand();
    db.Insertable(new FormattedTextEntry { OriginalText = "文本" }).ExecuteCommand();
});
```

## 5. 高级特性（可选）

### 5.1 复杂类型处理

```csharp
// 对于复杂类型，使用 JSON 存储
public class Plot
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    
    public string Title { get; init; }
    
    [SugarColumn(IsIgnore = true)] 
    public StringBuilder Content { get; init; }
    
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ContentText
    {
        get => Content?.ToString() ?? "";
        set => Content = new StringBuilder(value);
    }
    
    [SugarColumn(IsIgnore = true)] 
    public List<FormattedTextEntry> TextVariants = [];
    
    [SugarColumn(ColumnDataType = "TEXT")]
    public string TextVariantsJson
    {
        get => JsonSerializer.Serialize(TextVariants);
        set => TextVariants = JsonSerializer.Deserialize<List<FormattedTextEntry>>(value) ?? [];
    }
}
```

### 5.2 索引优化

```csharp
[SugarTable("PrtsData")]
public class PrtsData
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    
    [SugarColumn(IndexGroupNameList = new[] { "idx_tag" })]
    public readonly string Tag;
    
    // ... 其他属性
}
```

## 6. 项目结构

```
ArkPlotWpf/
├── Model/                    # 业务模型（几乎零改动）
│   ├── PrtsData.cs          # 只加特性
│   ├── Plot.cs              # 只加特性
│   └── FormattedTextEntry.cs # 只加特性
├── Data/
│   └── SqlSugarSetup.cs     # 单文件配置
└── 使用示例：
    new PrtsDataRepo().Add(new PrtsData("tag", data));
```

## 7. 迁移优势

- **零学习成本**：业务模型就是数据库模型
- **代码量最少**：单文件配置，通用仓储
- **性能最优**：直接操作业务对象，无转换开销
- **灵活扩展**：可随时添加专用方法
- **兼容性好**：原有业务逻辑完全不变

## 8. 完整使用示例

```csharp
// 一行代码初始化
var db = Db.Instance;

// 一行代码增删改查
new PrtsDataRepo().Add(new PrtsData("test", new StringDict()));

// 直接查询业务对象
var data = db.Queryable<PrtsData>().First(x => x.Tag == "test");

// 完全兼容原有业务逻辑
var plot = new Plot("标题", new StringBuilder("内容"));
new PlotRepo().Add(plot);
```

这就是最轻量级的 SqlSugar 实现：业务模型即数据库模型，代码最少，使用最直观！

## 5. 高级查询功能

### 5.1 复杂查询示例

```csharp
public class AdvancedQueryService
{
    private readonly SqlSugarClient _db;

    public AdvancedQueryService(string connectionString)
    {
        _db = SqlSugarConfig.GetInstance(connectionString);
    }

    // 分页查询
    public async Task<List<PrtsDataEntity>> GetPagedPrtsDataAsync(int pageIndex, int pageSize, string searchTag = null)
    {
        var query = _db.Queryable<PrtsDataEntity>();
        
        if (!string.IsNullOrEmpty(searchTag))
        {
            query = query.Where(x => x.Tag.Contains(searchTag));
        }

        return await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize);
    }

    // 多条件查询
    public async Task<List<PrtsDataEntity>> SearchPrtsDataAsync(string tagPattern, DateTime? startTime, DateTime? endTime)
    {
        return await _db.Queryable<PrtsDataEntity>()
            .WhereIF(!string.IsNullOrEmpty(tagPattern), x => x.Tag.Contains(tagPattern))
            .WhereIF(startTime.HasValue, x => x.CreatedTime >= startTime.Value)
            .WhereIF(endTime.HasValue, x => x.CreatedTime <= endTime.Value)
            .ToListAsync();
    }

    // 使用原生 SQL
    public async Task<List<PrtsDataEntity>> GetByCustomSqlAsync(string tag)
    {
        return await _db.Ado.SqlQueryAsync<PrtsDataEntity>(
            "SELECT * FROM PrtsData WHERE Tag LIKE @tag",
            new { tag = $"%{tag}%" });
    }

    // 事务处理
    public async Task<bool> BatchInsertAsync(List<PrtsDataEntity> entities)
    {
        var result = false;
        await _db.Ado.UseTranAsync(async () =>
        {
            result = await _db.Insertable(entities).ExecuteCommandAsync() > 0;
        });
        return result;
    }

    // JSON 查询
    public async Task<List<PrtsDataEntity>> QueryByJsonFieldAsync(string jsonProperty, string value)
    {
        return await _db.Queryable<PrtsDataEntity>()
            .Where(x => x.DataJson.Contains($"\"{jsonProperty}\":\"{value}\""))
            .ToListAsync();
    }
}
```

## 6. 数据迁移

### 6.1 从 Dapper 迁移到 SqlSugar

```csharp
public class DataMigrationService
{
    private readonly SqlSugarClient _db;

    public DataMigrationService(string connectionString)
    {
        _db = SqlSugarConfig.GetInstance(connectionString);
    }

    public async Task<bool> MigrateFromDapperAsync()
    {
        try
        {
            // 1. 创建新表结构
            _db.CodeFirst.SetStringDefaultLength(100).InitTables(
                typeof(PrtsDataEntity),
                typeof(PlotEntity),
                typeof(FormattedTextEntryEntity));

            // 2. 数据迁移（如果需要）
            // 这里可以添加从旧表迁移数据到新表的逻辑

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Migration failed: {ex.Message}");
            return false;
        }
    }
}
```

## 7. 性能优化

### 7.1 索引配置

```csharp
[SugarTable("PrtsData")]
public class PrtsDataEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [SugarColumn(IsNullable = false, Length = 100, IndexGroupNameList = new string[] { "idx_tag" })]
    public string Tag { get; set; } = "";

    [SugarColumn(IsNullable = false, ColumnDataType = "TEXT")]
    public string DataJson { get; set; } = "";

    [SugarColumn(IsNullable = true, IndexGroupNameList = new string[] { "idx_createdtime" })]
    public DateTime? CreatedTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? UpdatedTime { get; set; }
}
```

### 7.2 缓存配置

```csharp
public class CacheService
{
    private readonly SqlSugarClient _db;

    public CacheService(string connectionString)
    {
        _db = SqlSugarConfig.GetInstance(connectionString);
    }

    public async Task<List<PrtsDataEntity>> GetWithCacheAsync(string tag)
    {
        return await _db.Queryable<PrtsDataEntity>()
            .WithCache()
            .Where(x => x.Tag == tag)
            .ToListAsync();
    }
}
```

## 8. 使用示例

### 8.1 基本 CRUD 操作

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = "Data Source=arkplot.db";
        var repository = new PrtsDataRepository(connectionString);

        // 添加数据
        var prtsData = new PrtsData
        {
            Tag = "test-tag",
            DataJson = "{\"key\":\"value\"}"
        };

        await repository.AddOrUpdatePrtsDataAsync(prtsData);

        // 查询数据
        var result = await repository.GetPrtsDataByTagAsync("test-tag");
        Console.WriteLine(result?.DataJson);

        // 删除数据
        await repository.DeletePrtsDataByTagAsync("test-tag");
    }
}
```

### 8.2 依赖注入配置

```csharp
// 在 Startup.cs 或 Program.cs 中
services.AddScoped<IPrtsDataRepository>(provider =>
    new PrtsDataRepository(connectionString));
```

## 总结

SqlSugar 提供了以下优势：

1. **简化开发**：自动表创建、属性映射
2. **性能优化**：内置缓存、批量操作
3. **功能丰富**：支持复杂查询、事务、存储过程
4. **易于维护**：代码简洁，可读性强
5. **跨数据库**：支持多种数据库类型

通过以上实现，你可以轻松地将现有 Dapper 代码迁移到 SqlSugar，同时获得更好的开发体验和性能。