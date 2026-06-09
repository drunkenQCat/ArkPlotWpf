# ArkPlot TTS 架构文档

> 生成日期：2026-06-05
> 第一~七章：重构前现状基线（已归档）
> 第八~十一章：重构方案与执行记录

---

## 一、总体架构

TTS 是 ArkPlot 输出管线的**最后一个下游环节**，将解析/小说化后的文本通过 **Edge TTS**（微软在线 API）转换为 MP3 音频。当前**默认关闭**（`CliOptions.EnableTts = false`）。

### 核心依赖

| NuGet 包 | 版本 | 用途 |
|---|---|---|
| `EdgeTTS.DotNet` | 0.4.0 | Edge TTS WebSocket 合成 API 客户端 |
| `NAudio` | 2.3.0 | MP3 帧级读写、合并 |

### 数据流全景

```
原始剧情数据 (SQLite: FormattedTextEntry)
│
├─── 主管线 (CliPipeline)
│    Step 1-8: 解析 → 图片描述 → JSON Dump → Markdown 导出
│    Step 9 (可选): TtsRunner → TtsService → 逐条合成 → 合并 MP3
│
└─── 小说化管线 (Novelizer)
     LLM 生成小说化文本 (.md)
     │
     ├── align 命令 → AlignRunner
     │    NovelAligner 对齐 → 输出 _aligned.json + 音色预览表
     │
     ├── chapter-tts 命令 → ChapterTtsRunner
     │    ChapterTtsGenerator → 对齐 + 按章节逐段合成 → 每章一个 MP3
     │
     └── tts-novel 命令 → NovelTtsRunner
          读取 _aligned.json → 逐段合成 → 单个 MP3
```

---

## 二、文件清单与职责

### 🔴 核心服务层

| 文件 | 行数 | 职责 |
|---|---|---|
| `ArkPlot.Core/Services/TtsService.cs` | ~280 | **TTS 引擎核心**。音色池管理、角色→音色分配（SHA256 哈希）、单段合成（3次重试）、MP3 帧级合并、Xing/VBRI 头帧过滤 |
| `ArkPlot.Novelizer/ChapterTtsGenerator.cs` | ~200 | **章节级 TTS 编排**。创建内部 TtsService + NovelAligner，按章节分组、逐段合成、per-segment 容错 |

### 🟡 对齐管线（TTS 前置）

| 文件 | 行数 | 职责 |
|---|---|---|
| `ArkPlot.Novelizer/NovelAligner.cs` | ~260 | 小说文本 ↔ DB 对话对齐，CharacterCode 提取，性别推断（PicDescription 关键词扫描） |
| `ArkPlot.Novelizer/DialogExtractor.cs` | ~130 | 按 `##` 拆章节，按 `""` 提取对话/旁白分段 |

### 🟠 CLI 入口层（3 个 Runner + 1 个对齐）

| 文件 | 职责 |
|---|---|
| `ArkPlot.Cli/Pipeline/TtsRunner.cs` | 主管线 Step 9，对原始 `FormattedTextEntry` 列表做 TTS |
| `ArkPlot.Cli/Pipeline/NovelTtsRunner.cs` | `tts-novel` 命令：读 aligned JSON → 生成单个 MP3 |
| `ArkPlot.Cli/Pipeline/ChapterTtsRunner.cs` | `chapter-tts` 命令：按章节生成，支持 `--limit N` / `--debug-voice` |
| `ArkPlot.Cli/Pipeline/AlignRunner.cs` | `align` 命令：对齐 + 音色预览 + 输出 JSON |
| `ArkPlot.Cli/Program.cs` | 命令路由：`align` / `tts-novel` / `chapter-tts` |
| `ArkPlot.Cli/CliOptions.cs` | `EnableTts = false` 开关 |
| `ArkPlot.Cli/Pipeline/CliPipeline.cs` | 主管线编排，Step 9 有条件调用 TtsRunner |

### 🔵 Web UI（测试用）

| 文件 | 职责 |
|---|---|
| `ArkPlot.WebDemo/Pages/Tts.razor` | Blazor 测试面板：单句合成 / 角色音色查询 / 批量合成 |

### ⚪ 辅助工具

| 文件 | 职责 |
|---|---|
| `VoiceLister/Program.cs` | 列出所有 `zh-*` Edge TTS 音色（独立 EXE） |

### ❌ 缺失部分

| 位置 | 现状 |
|---|---|
| `ArkPlot.Avalonia/` | **零 TTS 代码**，UI 中完全没有集成 TTS |
| 测试项目 | **零 TTS 测试**，没有任何单元/集成测试覆盖 |

---

## 三、音色池现状

