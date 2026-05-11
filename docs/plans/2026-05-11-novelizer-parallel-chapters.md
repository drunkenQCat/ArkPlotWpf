# Novelizer 并行处理三章

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将 `ProcessMdFileAsync` 中逐章串行调 LLM 改为最多同时处理 3 章，大幅缩短总耗时。

**Architecture:** 用 `SemaphoreSlim(3)` 限流，`Dictionary<int, string>` 保持输出顺序，`Task.WhenAll` 等待全部完成。`onLog` 回调在 ViewModel 侧用 `Dispatcher.UIThread.InvokeAsync` 确保线程安全。

**Tech Stack:** .NET 9, SemaphoreSlim, Task.WhenAll, Avalonia Dispatcher

**现状**：17 章的「无忧梦呓」串行处理约 17 × 30s ≈ 8 分钟。并行 3 章可降到 ~3 分钟。

---

### Task 1: ViewModel 侧 onLog 回调加 UI 线程保护

**Files:**
- Modify: `ArkPlot.Avalonia/ViewModels/MainWindowViewModel.cs:421-424`

**Step 1: 修改 onLog 回调，包裹 Dispatcher.UIThread.InvokeAsync**

当前代码：
```csharp
var log = (string msg) => noticeBlock.RaiseCommonEvent(msg);
```

改为：
```csharp
var log = (string msg) =>
{
    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => noticeBlock.RaiseCommonEvent(msg));
};
```

**原因**：并行处理时多个 Task 在不同线程池线程上执行，`noticeBlock.RaiseCommonEvent` 触发的事件处理器会更新 `ConsoleOutput`（`[ObservableProperty]`），必须在 UI 线程上执行。

**Step 2: 编译验证**

```bash
dotnet build ArkPlot.Avalonia/ArkPlot.Avalonia.csproj
```

预期：Build succeeded.

**Step 3: Commit**

```bash
git add ArkPlot.Avalonia/ViewModels/MainWindowViewModel.cs
git commit -m "fix: onLog 回调加 Dispatcher.UIThread.InvokeAsync 保证线程安全"
```

---

### Task 2: ProcessMdFileAsync 改为并行处理

**Files:**
- Modify: `ArkPlot.Novelizer/NovelizerPipeline.cs` — `ProcessMdFileAsync` 方法中的 `for` 循环

**Step 1: 将串行 for 循环替换为 SemaphoreSlim 并行**

当前 `ProcessMdFileAsync` 中的章节处理循环（~第 109-162 行）：

```csharp
for (int i = 0; i < rawChapters.Count; i++)
{
    var chunk = rawChapters[i];
    ...
    var result = await _client.ChatAsync(model, SystemPrompt, body);
    ...
}
```

替换为 SemaphoreSlim 并行版本：

