---
name: blazor-dev
description: Blazor 全栈开发助手 — 组件/生命周期/渲染模式/表单/CSS isolation/JS互操作/路由/测试/性能优化
---
# Blazor 开发助手

你是 Blazor / ASP.NET Core 全栈开发专家，严格遵循微软官方最佳实践。
运行 `dotnet --version` 确认 .NET SDK 版本，所有代码与当前 SDK 兼容。

## 1. 渲染模式（Render Mode）

### Blazor Web App 四种渲染模式

| 模式 | 指令 | 说明 |
|------|------|------|
| 静态 SSR | `@rendermode @(new InteractiveServerRenderMode(false))` 不添加 | 无交互，纯服务端渲染 |
| Interactive Server | `@rendermode InteractiveServer` | 通过 SignalR 实时连接处理 UI 事件 |
| Interactive WebAssembly | `@rendermode InteractiveWebAssembly` | 浏览器中运行 .NET WASM 运行时 |
| Interactive Auto | `@rendermode InteractiveAuto` | 先下载 WASM 运行时前用 Server 模式，之后切换 WASM |

- 页面级别设置渲染模式：`@page "/counter" @rendermode InteractiveServer`
- 组件级别设置：`<Counter @rendermode="InteractiveServer" />`
- 全局设置：在 `Routes.razor` 的 `<Routes />` 上设置默认模式
- Prerendering 期间 **不能** 调用 JS interop，用 `OnAfterRender` + `firstRender` 判断

## 2. 组件生命周期（按调用顺序）

```
SetParametersAsync
  ↓
OnInitialized / OnInitializedAsync     ← 组件初始化（仅一次）
  ↓
OnParametersSet / OnParametersSetAsync  ← 参数变更时触发
  ↓
ShouldRender                            ← 返回 false 跳过渲染
  ↓
Render (构建渲染树 diff)
  ↓
OnAfterRender / OnAfterRenderAsync      ← DOM 已更新，可做 JS interop
  ↓
Dispose / DisposeAsync                   ← 组件销毁时清理资源
```

### 关键规则

- **`OnInitializedAsync` 在预渲染时调用两次**（服务器 + 客户端），用 `PersistentComponentState` 避免重复初始化
- **`OnAfterRender(firstRender)`** 的 `firstRender` 在组件首次渲染时为 true，适合做一次性的 JS interop 初始化
- **`ShouldRender()`** 返回 false 跳过本次渲染，用于性能优化（默认 true）
- 异步生命周期方法中确保组件处于有效渲染状态，处理 `CancellationToken`
- **`InvokeAsync(StateHasChanged)`** 在外部线程通知组件重新渲染

```razor
@implements IDisposable

@code {
    private CancellationTokenSource? cts;

    protected override async Task OnInitializedAsync()
    {
        cts = new CancellationTokenSource();
        // 异步初始化
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}
```

## 3. 组件设计与参数

### 参数定义

```razor
<!-- 普通参数 -->
[Parameter] public string Title { get; set; } = string.Empty;

<!-- 级联参数 -->
[CascadingParameter] public AppState AppState { get; set; } = default!;

<!-- 通过 CascadingValue 提供 -->
<CascadingValue Value="appState">
    <ChildComponent />
</CascadingValue>

<!-- 按名称传递 -->
<CascadingValue Name="Theme" Value="currentTheme">
    <ChildComponent />
</CascadingValue>
```

### RenderFragment 模板化组件

```razor
@typeparam TItem

<div class="list">
    @foreach (var item in Items)
    {
        <div class="list-item">
            @ItemTemplate(item)
        </div>
    }
</div>

@code {
    [Parameter] public IEnumerable<TItem> Items { get; set; } = [];
    [Parameter] public RenderFragment<TItem> ItemTemplate { get; set; } = null!;
}
```

### 组件引用

```razor
<!-- 父组件获取子组件实例 -->
<MyDialog @ref="dialog" />

@code {
    private MyDialog? dialog;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && dialog is not null)
        {
            await dialog.ShowAsync();
        }
    }
}
```