| 类别 | 音色名 | 口音/特征 |
|---|---|---|
| **女声池 (4)** | `zh-CN-XiaoyiNeural` | 标准普通话，活泼 |
| | `zh-CN-liaoning-XiaobeiNeural` | 东北口音 |
| | `zh-TW-HsiaoChenNeural` | 台湾口音 |
| | `zh-TW-HsiaoYuNeural` | 台湾口音 |
| **男声池 (4)** | `zh-CN-YunxiNeural` | 阳光 |
| | `zh-CN-YunjianNeural` | 激情 |
| | `zh-CN-YunxiaNeural` | 可爱 |
| | `zh-CN-YunyangNeural` | 专业 |
| **旁白专用 (1)** | `zh-CN-XiaoxiaoNeural` | 独占，不参与角色分配 |
| **已排除** | `zh-CN-shaanxi-XiaoniNeural` | 陕西方言 — 用户明确排除 |
| | `zh-HK-*` | 粤语 — 用户明确排除 |

---

## 四、核心机制分析

### 4.1 角色→音色分配

```
GetVoiceForCharacter(name, gender?)
  ├─ name 为空 → DefaultVoice (XiaoxiaoNeural)
  ├─ 有 gender → isFemale = gender.Contains("女")
  ├─ 无 gender → fallback: SHA256(name) 奇偶性决定
  └─ voicePool[SHA256(name) % pool.Length]
```

- **缓存**：`ConcurrentDictionary<cacheKey, voice>`，key 包含 gender 防止冲突
- **一致性问题**：同一角色在同一进程内保证音色一致，但**跨进程**因 `ConcurrentDictionary` 不持久化，每次启动重新分配

### 4.2 性别推断

```
NovelAligner.InferGender(characterCode, picDescByCode)
  ├─ 从 PicDescription 表中查找角色对应描述
  ├─ 检查前 100 字符中的 "她"/"他"（优先）
  └─ 兜底搜索 "女性/女人/女孩/少女" 或 "男性/男人/男孩/少年"
```

**问题**：依赖 LLM 生成的图片描述来推断性别，间接且不可靠。

### 4.3 MP3 合并

`TtsService.MergeAudioFiles()` — 使用 NAudio `Mp3FileReader` 逐帧读取，跳过 Xing/VBRI 头帧，写入输出流。内存恒定（每帧 ~1-4KB）。

**注意**：`ChapterTtsGenerator` 中有一个**独立的** `MergeAudioFiles()` 方法，**没有** Xing/VBRI 过滤逻辑（与 `TtsService` 中的不一致）。

### 4.4 文本清洗

`ChapterTtsGenerator.SanitizeForTts()` — 去除 HTML/Markdown 标记、代码块、实体，截断到 2000 字符（EdgeTTS 单段上限约 3000 字符）。

**注意**：`NovelTtsRunner` 和 `TtsRunner` 各自**没有**文本清洗逻辑。

### 4.5 重试与容错

| 位置 | 重试策略 | 容错粒度 |
|---|---|---|
| `TtsService.SynthesizeAsync()` | 3 次，2s/4s/6s 退避 | 单段级别 |
| `ChapterTtsGenerator` | 依赖 TtsService 的 3 次重试 | per-segment catch，失败跳过不崩章节 |
| `NovelTtsRunner` | 依赖 TtsService 的 3 次重试 | **无** per-segment 容错，一段失败全崩 |
| `TtsRunner`（主管线） | 依赖 TtsService 的 3 次重试 | **无** per-segment 容错 |
| `IsTransientError()` | 匹配 connection/timeout/reset/websocket/SocketException/EdgeTTS 异常 | — |

---

## 五、CLI 命令汇总

| 命令 | 入口 | 输入 | 输出 |
|---|---|---|---|
| `chapter-tts <novel.md>` | ChapterTtsRunner | 小说化 md 文件 | 每章一个 `.mp3` |
| `chapter-tts <novel.md> --limit N` | 同上 | 同上 | 每章只合成前 N 段 |
| `chapter-tts <novel.md> --debug-voice` | 同上 | 同上 | 音色分配表（不合成） |
| `tts-novel <aligned.json>` | NovelTtsRunner | 对齐后的 JSON | 单个 `_tts.mp3` |
| `align <novel.md>` | AlignRunner | 小说化 md | `_aligned.json` + 控制台预览 |
| 主管线（EnableTts=true） | TtsRunner via CliPipeline | FormattedTextEntry | 每章一个 `.mp3` |

---

## 六、已识别的问题与痛点

### 🔴 严重问题

| # | 问题 | 影响 |
|---|---|---|
| 1 | **MP3 合并逻辑重复且不一致** | `TtsService` 有 Xing/VBRI 过滤，`ChapterTtsGenerator` 和 `NovelTtsRunner` 各自有独立实现但**缺少**该过滤，可能导致合并后时长显示错误 |
| 2 | **性别推断不可靠** | 依赖 LLM 生成的 PicDescription 文本搜索"他/她"，描述质量直接影响音色分配正确率 |
| 3 | **零测试覆盖** | TTS 模块没有任何单元测试/集成测试，重构无法做回归验证 |
| 4 | **容错策略不一致** | ChapterTtsGenerator 有 per-segment 容错，NovelTtsRunner 和 TtsRunner 没有 |

