# ArkPlot 数据库访问层

本文件夹包含了 ArkPlot 项目的数据库访问层，基于 SqlSugar ORM 框架构建，提供了完整的数据库操作功能。

## 架构设计

### 1. 数据库上下文 (DatabaseContext)
- 负责管理 SqlSugar 数据库连接和配置
- 提供单例模式的数据库访问
- 自动初始化数据库表结构

### 2. 仓储模式 (Repository Pattern)
- **IBaseRepository<T>**: 基础仓储接口，定义通用 CRUD 操作
- **BaseRepository<T>**: 基础仓储实现，提供通用 CRUD 功能
- **具体仓储类**: 为每个实体提供特定的业务操作

### 3. 仓储工厂 (RepositoryFactory)
- 提供统一的仓储访问入口
- 使用单例模式管理仓储实例
- 提供事务操作支持

### 4. 数据库迁移 (DatabaseMigration)
- 支持数据库版本管理
- 自动执行数据库迁移
- 提供种子数据初始化

### 5. 数据库服务 (DatabaseService)
- 提供数据库管理功能
- 支持备份和恢复
- 提供统计信息和清理功能

## 文件结构

```
Data/
├── DatabaseContext.cs          # 数据库上下文
├── DatabaseMigration.cs        # 数据库迁移
├── DatabaseService.cs          # 数据库服务
├── README.md                   # 使用说明
└── Repositories/
    ├── IBaseRepository.cs      # 基础仓储接口
    ├── BaseRepository.cs       # 基础仓储实现
    ├── RepositoryFactory.cs    # 仓储工厂
    ├── PlotRepository.cs       # Plot 仓储
    ├── FormattedTextEntryRepository.cs  # FormattedTextEntry 仓储
    └── PrtsDataRepository.cs   # PrtsData 仓储
```

## 使用方法

### 1. 初始化数据库

```csharp
// 在应用程序启动时初始化数据库
DatabaseService.Instance.Initialize();
```

### 2. 使用仓储进行数据操作

```csharp
// 获取仓储实例
var plotRepo = RepositoryFactory.Plot;
var textEntryRepo = RepositoryFactory.FormattedTextEntry;
var prtsDataRepo = RepositoryFactory.PrtsData;

// 添加数据
var plot = new Plot("测试章节", new StringBuilder("测试内容"));
plotRepo.Add(plot);

// 查询数据
var plots = plotRepo.GetByTitle("测试");
var textEntries = textEntryRepo.GetByCharacterName("角色名");

// 更新数据
plotRepo.UpdateTitle(1, "新标题");

// 删除数据
plotRepo.DeleteById(1);
```

### 3. 异步操作

```csharp
// 异步添加数据
await plotRepo.AddAsync(plot);

// 异步查询数据
var plots = await plotRepo.GetByTitleAsync("测试");
```

### 4. 事务操作

```csharp
// 同步事务
RepositoryFactory.UseTransaction(() =>
{
    plotRepo.Add(plot1);
    plotRepo.Add(plot2);
});

// 异步事务
await RepositoryFactory.UseTransactionAsync(async () =>
{
    await plotRepo.AddAsync(plot1);
    await plotRepo.AddAsync(plot2);
});
```

### 5. 数据库管理

```csharp
// 获取数据库统计信息
var stats = DatabaseService.Instance.GetStatistics();
Console.WriteLine(stats);

// 备份数据库
DatabaseService.Instance.Backup("backup.db");

// 清理数据库
DatabaseService.Instance.Cleanup();
```

## 支持的实体

### 1. Plot (章节)
- 标题管理
- 内容管理
- 分页查询
- 模糊搜索

### 2. FormattedTextEntry (格式化文本条目)
- 类型管理
- 角色名称管理
- 索引范围查询
- 标签查询

### 3. PrtsData (PRTS 数据)
- 标签管理
- 数据字典操作
- 批量更新
- 前缀查询

## 性能优化

1. **连接池**: 使用 SqlSugar 的连接池管理
2. **索引**: 自动为常用查询字段创建索引
3. **分页**: 支持高效的分页查询
4. **事务**: 支持批量操作的事务处理

## 错误处理

所有数据库操作都包含适当的错误处理：
- 异常日志记录
- 事务回滚
- 连接状态检查
- 数据验证

## 扩展指南

### 添加新的实体仓储

1. 创建新的仓储类继承 `BaseRepository<T>`
2. 在 `RepositoryFactory` 中添加对应的属性
3. 实现特定的业务方法

```csharp
public class NewEntityRepository : BaseRepository<NewEntity>
{
    public NewEntityRepository(SqlSugarClient db = null) : base(db) { }
    
    // 添加特定的业务方法
    public List<NewEntity> GetByCustomField(string field) =>
        GetWhere(x => x.CustomField == field);
}
```

### 添加新的数据库迁移

1. 实现 `IMigration` 接口
2. 在 `DatabaseMigration.GetMigrations()` 中添加新的迁移

```csharp
public class Migration_004_NewFeature : IMigration
{
    public int Version => 4;
    public string Description => "添加新功能";

    public void Execute(SqlSugarClient db)
    {
        // 执行迁移逻辑
    }
}
```

## 注意事项

1. 确保在应用程序启动时调用 `DatabaseService.Instance.Initialize()`
2. 使用仓储工厂获取仓储实例，避免直接创建
3. 大量数据操作时使用事务
4. 定期备份数据库
5. 监控数据库性能和使用情况 