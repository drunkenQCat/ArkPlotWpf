# TtsWindow 逻辑缺口分析与修复计划

> 2026-06-05 审计 TtsViewModel + TtsWindow.axaml 的已实现 vs 未接通逻辑

---

## 一、缺口总览

| # | 缺口 | 严重度 | 状态 |
|---|---|---|---|
| 1 | **窗口打开不自动对齐** | 🔴 致命 | ✅ 已修复 |
| 2 | **音频播放用 Process.Start** | 🔴 致命 | ✅ 改用 NAudio WaveOutEvent |
| 3 | **生成后不刷新音频状态** | 🟡 严重 | ✅ RefreshAudioStatus |
| 4 | **立绘不加载** | 🟡 严重 | ✅ FormattedTextEntry.Portraits |
| 5 | **Gallery 背景图加载靠猜** | 🟡 严重 | ✅ charslot ResourceUrls |
| 6 | **导出只是日志** | 🟠 中等 | ✅ File.Copy 实际复制 |
| 7 | **搜索结果不跳转** | 🟠 中等 | ✅ 确认已可工作 |
| 8 | **窗口关闭不释放资源** | 🟠 中等 | ✅ IDisposable + Closed 事件 |
| 9 | **ComboBox 不显示 DisplayText** | 🔵 轻微 | ✅ ItemTemplate |

---

## 二、逐步修复计划

### Gap 1: 窗口打开不自动对齐

**问题**：构造函数里只调了 `ScanNovelFiles()`，没触发对齐。

**方案**：
```
构造函数末尾 → 延迟 100ms → 自动调用 LoadAlignmentAsync()
```
- 用 `Task.Run` 异步触发，不阻塞 UI 线程
- 对齐完成后自动填充章节/片段/音色配置

**代码改动**：`TtsViewModel` 构造函数

---

### Gap 2: 音频播放用 Process.Start

**问题**：`Process.Start(filePath)` 打开系统播放器，无法暂停/停止/获取时长。

**方案**：使用 **NAudio**（ArkPlot.Tts 已依赖）的 `WaveOutEvent` + `Mp3FileReader`

```csharp
// TtsViewModel 中：
private IWavePlayer? _wavePlayer;
private AudioFileReader? _audioReader;

private async Task PlayAudioFile(string filePath, CancellationToken ct)
{
    _audioReader = new AudioFileReader(filePath);
    _wavePlayer = new WaveOutEvent();
    _wavePlayer.Init(_audioReader);
    _wavePlayer.Play();

    // 轮询检查播放结束或取消
    while (_wavePlayer.PlaybackState == PlaybackState.Playing)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Delay(100, ct);
    }

    _wavePlayer.Dispose();
    _audioReader.Dispose();
}
```

**暂停**：`_wavePlayer?.Pause()`
**停止**：`_wavePlayer?.Stop()` + `_wavePlayer?.Dispose()`

**优点**：
- 精确控制播放/暂停/停止
- 可以获取精确时长（`_audioReader.TotalTime`）
- 不依赖外部进程
- NAudio 已在项目依赖中

**代码改动**：
- `TtsViewModel` 新增 `_wavePlayer` / `_audioReader` 字段
- 重写 `PlayAudioFile`
- 重写 `StopPlay` → 调用 `_wavePlayer?.Stop()`
- 新增 `PausePlay` 逻辑

---

### Gap 3: 生成后不刷新音频状态

**问题**：`StartGenerateAsync` 用章节文件名模糊匹配，不精确标记每个片段。

**方案**：生成完成后扫描 `TtsOutputDir` 中的 MP3，按章节标题匹配并标记。

```csharp
// 生成后：
RefreshAudioStatus();

private void RefreshAudioStatus()
{
    if (!Directory.Exists(TtsOutputDir)) return;
    var mp3Files = Directory.GetFiles(TtsOutputDir, "*.mp3");
    // 按章节分组匹配
}
```

---

### Gap 4: 立绘不加载

**问题**：`CurrentPortrait` 永远为 null，没有任何代码设置它。

**方案**：连播时从 `CharacterCode` → `PrtsPortraitLink` 表查找立绘 URL。

```csharp
private async Task<string?> LoadPortraitAsync(string? characterCode)
{
    if (string.IsNullOrEmpty(characterCode)) return null;
    var db = DbFactory.GetClient();
    var link = await db.Queryable<PrtsPortraitLink>()
        .Where(p => p.CharacterCode == characterCode)
        .FirstAsync();
    return link?.PortraitUrl;
}
```

**触发点**：`PlayFromSelectedAsync` 循环中，每次切段时设置 `CurrentPortrait`。

---

### Gap 5: Gallery 背景图加载

**问题**：当前从 `PrtsResource` 表按 `ResourceKey == characterCode` 查，但背景图的存储方式不同。

**方案**：从 `FormattedTextEntry` 的 `ResourceUrls` 字段提取背景图 URL。
- 需要按 `PlotId` + `Index` 关联到对应的 charslot 条目
- charslot 类型的 FormattedTextEntry 的 `ResourceUrls` 包含背景图 URL

需要重写 `LoadBackgroundsAsync`：
1. 查询当前活动的所有 `FormattedTextEntry`（按 PlotId + Index 排序）
2. 遍历 entries，找到 `Type == "charslot"` 且 `ResourceUrls` 非空的条目
3. 关联到最近的对话段落

---

### Gap 6: 导出只是日志

**方案**：改为用 `OpenFolderDialog` 选目标目录，然后 `File.Copy` 过去。
或者简化为：直接在输出目录原地展示，用户从文件管理器取。

---

### Gap 7: 搜索结果不跳转

**问题**：`OnSearchTextChanged` 调了 `LoadSegmentsForChapter()`，过滤逻辑已写，但 `SearchText` 为空时应显示当前章节所有片段。

**方案**：现有逻辑其实是对的，只需要确认 `_allEntries` 有数据（依赖 Gap 1 修复）。

---

### Gap 8: 窗口关闭不释放资源

**方案**：`TtsWindow.axaml.cs` 加 `Closed` 事件 → ViewModel 实现 `IDisposable`。

---

### Gap 9: ComboBox 不显示章节名

**方案**：给 ComboBox 加 `ItemTemplate`，绑定 `DisplayText`。

---

## 三、执行优先级

| 优先级 | Gap | 预估工作量 |
|---|---|---|
| P0 | #1 自动对齐 | 5min |
| P0 | #2 NAudio 播放 | 15min |
| P0 | #9 ComboBox 显示 | 2min |
| P1 | #4 立绘加载 | 10min |
| P1 | #3 刷新音频状态 | 10min |
| P1 | #5 Gallery 背景图 | 15min |
| P2 | #6 导出实际逻辑 | 10min |
| P2 | #8 资源释放 | 5min |
| P3 | #7 搜索已可工作 | 0min（确认即可） |