### 🟡 设计缺陷

| # | 问题 | 影响 |
|---|---|---|
| 5 | **音色分配不持久化** | 每次运行重新分配，同一角色可能在不同次运行中获得不同音色 |
| 6 | **三条 TTS 管线各自为政** | TtsRunner / NovelTtsRunner / ChapterTtsRunner 各自创建 TtsService、各自管理临时文件、各自实现合并，大量重复代码 |
| 7 | **文本清洗只在 ChapterTtsGenerator** | NovelTtsRunner 和 TtsRunner 直接传原文给 EdgeTTS，可能因 Markdown/HTML 标记导致语音异常 |
| 8 | **WebDemo 音色列表过时** | `Tts.razor` 下拉框仍包含 `zh-CN-shaanxi-XiaoniNeural`（已排除的陕西口音） |
| 9 | **旁白和角色共享音色空间** | `DefaultVoice = NarratorVoice`，空角色名的旁白也用 XiaoxiaoNeural；但 ChapterTtsGenerator 的旁白走 `GetNarratorVoice()` 而 NovelTtsRunner 的旁白走 `GetVoiceForGender(null)` → 返回 DefaultVoice，逻辑不一致 |
| 10 | **请求间隔硬编码** | `TtsService` 800ms，`ChapterTtsGenerator` 2000ms，`NovelTtsRunner` 800ms — 无统一策略 |

### 🟠 功能缺失

| # | 缺失 | 说明 |
|---|---|---|
| 11 | **Avalonia UI 无 TTS** | 桌面端完全没有 TTS 功能入口 |
| 12 | **无音色自定义** | 用户无法手动指定某角色使用特定音色 |
| 13 | **无进度/取消支持** | 长章节合成时没有进度回调，取消只能靠 Ctrl+C |
| 14 | **无离线/缓存机制** | 同一文本重复合成会重新请求 EdgeTTS，无本地缓存 |
| 15 | **主管线 TTS 是原始剧情，非小说化文本** | `TtsRunner` 处理的是未小说化的 `FormattedTextEntry.Dialog`，与小说化管线的 `ChapterTtsGenerator` 输出质量差异大 |

---

## 七、代码复用关系图

```
TtsService (ArkPlot.Core)
├── GetVoiceForCharacter()   ← ChapterTtsGenerator, NovelTtsRunner, TtsRunner, AlignRunner
├── GetNarratorVoice()       ← ChapterTtsGenerator
├── GetFallbackVoice()       ← ChapterTtsGenerator
├── GetVoiceForGender()      ← NovelTtsRunner
├── SynthesizeAsync()        ← ChapterTtsGenerator, NovelTtsRunner, TtsService.GenerateChapterAudioAsync
├── GenerateChapterAudioAsync() ← TtsRunner, WebDemo Tts.razor
└── MergeAudioFiles()        ← 仅 TtsService 内部使用（私有方法）

NovelAligner (ArkPlot.Novelizer)
├── AlignByFileNameAsync()   ← ChapterTtsGenerator, AlignRunner
└── InferGender()            ← NovelAligner 内部

DialogExtractor (ArkPlot.Novelizer)
└── ExtractChapters()        ← NovelAligner 内部
```

---

## 八、重构需求决策记录

| 维度 | 决策 |
|---|---|
| 架构方案 | **方案 A**：新建 `ArkPlot.Tts` 独立项目，Core 完全不含 TTS 代码 |
| TTS 引擎扩展 | `ITtsEngine` 接口放在 ArkPlot.Tts 内部，未来可加 Azure/本地引擎 |
| 对齐模块 | NovelAligner + DialogExtractor **搬到 ArkPlot.Tts** |
| 性别推断 | 保留 PicDescription 推断，加 `character_overrides.json` 兜底 |
| TTS 缓存 | (text, voice, rate, volume) SHA256 → `_tts_cache/` 项目专用目录 |
| 音色持久化 | SQLite `CharacterVoiceMap` 表 |
| 音频合并 | 纯帧拼接，不加淡入淡出，统一收归 Tts 项目 |
| 文本清洗 | 统一收归 Tts 项目，所有管线共用 |
| 测试策略 | 每步都补单元测试 |
| Avalonia 集成 | ❌ 本次不含 |
| WebDemo | ⏸️ 暂不更新 |

### 重构后依赖关系

```
ArkPlot.Cli ──→ ArkPlot.Tts ──→ ArkPlot.Core（Model / DB 基础设施）
  └─→ ArkPlot.Core                  ↑
  └─→ ArkPlot.Novelizer ──→ ArkPlot.Core
```