```csharp
var semaphore = new SemaphoreSlim(3);
var results = new Dictionary<int, string>(); // key=索引, value=小说文本
var tasks = new List<Task>();
int totalPrompt = 0, totalCompletion = 0;
var tokenLock = new object();

for (int i = 0; i < rawChapters.Count; i++)
{
    var idx = i; // 捕获循环变量
    var chunk = rawChapters[idx];
    var lines = chunk.Split('\n', 2);
    var title = lines[0].TrimStart('#', ' ').Trim();
    var body = lines.Length > 1 ? lines[1].Trim() : "";

    Log($"[DIAG] 第 {idx+1}/{rawChapters.Count} 章「{title}」, body={body.Length} 字符");

    if (string.IsNullOrEmpty(body))
    {
        Log($"⏭️  第 {idx + 1}/{rawChapters.Count} 章「{title}」无正文，跳过。");
        results[idx] = $"## {title}\n\n> *（本章无正文）*";
        continue;
    }

    tasks.Add(Task.Run(async () =>
    {
        await semaphore.WaitAsync();
        try
        {
            Log($"\n--- 第 {idx + 1}/{rawChapters.Count} 章: {title} ({body.Length} 字符) ---");
            Log($"[DIAG] 即将调用 ChatAsync for 第 {idx+1} 章「{title}」");

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await _client.ChatAsync(model, SystemPrompt, body);
                sw.Stop();
                Log($"[DIAG] ChatAsync 返回，耗时 {sw.Elapsed.TotalSeconds:F1}s");

                results[idx] = $"## {title}\n\n{result.AnswerContent}";

                if (result.Usage is not null)
                {
                    lock (tokenLock)
                    {
                        totalPrompt += result.Usage.PromptTokens;
                        totalCompletion += result.Usage.CompletionTokens;
                    }
                    Log($"✅ Token: 入 {result.Usage.PromptTokens} / 出 {result.Usage.CompletionTokens}");
                    Log($"[DIAG] 第 {idx+1} 章 token: prompt={result.Usage.PromptTokens}, completion={result.Usage.CompletionTokens}");
                }
            }
            catch (BailianException ex)
            {
                sw.Stop();
                Log($"[DIAG] ChatAsync 抛出 BailianException（{sw.Elapsed.TotalSeconds:F1}s）: {ex.Message}");
                LogError($"❌ 第 {idx + 1} 章失败: {ex.Message}");
                results[idx] = $"## {title}\n\n> *（本章生成失败：{ex.Message}）*";
            }
        }
        finally
        {
            semaphore.Release();
        }
    }));
}

await Task.WhenAll(tasks);

// 按原始顺序组装 allParts
var allParts = results.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
```

**注意**：需要添加 `using System.Diagnostics;` 到文件顶部（`Stopwatch` 命名空间）。

**Step 2: 编译验证**

```bash
dotnet build ArkPlot.Novelizer/ArkPlot.Novelizer.csproj && dotnet build ArkPlot.Avalonia/ArkPlot.Avalonia.csproj
```

预期：Build succeeded.

**Step 3: 用 example_data.json 快速验证**

```bash
dotnet run --project ArkPlot.Novelizer -- test example_data.json
```

预期：正常生成 pro + flash 两个小说文件，无异常。

**Step 4: Commit**

```bash
git add ArkPlot.Novelizer/NovelizerPipeline.cs
git commit -m "perf: ProcessMdFileAsync 改为 SemaphoreSlim(3) 并行处理三章"
```

---

### Task 3: 清理旧的调试日志（可选）

**Files:**
- Modify: `ArkPlot.Novelizer/NovelizerPipeline.cs`
- Modify: `ArkPlot.Novelizer/BailianClient.cs`
- Modify: `ArkPlot.Avalonia/ViewModels/MainWindowViewModel.cs`

**Step 1: 删除 `LogDiag` 方法和所有 `[DIAG]` 日志**

如果诊断已完成、并行版稳定，可以把调试日志精简掉，只保留正常的 `Log()`。

或保留 `[DIAG]` 标记但降级为仅在 `#if DEBUG` 下输出：

```csharp
[Conditional("DEBUG")]
private void LogDiag(string msg) { ... }
```

此步为可选，确认稳定后再做。

---

### 关键设计决策

| 决策 | 选择 | 原因 |
|------|------|------|
| 并发上限 | 3 | DeepSeek API 限流约 5 QPS，3 安全 |
| 结果排序 | `Dictionary<int, string>` + `OrderBy` | 小说章节必须保持原始顺序 |
| 线程安全 | `lock(tokenLock)` 保护 token 计数 | 避免竞态条件 |
| 日志回调 | `Dispatcher.UIThread.InvokeAsync` | ObservableProperty 更新必须在 UI 线程 |
| 异常隔离 | per-chapter try-catch | 一章失败不阻塞其他章节 |

---

### 预期效果

- **17章故事**：串行 ~8 分钟 → 并行 ~3 分钟
- **单章 API 调用**：串行 `await` → `SemaphoreSlim` 内 `await`
- **错误处理**：单章失败标记 `> *（本章生成失败）*`，不影响其他章
- **输出顺序**：`Dictionary` + `OrderBy` 保证和源文件章节顺序一致