## 4. 表单与验证

### EditForm 核心结构

```razor
@using System.ComponentModel.DataAnnotations

<EditForm Model="model" OnValidSubmit="HandleSubmit" FormName="editForm">
    <DataAnnotationsValidator />
    <ValidationSummary />

    <div>
        <label>名称：</label>
        <InputText @bind-Value="model.Name" />
        <ValidationMessage For="() => model.Name" />
    </div>

    <button type="submit">提交</button>
</EditForm>

@code {
    private MyModel model = new();

    private void HandleSubmit() { /* 处理有效提交 */ }

    public class MyModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
```

### 自定义验证组件

```razor
@using Microsoft.AspNetCore.Components.Forms

<EditForm Model="model" OnValidSubmit="Submit">
    <CustomValidator @ref="validator" />
    ...
</EditForm>

@code {
    private CustomValidator? validator;

    private void Submit()
    {
        var errors = new Dictionary<string, List<string>>();
        // 自定义验证逻辑
        if (errors.Count > 0)
            validator?.DisplayErrors(errors);
    }
}
```

- 自定义验证组件需实现 `ComponentBase`，操作 `ValidationMessageStore`
- 服务器端验证通过 Web API 返回 `Dictionary<string, List<string>>`
- **静态 SSR 下客户端验证不可用**，依赖服务器端验证

## 5. CSS Isolation

- **约定**：`MyComponent.razor` → `MyComponent.razor.css`
- 编译后自动添加 `b-{10字符}` 作用域标识
- `::deep` 伪选择器穿透到子组件

```css
/* Parent.razor.css — 影响子组件的 h1 */
::deep h1 {
    color: red;
}
```

- **重要**：`::deep` 仅对后代元素生效，父组件需用 wrapper 包围
- Razor 类库（RCL）的 CSS 通过 `@import '_content/ClassLib/ClassLib.bundle.scp.css'` 引用
- 可通过项目文件 `<None Update="...razor.css" CssScope="..." />` 自定义作用域标识符
- 全局禁用 CSS isolation：`<ScopedCssEnabled>false</ScopedCssEnabled>`

## 6. JavaScript 互操作

### C# 调用 JS

```razor
@inject IJSRuntime JS

@code {
    private ElementReference myElement;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("console.log", "Component rendered");
        }
    }

    private async Task CallJsFunction()
    {
        var result = await JS.InvokeAsync<string>("someJsFunction", args);
    }
}
```

### JS 调用 C#

```csharp
[JSInvokable]
public static async Task<string> GetDataFromDotNet()
{
    return "Data from .NET";
}
```

```javascript
// JavaScript
DotNet.invokeMethodAsync('AssemblyName', 'GetDataFromDotNet')
    .then(data => console.log(data));
```

### JS 模块隔离（推荐）

```javascript
// wwwroot/component.js
export function showPrompt(message) {
    return prompt(message);
}
```

```csharp
private IJSObjectReference? module;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        module = await JS.InvokeAsync<IJSObjectReference>("import", 
            "./_content/YourLib/component.js");
    }
}

async ValueTask IAsyncDisposable.DisposeAsync()
{
    if (module is not null)
        await module.DisposeAsync();
}
```

- **预渲染期间不能调用 JS**，始终在 `OnAfterRender(firstRender == true)` 中做
- 使用 `IJSObjectReference` 导入 ES 模块避免全局污染

## 7. 路由与导航

```razor
@page "/products"
@page "/products/{Category:string?}"
@page "/products/{Id:int}"

@code {
    [Parameter] public string? Category { get; set; }
    [Parameter] public int Id { get; set; }
}
```

### 路由约束

| 约束 | 示例 | 说明 |
|------|------|------|
| `:int` | `{Id:int}` | 整数 |
| `:bool` | `{Active:bool}` | 布尔值 |
| `:datetime` | `{Date:datetime}` | 日期时间 |
| `:guid` | `{Key:guid}` | GUID |
| `:string` | `{Name:string}` | 字符串（默认） |

### 导航管理器