- `ArkPlot.Core`：零 TTS 代码，纯基础设施
- `ArkPlot.Novelizer`：LLM 小说化 + 小说后处理（章节拆分、角色表、质量评估）
- `ArkPlot.Tts`：对齐 + 音色管理 + 合成引擎 + 缓存 + 编排器，自包含闭环

---

## 九、重构 Todo List

### Phase 0: 项目脚手架 + 测试骨架

- [ ] **0.1** 创建 `ArkPlot.Tts` 项目（.NET 类库），添加到 `ArkPlot.sln`
- [ ] **0.2** 设置 `ArkPlot.Tts` 的 NuGet 依赖：`EdgeTTS.DotNet 0.4.0` + `NAudio 2.3.0`，项目引用 `ArkPlot.Core`
- [ ] **0.3** 在测试项目中创建 `Tts/` 测试目录，搭建测试基础设施
- [ ] **0.4** 创建 `MockTtsEngine`：实现 `ITtsEngine`，`SynthesizeAsync` 写固定内容文件，不调网络
- [ ] **0.5** 验证项目编译 + 至少一个空测试 pass

### Phase 1: 定义 ITtsEngine 接口 + 迁移 EdgeTtsEngine

- [ ] **1.1** 在 `ArkPlot.Tts` 中定义 `ITtsEngine` 接口：`SynthesizeAsync(text, voice, outputPath)`, `ListVoicesAsync()`
- [ ] **1.2** 将 `TtsService` 中的 EdgeTTS 合成逻辑提取为 `ArkPlot.Tts/Engines/EdgeTtsEngine.cs`，实现 `ITtsEngine`
- [ ] **1.3** 补充单元测试：MockTtsEngine 合成 → 验证输出文件存在且非空
- [ ] **1.4** 编译通过 + 测试通过

### Phase 2: 迁移音色池 + 音色分配逻辑

- [ ] **2.1** 将 `TtsService` 的音色池（FemaleVoices / MaleVoices / NarratorVoice）搬到 `ArkPlot.Tts/VoicePool.cs`
- [ ] **2.2** 将 `GetVoiceForCharacter()` / `GetVoiceForGender()` / `GetNarratorVoice()` / `GetFallbackVoice()` 搬到 `ArkPlot.Tts/VoiceManager.cs`
- [ ] **2.3** 将 SHA256 稳定哈希逻辑搬过去
- [ ] **2.4** 补充单元测试：女角色→女声、男角色→男声、空角色名→旁白、哈希一致性
- [ ] **2.5** 编译通过 + 测试通过

### Phase 3: 统一 MP3 合并 + 文本清洗

- [ ] **3.1** 将 `TtsService.MergeAudioFiles()` 搬到 `ArkPlot.Tts/AudioMerger.cs`，改为 `public static`，保留 Xing/VBRI 过滤
- [ ] **3.2** 将 `ChapterTtsGenerator.SanitizeForTts()` 搬到 `ArkPlot.Tts/TextSanitizer.cs`，改为 `public static`
- [ ] **3.3** 补充测试：合并 2 个 MP3 → 验证帧数；HTML/Markdown 清洗；超长截断
- [ ] **3.4** 编译通过 + 测试通过

### Phase 4: 迁移对齐模块

- [ ] **4.1** 将 `NovelAligner.cs` 从 `ArkPlot.Novelizer` 搬到 `ArkPlot.Tts/Alignment/NovelAligner.cs`
- [ ] **4.2** 将 `DialogExtractor.cs` 从 `ArkPlot.Novelizer` 搬到 `ArkPlot.Tts/Alignment/DialogExtractor.cs`
- [ ] **4.3** 将 `AlignmentEntry` / `AlignmentStats` / `NovelSegment` / `NovelChapter` 等 record 搬到 `ArkPlot.Tts/Alignment/`
- [ ] **4.4** 更新所有引用：`ArkPlot.Cli` 和测试项目中的 `using ArkPlot.Novelizer` → `using ArkPlot.Tts.Alignment`
- [ ] **4.5** 更新 `ArkPlot.Avalonia.Tests/DialogExtractorTests.cs` 的命名空间引用
- [ ] **4.6** 编译通过 + 现有测试通过

### Phase 5: 性别兜底 — JSON 配置文件

- [ ] **5.1** 设计 `character_overrides.json` 格式：`{ "角色名": { "gender": "女" } }`
- [ ] **5.2** 在 `ArkPlot.Tts` 中创建 `GenderOverrideProvider`：加载 JSON，提供 `GetOverride(name)` 方法
- [ ] **5.3** 修改 `NovelAligner.InferGender()`：优先查 override → fallback PicDescription 推断
- [ ] **5.4** 补充单元测试：有 override 返回 override 值；无 override 走推断；JSON 不存在不报错
- [ ] **5.5** 编译通过 + 测试通过

