# ArkPlot.Avalonia Null 安全审计

> 审计时间: 2026-06-04
> 起因: 删除自定义 Provider 后 ComboBox 将 SelectedItem 设为 null → `Dictionary.TryGetValue(null)` → `ArgumentNullException`
> 已修复: 数据层 Get 方法加 `string?` + `IsNullOrEmpty` 防护；ViewModel 层 Delete 时重置 SelectedProvider

---

## 未修复的隐患清单

### CRITICAL-1: `ApiKeys` 字典可能为 null → NullReferenceException

**文件:** `Models/AppSettings.cs` 第 80 行、第 209 行

**代码:**
```csharp
// 第 80 行 — AppSettings.GetApiKey
if (Novelizer.ApiKeys.TryGetValue(providerName, out var key) ...)

// 第 209 行 — NovelizerSettings.GetApiKeyForProvider
if (ApiKeys.TryGetValue(providerName, out var key) ...)
```

**触发场景:**
- 用户手动编辑 settings.json，删除了 `"ApiKeys"` 字段或设为 null
- 早期版本写入的不完整 settings.json

**修复:** 改为 `ApiKeys?.TryGetValue(...) == true`

---

### CRITICAL-3: `OnSelectedProviderChanged` 收到 null → 保存后损坏 settings.json

**文件:** `ViewModels/SettingsViewModel.cs` 第 121 行

**代码:**
```csharp
partial void OnSelectedProviderChanged(string value)
{
    // value 可能为 null（ComboBox ItemsSource 替换时）
    ModelOptions = novelizer.GetModelsForProvider(value); // 返回 []
    // SelectedProvider 属性值现在是 null
    // 随后 Save 会把 null 写入 settings.json
}
```

**修复:** 开头加 `if (string.IsNullOrEmpty(value)) return;`

---

### CRITICAL-4: Save 方法可能保存 null SelectedProvider/SelectedModel

**文件:** `ViewModels/SettingsViewModel.cs` 第 196 行 (`SaveNovelizerSettings`)、第 260 行 (`SaveVisionSettings`)

**代码:**
```csharp
var novelizer = settings.Novelizer with
{
    SelectedProvider = SelectedProvider,  // 可能为 null
    SelectedModel = SelectedModel,        // 可能为 null/空
};
settings.Save();  // 写入 null → 永久损坏配置
```

**修复:** 保存前校验：
```csharp
if (string.IsNullOrEmpty(SelectedProvider) || string.IsNullOrEmpty(SelectedModel))
{
    SaveFeedbackText = "⚠️ 请先选择一个有效的平台和模型";
    return;
}
```

---

### HIGH-8: ExportDocuments vision provider/model 为 null 时静默跳过

**文件:** `ViewModels/MainWindowViewModel.cs` 第 409 行

**代码:**
```csharp
var providerName = vision.SelectedProvider;   // 可能为 null（JSON 反序列化后）
var model = vision.SelectedModel;             // 可能为 null
```

**后果:** 不会崩溃（GetApiKeyForProvider(null) 返回 ""，走 skipVision），但用户看不到明确错误提示。

**修复:** 加默认值降级：
```csharp
if (string.IsNullOrEmpty(providerName)) providerName = "百炼";
if (string.IsNullOrEmpty(model)) model = "qwen3-vl-flash";
```

---

### HIGH-9: RunNovelizerIfEnabled 空 model 传入 API pipeline

**文件:** `ViewModels/MainWindowViewModel.cs` 第 557 行

**代码:**
```csharp
var model = novelizer.SelectedModel;  // 可能为 "" 或 null
await pipeline.BatchProcessAsync(outputPathOfCurrentStory, [model], force: false);
```

**后果:** 空字符串模型名调用 LLM API → API 返回错误 → 用户看到"小说生成失败"但原因不明。

**修复:** pipeline 调用前校验：
```csharp
if (string.IsNullOrEmpty(model))
{
    noticeBlock.RaiseCommonEvent("❌ 未选择模型，跳过小说生成。");
    return;
}
```

---

### MEDIUM-5/6: Load 时已删除 provider 导致 SelectedModel 降级为 ""

**文件:** `ViewModels/SettingsViewModel.cs` 第 130 行 (`LoadNovelizerSettings`)、第 239 行 (`LoadVisionSettings`)

**代码:**
```csharp
SelectedProvider = novelizer.SelectedProvider;  // 可能是已删除的自定义名称
ModelOptions = novelizer.GetModelsForProvider(SelectedProvider); // 返回 []
SelectedModel = ModelOptions.Length > 0 ? ModelOptions[0] : "";  // 设为 ""
// SelectedProvider 不在 ProviderOptions 列表中 → ComboBox 显示异常 → 可能触发 null
```

**修复:** Load 时校验 SelectedProvider 是否在当前可用列表中：
```csharp
SelectedProvider = novelizer.SelectedProvider;
if (!ProviderOptions.Contains(SelectedProvider))
    SelectedProvider = ProviderOptions.Length > 0 ? ProviderOptions[0] : "DeepSeek";
```

---

## 修复优先级建议

1. **CRITICAL-1** — 最简单（2 处加 `?.`），影响启动时加载
2. **CRITICAL-3** — 1 行代码阻断 null 传播
3. **CRITICAL-4** — 保存前校验，防止永久损坏配置
4. **MEDIUM-5/6** — 加载时兜底，防止链式崩溃
5. **HIGH-8/9** — 运行时校验，改善用户体验

全部修复后需补充回归测试覆盖 null provider 场景。
