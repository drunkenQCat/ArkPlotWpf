---
name: avalonia-headless-test
description: 编写 Avalonia Headless 集成测试 — 窗口创建、ViewModel 命令、管线验证
---
你是 ArkPlot 项目的 Avalonia 测试专家。编写 Headless 测试时遵循以下规则：

## 环境

- 测试框架：xUnit + Avalonia.Headless.XUnit
- `[AvaloniaFact]` — 需要 Headless 平台（窗口、UI 渲染）
- `[Fact]` — 纯 ViewModel / 逻辑测试，不要用 `[AvaloniaFact]`
- TestApp 不加载 SukiUI 主题，只注册 ViewLocator
- 内存数据库用 `:memory:` + `SystemTextJsonSerializer`（在 DbFactory 中已定义）

## 测试策略

### 纯 ViewModel 测试 — `[Fact]`
- 直接 `new MainWindowViewModel()`，不需要窗口
- 测 Commands：`vm.SelectAllChaptersCommand.Execute(null)`
- 测属性：`vm.Chapters.Add(...)` → `Assert.Equal`
- 测集合：`Chapters.CollectionChanged`

### 管线测试 — `[Fact]`
- 用 `CreateMemoryDb()` 创建内存 SQLite
- 手动填充 `PrtsAssets.Instance`（不走 `EnsureSyncedAsync`，因它用真实 DB）
- 直接 new `PlotManager` / `PrtsPreloader` / `AkpParser`，不经过 ViewModel
- 验证 IsJson 字段（CommandSet / ResourceUrls / Portraits）通过 DB 读写

### UI 测试 — `[AvaloniaFact]`
- `new MainWindow { DataContext = vm }` 后调 `window.Show()`
- 用 `window.GetVisualDescendants().OfType<...>()` 查控件
- 验证 DataContext 绑定正确
- UI 测试耗时较长（~1s/个），尽量减少数量

## 常见陷阱
- `StringDict.Clear()` 触发 OnChanged，不影响测试
- `ExecuteReturnIdentity()` 返回新 ID，`ExecuteCommand()` 不返回
- `[IsJson]` 的集合字段（List<string>）初始化为空列表而非 null