### Phase 6: 音色分配持久化 — SQLite

- [ ] **6.1** 在 `ArkPlot.Tts` 中创建 `CharacterVoiceMap` 实体：`CharacterName`, `Gender`, `Voice`, `AssignedAt`
- [ ] **6.2** 在 `DbFactory` 的 CodeFirst 中注册 `CharacterVoiceMap` 表
- [ ] **6.3** 修改 `VoiceManager`：接受 `SqlSugarClient`（可选），首次分配写 DB，后续从 DB 读取
- [ ] **6.4** 补充单元测试：首次分配 → 写入 DB → 二次读取命中同一音色
- [ ] **6.5** 编译通过 + 测试通过

### Phase 7: TTS 缓存层

- [ ] **7.1** 在 `ArkPlot.Tts` 中创建 `TtsCacheService`：
  - `GetCacheKey(text, voice, rate, volume)` → SHA256
  - `TryGetCachedAudio(key) → (bool, string?)` 查找 `_tts_cache/{key}.mp3`
  - `SaveToCache(key, sourcePath)` 复制到缓存目录
- [ ] **7.2** 缓存目录默认为输出目录下 `_tts_cache/`，支持构造函数覆盖
- [ ] **7.3** 补充单元测试：首次写入 → 二次命中；不同文本不同 key；缓存目录不存在时自动创建
- [ ] **7.4** 编译通过 + 测试通过

### Phase 8: 统一编排器 — TtsPipeline

- [ ] **8.1** 在 `ArkPlot.Tts` 中创建 `TtsPipeline` 类，统一入口：
  ```csharp
  public class TtsPipeline
  {
      public TtsPipeline(ITtsEngine engine, VoiceManager voices,
                         TtsCacheService cache, AudioMerger merger);

      public async Task<TtsPipelineResult> GenerateAsync(
          TtsRequest request, CancellationToken ct, IProgress<string>? progress = null);
  }

  public enum TtsInputMode { NovelChapter, AlignedJson, RawEntries }

  public record TtsRequest(
      TtsInputMode Mode,
      string InputPath,
      string OutputDir,
      int? SegmentLimit = null,
      bool DebugVoiceOnly = false
  );
  ```
- [ ] **8.2** 内部编排：文本清洗 → 音色选择 → 缓存查询 → 合成（未命中时）→ 合并
- [ ] **8.3** 统一 per-segment 容错：单段失败跳过，不崩全章
- [ ] **8.4** 统一请求间隔：可配置，默认 1000ms
- [ ] **8.5** 进度回调：`IProgress<string>` 日志报告
- [ ] **8.6** 补充单元测试：MockTtsEngine + 验证完整流程（含缓存命中场景）
- [ ] **8.7** 编译通过 + 测试通过

### Phase 9: CLI Runner 瘦身

- [ ] **9.1** 重写 `ChapterTtsRunner`：解析参数 → 构造 `TtsPipeline` → 调用 `GenerateAsync()`
- [ ] **9.2** 重写 `NovelTtsRunner`：同上
- [ ] **9.3** 重写 `TtsRunner`（主管线 Step 9）：同上
- [ ] **9.4** 修改 `CliPipeline.cs`：移除对旧 `TtsService` 的引用
- [ ] **9.5** 修改 `ArkPlot.Cli.csproj`：添加 `ArkPlot.Tts` 项目引用
- [ ] **9.6** 验证三个 CLI 命令功能不变：`chapter-tts` / `tts-novel` / 主管线 TTS
- [ ] **9.7** 编译通过 + 测试通过

### Phase 10: 清理旧代码 + 从 Core 移除 TTS

- [ ] **10.1** 从 `ArkPlot.Core/Services/TtsService.cs` 删除整个文件
- [ ] **10.2** 从 `ArkPlot.Core.csproj` 移除 `EdgeTTS.DotNet` 和 `NAudio` 依赖
- [ ] **10.3** 删除 `ChapterTtsGenerator` 中的私有 `MergeAudioFiles()` 和 `SanitizeForTts()`
- [ ] **10.4** 删除 `NovelTtsRunner` 中的私有 `MergeAudioFiles()`
- [ ] **10.5** 删除 `ArkPlot.Novelizer` 中的 `NovelAligner.cs` 和 `DialogExtractor.cs`（已搬走）
- [ ] **10.6** 更新 `ArkPlot.Novelizer.csproj`：确认不再需要 `ArkPlot.Core` 的 TTS 相关引用
- [ ] **10.7** 全量编译通过

### Phase 11: 文档收尾