```csharp
@inject NavigationManager Navigation

@code {
    private void GoToProducts()
    {
        Navigation.NavigateTo("/products/electronics");
    }
}
```

- `Navigation.NavigateTo(uri, forceLoad: true)` — 强制整页加载（跨应用）
- `Navigation.NavigateTo(uri, replace: true)` — 不增加浏览器历史
- 路由参数变更自动触发 `OnParametersSetAsync`，**不需要手动监听 URL 变化**

## 8. 依赖注入

### 常用注入方式

```razor
@inject ILogger<MyComponent> Logger
@inject HttpClient Http
@inject IServiceProvider ServiceProvider
@inject NavigationManager Navigation
```

### 注册范围

```csharp
builder.Services.AddSingleton<IMyService, MyService>();    // 全局单例
builder.Services.AddScoped<IMyService, MyService>();       // 每次连接/每个用户会话
builder.Services.AddTransient<IMyService, MyService>();     // 每次请求
```

- **Blazor Server** 中 `Scoped` = 每个 SignalR 连接（每个用户）
- **Blazor WebAssembly** 中 `Scoped` = 等同于 `Singleton`（没有 HTTP 请求概念）
- 需要 `IDisposable` 的服务应尽快释放，或使用工厂模式

## 9. 状态管理

### 方案选择

| 场景 | 方案 |
|------|------|
| 父子组件共享 | `[CascadingParameter]` + `CascadingValue` |
| 页面内多组件 | 状态容器（State Container） |
| 跨页面/用户持久 | 数据库/本地存储 |
| 跨组件实时更新 | `CascadingValue` + `INotifyPropertyChanged` |

```csharp
// 状态容器（Scoped 注册）
public class AppState
{
    public int CurrentCount { get; set; }
    public event Action? OnChange;

    public void SetCount(int count)
    {
        CurrentCount = count;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

### 预渲染状态持久化

```csharp
// 在 Program.cs 或组件中
builder.Services.AddSingleton<PersistentComponentState>();

// 组件中使用
[Inject] private PersistentComponentState PersistentState { get; set; } = default!;
```

## 10. 测试

### bUnit 组件测试

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;

[Test]
public void CounterComponent_Increments_WhenButtonClicked()
{
    using var ctx = new TestContext();
    var cut = ctx.RenderComponent<Counter>();
    
    cut.Find("button").Click();
    
    cut.Find("p").MarkupMatches("<p>Current count: 1</p>");
}
```

### Playwright E2E 测试

```csharp
[Test]
public async Task BlazorApp_LoadsCounterPage()
{
    using var playwright = await Playwright.CreateAsync();
    var browser = await playwright.Chromium.LaunchAsync();
    var page = await browser.NewPageAsync();
    
    await page.GotoAsync("https://localhost:5001/counter");
    await page.ClickAsync("button");
    
    var text = await page.TextContentAsync("p");
    Assert.That(text, Does.Contain("Current count: 1"));
}
```

## 11. 性能优化

- **`ShouldRender()`**：重写以跳过不必要的渲染
- **`QuickGrid`** 组件处理大数据集（内置虚拟化）
- **`Virtualize<TItem>`** 组件虚拟滚动
- 避免在渲染循环中创建委托、分配大量对象
- 使用 `InvokeAsync(StateHasChanged)` 仅在 UI 真的需要更新时调用
- **延迟加载程序集** — `LazyAssemblyLoader` 按需下载 WASM 程序集
- 设置 `@rendermode="RenderMode.InteractiveServer"` 只对需要交互的组件启用

## 12. 项目结构规范

```
MyBlazorApp/
├── Components/
│   ├── Layout/          # 布局组件 (MainLayout.razor)
│   ├── Pages/           # 页面组件 (@page 路由)
│   ├── Shared/          # 共享可复用组件
│   └── Controls/        # 业务专用控件
├── Models/               # 数据模型
├── Services/             # 业务服务
├── State/                # 状态管理
├── wwwroot/              # 静态资源
│   └── css/             # 全局 CSS
└── Program.cs            # 入口配置
```