- [ ] **11.1** 更新本文档：补充"重构后架构"章节，标记现状部分为"重构前"
- [ ] **11.2** 确认 `VoiceLister` 工具仍可独立运行（可能需要改为引用 ArkPlot.Tts）
- [ ] **11.3** 全量测试 pass

---

## 十、依赖关系与执行顺序

```
Phase 0  (脚手架 + 测试骨架)
  │
  ├──→ Phase 1  (ITtsEngine + EdgeTtsEngine)
  │     └──→ Phase 2  (音色池 + 分配逻辑)
  │           └──→ Phase 3  (MP3 合并 + 文本清洗)
  │                 │
  ├──→ Phase 4  (迁移对齐模块)  ←── 可独立并行
  │     └──→ Phase 5  (性别兜底 JSON)
  │           └──→ Phase 6  (音色持久化 SQLite)
  │                 │
  │   Phase 7  (TTS 缓存) ←── 依赖 Phase 1-3
  │                 │
  └──→ Phase 8  (统一编排器 TtsPipeline) ←── 依赖 Phase 1-7 全部
        └──→ Phase 9  (CLI Runner 瘦身)
              └──→ Phase 10 (清理旧代码)
                    └──→ Phase 11 (文档收尾)
```

---

## 十一、预期产出

### 新增文件（ArkPlot.Tts 项目）

| 文件 | 说明 |
|---|---|
| `ArkPlot.Tts/ArkPlot.Tts.csproj` | 项目文件 |
| `ArkPlot.Tts/ITtsEngine.cs` | TTS 引擎接口 |
| `ArkPlot.Tts/Engines/EdgeTtsEngine.cs` | EdgeTTS 实现 |
| `ArkPlot.Tts/VoicePool.cs` | 音色池定义 |
| `ArkPlot.Tts/VoiceManager.cs` | 音色分配（哈希 + DB 持久化） |
| `ArkPlot.Tts/AudioMerger.cs` | MP3 帧级合并 |
| `ArkPlot.Tts/TextSanitizer.cs` | 文本清洗 |
| `ArkPlot.Tts/TtsCacheService.cs` | 缓存层 |
| `ArkPlot.Tts/TtsPipeline.cs` | 统一编排器 |
| `ArkPlot.Tts/Alignment/NovelAligner.cs` | 对齐（从 Novelizer 搬来） |
| `ArkPlot.Tts/Alignment/DialogExtractor.cs` | 对话提取（从 Novelizer 搬来） |
| `ArkPlot.Tts/Alignment/Models.cs` | AlignmentEntry / NovelSegment 等 record |
| `ArkPlot.Tts/GenderOverrideProvider.cs` | 性别兜底 |
| `ArkPlot.Tts/Model/CharacterVoiceMap.cs` | 音色映射 DB 实体 |
| `character_overrides.json` | 性别兜底配置模板 |

### 新增测试

| 文件 | 覆盖 |
|---|---|
| `Tests/Tts/VoiceManagerTests.cs` | 音色分配逻辑 |
| `Tests/Tts/AudioMergerTests.cs` | MP3 合并 |
| `Tests/Tts/TextSanitizerTests.cs` | 文本清洗 |
| `Tests/Tts/TtsCacheTests.cs` | 缓存命中/未命中 |
| `Tests/Tts/TtsPipelineTests.cs` | 端到端编排（Mock） |
| `Tests/Tts/GenderOverrideTests.cs` | 性别兜底 |

### 重构/删除文件

| 文件 | 动作 |
|---|---|
| `ArkPlot.Core/Services/TtsService.cs` | **删除** |
| `ArkPlot.Core/ArkPlot.Core.csproj` | 移除 EdgeTTS.DotNet + NAudio |
| `ArkPlot.Novelizer/NovelAligner.cs` | **删除**（搬走） |
| `ArkPlot.Novelizer/DialogExtractor.cs` | **删除**（搬走） |
| `ArkPlot.Novelizer/ChapterTtsGenerator.cs` | **删除**（职责由 TtsPipeline 接管） |
| `ArkPlot.Cli/Pipeline/ChapterTtsRunner.cs` | 瘦身为 TtsPipeline 调用 |
| `ArkPlot.Cli/Pipeline/NovelTtsRunner.cs` | 瘦身为 TtsPipeline 调用 |
| `ArkPlot.Cli/Pipeline/TtsRunner.cs` | 瘦身为 TtsPipeline 调用 |
| `ArkPlot.Cli/Pipeline/AlignRunner.cs` | 改用 ArkPlot.Tts.Alignment |
| `ArkPlot.Cli/Pipeline/CliPipeline.cs` | Step 9 改用 TtsPipeline |
| `ArkPlot.Cli/ArkPlot.Cli.csproj` | 添加 ArkPlot.Tts 引用 |

---

*重构已于 2026-06-05 完成。Phase 0→10 全部执行，76 个单元测试全绿，全解决方案编译通过。*

---

## 十二、重构后架构（2026-06-05）

### 依赖关系

```
ArkPlot.Cli ──→ ArkPlot.Tts ──→ ArkPlot.Core（Model / DB / 基础设施）
  └─→ ArkPlot.Core                  ↑ 零 TTS 代码
  └─→ ArkPlot.Novelizer ──→ ArkPlot.Core
```

### ArkPlot.Tts 项目文件结构

```
ArkPlot.Tts/
├── ITtsEngine.cs                  ← TTS 引擎接口（扩展点：Azure/本地引擎）
├── Engines/
│   └── EdgeTtsEngine.cs           ← EdgeTTS 实现
├── VoicePool.cs                   ← 音色池定义（4女+4男+1旁白）
├── VoiceManager.cs                ← 音色分配（SHA256 + DB 持久化）
├── AudioMerger.cs                 ← MP3 帧级合并（Xing/VBRI 过滤）
├── TextSanitizer.cs               ← 文本清洗（HTML/Markdown/截断）
├── TtsCacheService.cs             ← 缓存层（SHA256 key → _tts_cache/）
├── TtsPipeline.cs                 ← 请求/结果类型定义
├── TtsPipelineOrchestrator.cs     ← 统一编排器
├── GenderOverrideProvider.cs      ← 性别兜底（JSON 配置）
├── ChapterTtsGenerator.cs         ← [已删除] 由 TtsPipeline 取代
├── Alignment/
│   ├── Models.cs                  ← AlignmentEntry / NovelSegment / NovelChapter
│   ├── DialogExtractor.cs         ← 对话提取（从 Novelizer 搬来）
│   └── NovelAligner.cs            ← 对齐引擎（从 Novelizer 搬来）
└── Model/
    └── CharacterVoiceMap.cs       ← 音色映射 DB 实体
```

### 已删除的文件

| 文件 | 说明 |
|---|---|
| `ArkPlot.Core/Services/TtsService.cs` | TTS 引擎核心 → 拆为 ITtsEngine + VoiceManager + AudioMerger |
| `ArkPlot.Novelizer/NovelAligner.cs` | → 搬到 ArkPlot.Tts/Alignment/ |
| `ArkPlot.Novelizer/DialogExtractor.cs` | → 搬到 ArkPlot.Tts/Alignment/ |
| `ArkPlot.Novelizer/ChapterTtsGenerator.cs` | → 由 TtsPipeline 取代 |

### 重构解决的 15 个问题

| # | 问题 | 状态 |
|---|---|---|
| 1 | MP3 合并逻辑重复且不一致 | ✅ 统一为 `AudioMerger.MergeFiles()` |
| 2 | 性别推断不可靠 | ✅ 加 `character_overrides.json` 兜底 |
| 3 | 零测试覆盖 | ✅ 76 个单元测试 |
| 4 | 容错策略不一致 | ✅ TtsPipeline 统一 per-segment 容错 |
| 5 | 音色分配不持久化 | ✅ SQLite `CharacterVoiceMap` 表 |
| 6 | 三条管线各自为政 | ✅ 统一为 `TtsPipeline` |
| 7 | 文本清洗只在一处 | ✅ `TextSanitizer` 全局共用 |
| 8 | WebDemo 音色列表过时 | ✅ 移除陕西口音，更新为 Tts 新类型 |
| 9 | 旁白/角色逻辑不一致 | ✅ TtsPipeline 统一处理 |
| 10 | 请求间隔硬编码 | ✅ `TtsRequest.RequestDelayMs` 可配置 |
| 11 | Avalonia 无 TTS | ⏸️ 本期不含（架构已预留） |
| 12 | 无音色自定义 | ⏸️ 留后续 |
| 13 | 无进度/取消支持 | ✅ `IProgress<string>` + `CancellationToken` |
| 14 | 无缓存机制 | ✅ `TtsCacheService` |
| 15 | Core 耦合 TTS 代码 | ✅ Core 零 TTS 代码 |

### 测试覆盖（76 个）

| 测试文件 | 数量 | 覆盖 |
|---|---|---|
| MockTtsEngineTests | 4 | Mock 引擎基本行为 |
| EdgeTtsEngineTests | 10 | IsTransientError 判断 |
| VoiceManagerTests + VoicePoolTests | 20 | 音色分配、哈希一致性、DB 持久化 |
| VoiceManagerDbTests | 4 | SQLite 持久化验证 |
| TextSanitizerTests | 11 | HTML/Markdown 清洗、截断 |
| AudioMergerTests | 3 | 输入验证、目录创建 |
| GenderOverrideTests | 9 | JSON 加载、override 优先、容错 |
| TtsCacheTests | 10 | 缓存命中/未命中、覆盖、空文件 |
| TtsPipelineTests | 5 | 端到端编排、缓存、调试模式、容错 |

### 后续扩展点

1. **新 TTS 引擎**：实现 `ITtsEngine` 接口（如 `AzureTtsEngine`、`CoquiTtsEngine`）
2. **Avalonia 集成**：引用 ArkPlot.Tts，用 TtsPipeline 提供 UI 入口
3. **音色自定义**：扩展 `character_overrides.json` 支持 `voice` 字段直接指定音色
4. **批量导出**：TtsPipeline 已支持多章节输出，可加 EPUB 打包

---

## 十三、Avalonia UI 集成计划（Phase 12）

### 入口方式

主窗口新增 **[TTS语音生成]** 按钮（与 [开始] [设置] 平级），点击时：
1. 扫描当前活动输出目录下是否有 `*_novel_*.md` 文件
2. 无 → Toast 提示"请先生成小说化文本"
3. 有 → 打开独立的 `TtsWindow` 窗口

### TtsWindow 布局（1100×750）

```
┌─── 左面板 (320px 固定) ──┐  ┌─── 右面板 (~780px 可拉伸) ────────────────────┐
│                           │  │                                                │
│  ┌─────────────────────┐  │  │  章节选择 ComboBox + ◄ 1/5 ► 翻页             │
│  │                     │  │  │  搜索框 (跨章节)                               │
│  │   [角色立绘]         │  │  │                                                │
│  │   宽<高 竖向裁切     │  │  │  片段表格 (#/角色/类型/音频)                   │
│  │   居中 + 上下渐变    │  │  │    - 已生成行: ▶按钮 + 波形条 + 时长          │
│  │                     │  │  │    - 未生成行: 灰色虚线                         │
│  │  当前说话人: 阿米娅  │  │  │    - 悬浮🖼角色行 → 立绘 Tooltip              │
│  └─────────────────────┘  │  │                                                │
│                           │  │  ── 播放 ──                                    │
│  小说文件 (勾选)           │  │  [ ▶ 从选中行开始连播 ]  (大按钮)              │
│  输出目录                  │  │                                                │
│                           │  │  [📥 导出当前章节]  [📥 导出全部章节]          │
│  ── 音色配置 ──           │  │                                                │
│  角色名 + 性别 + [▾音色]  │  │  ── 场景 Gallery ──                            │
│  (下拉菜单可手动改)       │  │  │ 上文(模糊)                                   │
│                           │  │  │▌10%│    [中心背景图 100%]    │▌10%│          │
│  ── 进度 ──               │  │  │ 下文(模糊)                                   │
│  当前章节 + 段数进度条     │  │  悬浮背景图 → PicDescription Tooltip           │
│  总进度                    │  │                                                │
│  [ ▶ 开始生成 ]            │  │                                                │
│  [ ■ 取消 ]               │  │                                                │
│                           │  │                                                │
│  ── 日志 ──               │  │                                                │
└───────────────────────────┘  └────────────────────────────────────────────────┘
```

### Gallery 联动规则

- 同一背景图时：播放新片段 → Gallery 不动（上下文文本固定，展示这张图前后大家在说什么）
- 播放到新背景图 → Gallery 平滑切换，上下文跟着更新
- 手动点击左右裁剪图切换 → 音频播放不受影响
- PicDescription 改为悬浮背景图时飘出的 Tooltip

### 立绘规则

- 宽<高的竖向图片，`Stretch=UniformToFill` 水平居中裁切
- 上下边缘渐变淡出到面板背景
- 连播时随当前说话人实时切换（带淡入淡出过渡动画）
- 旁白段无立绘时显示通用图标

### 上下文文本来源

- 来自 `FormattedTextEntry.Dialog`（非小说化文本）
- 每张背景图关联 2 句上文 + 2 句下文
- 距当前段越远 → Opacity 越低 + FontSize 越小 + Blur

### 执行步骤

| Phase | 内容 |
|---|---|
| 1 | TtsWindow 脚手架 + 左右分栏布局 |
| 2 | 左面板：小说文件扫描 + 输出目录 + 音色配置（下拉菜单） |
| 3 | 左面板：角色立绘显示（裁切居中 + 渐变淡出 + 说话人切换动画） |
| 4 | 左面板：进度条 + 开始生成/取消按钮 + 日志 |
| 5 | 右面板：片段表格 + 章节翻页 + 搜索框 |
| 6 | 右面板：大播放按钮（从选中行连播）+ 播放状态高亮 |
| 7 | 右面板：导出当前章节/导出全部章节按钮 |
| 8 | 右面板：场景 Gallery（中心100% + 左右10%裁剪 + PicDesc tooltip + 上下文） |
| 9 | 连播联动：音频→表格高亮→立绘切换→Gallery切换 |
| 10 | 主窗口入口：[TTS语音生成] 按钮 + 前置检查 |
| 11 | 编译 + 测试 + 收尾 |
