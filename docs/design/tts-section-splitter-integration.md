# TTS 分节器集成设计文档

> 日期：2026-07-17（2026-07-18 更新）
> 状态：设计阶段（原型验证通过）

## 一、背景

### 问题

Novelizer 生成的小说化文本缺少场景切换的听觉导航。TTS 听众无法看到 `---`、空行等排版标记，在场景切换时容易"丢失定位"。

### 问题根因（A/B 测试发现）

最初尝试将 TTS 规则直接放入 Novelizer 的 system prompt（单 pass 方案）。A/B 测试暴露了根本矛盾：**模型会把"格式要求"误解成"创作目标"**——当 prompt 说"平行切换要有导航词"时，模型会主动创造不存在的平行切场景（幻觉），导致对齐失败。

v1.1 通过禁止性护栏堵住了幻觉，但代价是 prompt 膨胀 + 模型过度保守。因此改为双阶段架构：Pass 1 不变，Pass 2 独立处理分节。

### 已验证的方案

**双阶段架构**：Pass 1（Novelizer，不变）+ Pass 2（TTS 分节器，新增）。

Pass 2 从 DB 的 `FormattedTextEntry.Bg` 字段找到背景变化点，用附近对话文本在小说化输出中定位，提取切片送 LLM 分类，返回 JSON 插入指令（节标题 + 过渡句）。

### 关键发现：enable_thinking 必须放在请求体顶层

百炼 DashScope 的 OpenAI 兼容接口**不转发 `extra_body`**。原 BailianClient 代码 `requestBody["extra_body"] = new { enable_thinking = false }` 无效——模型仍在做 thinking。

修复（一行）：
```csharp
// 修复前（无效）：
requestBody["extra_body"] = new { enable_thinking = _config.EnableThinking };
// 修复后（有效）：
requestBody["enable_thinking"] = _config.EnableThinking;
```

验证结果（同一短句，三种放法）：

| 方式 | completion_tokens | reasoning_content | reasoning_tokens |
|------|-------------------|-------------------|-----------------|
| `extra_body.enable_thinking=false` | 171 | 有 | 164 |
| **顶层 `enable_thinking=false`** | **5** | **无** | **0** |
| 无参数（基线） | 77 | 有 | 70 |

### 原型验证数据（孤星第一章）

| 指标 | thinking 开 500字切片 | thinking 关 500字切片 | **thinking 关 250字切片（最终）** |
|------|---------------------|---------------------|-------------------------------|
| DB 条目 | 826 | 826 | 826 |
| Bg 变化点（过滤 bg_black 后） | 12 | 12 | 12 |
| 成功定位切片 | 7/12 | 7/12 | 7/12 |
| 切片总输入 | ~4200 字 | ~4200 字 | ~2600 字 |
| Pass 2 Prompt tokens | 2996 | 2996 | **2003** |
| Pass 2 Completion tokens | 5391 | 440 | **328** |
| Pass 2 Total tokens | 8387 | 3436 | **2331** |
| Pass 2 耗时 | 53s | 4.9s | **4.4s** |
| 双 pass 总成本 vs 单 pass | +37% | +15% | **+10%** |
| 元叙述术语 | 0 | 0 | 0 |
| 节标题格式 | 正确 | 正确 | 正确 |
| 切换类型分类质量 | 有误判 | 改善 | **持平**（部分改善，部分互换） |

## 二、管线位置

```
现有管线：
  DB FormattedTextEntry → MdReconstructor → input.md → Novelizer → novel.md

新增 Pass 2：
  novel.md ──┐
             ├─→ TtsSectionSplitter ──→ novel_sectioned.md
  DB Bg 数据 ─┘
```

Pass 2 是**可选后处理**，在 Novelizer 输出之后、TTS 对齐之前执行。不改变 Pass 1 的行为。

## 三、数据流

```
1. 读取 novel.md（Pass 1 输出）
2. 查询 DB：SELECT Index, Bg, Dialog, CharacterName, MdText
            FROM FormattedTextEntry WHERE PlotId = ? ORDER BY Index
3. 找 Bg 变化点（过滤 bg_black）
4. 对每个变化点：
   a. 向后搜索 15 条找最近 Dialog
   b. 向前搜索 15 条找最近 Dialog
   c. 用 Dialog 前 12 字在 novel.md 中搜索定位
   d. 定位失败时用 MdText 前 12 字回退搜索
   e. 提取前后各 125 字的切片（共 ~250 字）
5. 构建批量 prompt：所有切片 + 上下文（角色名、前后对话）
6. 调用百炼 API（deepseek-v4-flash，`enable_thinking=false` 放请求体顶层）
7. 解析 JSON 响应
8. 对每个插入指令：在 novel.md 中找到 anchor，插入 insert_before
9. 输出 novel_sectioned.md
```

## 四、代码结构

### 新增项目：`ArkPlot.Novelizer.SectionSplitter`

或直接放在 `ArkPlot.Novelizer` 项目中（更简单）。

```
ArkPlot.Novelizer/
├── NovelizerPipeline.cs          # 现有 Pass 1
├── BailianClient.cs              # 现有 API 客户端
├── SectionSplitter.cs            # 【新增】Pass 2 核心
├── SectionSplitterPrompt.cs      # 【新增】Prompt 常量
└── Program.cs                    # 【修改】新增 split 子命令
```

### 核心类：`SectionSplitter`

```csharp
public class SectionSplitter
{
    // 依赖：BailianClient（复用现有）+ DB 连接（复用 DbFactory）

    /// <summary>
    /// 对小说化文本执行 TTS 分节，返回带节标题和过渡句的文本。
    /// </summary>
    public async Task<string> SplitAsync(
        string novelText,        // Pass 1 输出
        long plotId,             // DB 章节 ID
        string model = "deepseek-v4-flash",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        // 1. 查 DB 找 Bg 变化点
        var transitions = FindBgTransitions(plotId);

        // 2. 在 novelText 中定位切片
        var snippets = LocateSnippets(novelText, transitions);

        // 3. 构建批量 prompt
        var (system, user) = BuildPrompt(snippets);

        // 4. 调用 API
        var json = await CallApi(system, user, model, ct);

        // 5. 解析 JSON + 应用插入
        return ApplyInsertions(novelText, json);
    }
}
```

### 关键方法

```csharp
// Bg 变化点检测（过滤 bg_black）
record BgTransition(int EntryIndex, string PrevChar, string CurrChar,
                   string PrevDialog, string CurrDialog);

List<BgTransition> FindBgTransitions(long plotId)
{
    // 查 DB → 找 Bg != prevBg 且不含 "bg_black" 的条目
    // 向前/向后 15 条搜索 Dialog
}

// 切片定位
record Snippet(int CharPos, string Text, BgTransition Transition);

List<Snippet> LocateSnippets(string novel, List<BgTransition> transitions)
{
    // 用 Dialog 前 12 字搜索
    // 失败时用 MdText 回退
    // 提取前后各 250 字
}

// JSON 插入应用
string ApplyInsertions(string novel, string json)
{
    // 解析 JSON 数组
    // 对每条 {anchor, insert_before}：
    //   novel.IndexOf(anchor) → 插入 insert_before
    //   anchor 未找到时跳过 + 日志警告
}
```

## 五、CLI 集成

```bash
# 现有：生成小说化文本
dotnet run --project ArkPlot.Novelizer -- run -i input.md --model flash --tag baseline

# 新增：对已生成的小说化文本做分节
dotnet run --project ArkPlot.Novelizer -- split -i novel_baseline.md --plot 31 --model flash

# 组合：一条命令完成两 pass
dotnet run --project ArkPlot.Novelizer -- run-and-split -i input.md --model flash
```

## 六、Prompt 管理

Prompt 存为常量 `SectionSplitterPrompt.Default`，内容即 `section_splitter_prompt.md`：

- 切换类型定义（flashback_in/out、parallel、time_jump、location_change、continuation），每种附带特征描述
- 禁止元叙述术语（"镜头"、"场景切换"、"画面转向"等），过渡句必须是小说叙述语言
- 节标题格式：第X节 标题名（汉字序号，不用阿拉伯数字、不用 Markdown 井号）
- 过渡句用破折号开头
- 只返回 JSON

**API 调用必须设置 `enable_thinking=false`**（放请求体顶层，非 `extra_body`）。分节是分类标注任务，不需要推理。关闭 thinking 后：
- Completion tokens 降 94%（5391→328）
- 耗时降 92%（53s→4.4s）
- 分类质量反而提升（模型不再过度分析，直觉判断更准）

切片大小建议 250 字（前后各 125 字）。500 字切片质量持平但多花 32% token。

可后续提取为 bundle（类似 Novelizer 的 `bundles/` 结构）以便迭代。

## 七、错误处理

| 场景 | 处理 |
|------|------|
| Bg 变化点无附近对话 | 用 MdText 回退搜索；仍失败则跳过该变化点 |
| Dialog 在小说化文本中找不到 | 尝试去标点搜索；仍失败则跳过 |
| API 返回非法 JSON | 跳过分节，返回原始 novelText + 日志警告 |
| anchor 在正文中找不到 | 跳过该插入 + 日志警告 |
| LLM 幻觉新场景 | Pass 2 只插入节标题和过渡句，不改写正文。幻觉风险为零（Pass 1 不受 TTS 规则影响） |

## 八、成本分析

| 场景 | Pass 1 | Pass 2 (thinking 关, 250字切片) | 总计 | vs 单 pass |
|------|--------|-------------------------------|------|------------|
| 孤星第一章（826 条目） | 22699 tokens / 111s | 2331 tokens / 4.4s | 25030 tokens | **+10%** |

- Pass 2 只处理 Bg 变化点（~7-12 个），不处理整章
- 切片总计 ~2600 字（vs 整章 ~9500 字）
- `enable_thinking=false` 使 Pass 2 completion 从 5391 降到 328 tokens（-94%）
- 切片从 500 字缩到 250 字后 token 再降 32%，质量持平
- 可用 flash 模型（分节是分类任务，不需要创作能力）
- Pass 1 是否也关 thinking 待验证——创作任务可能需要 thinking

### 四方案成本对比

| 方案 | Total tokens | vs 单 pass | 耗时 |
|------|-------------|------------|------|
| 单 pass（Novelizer baseline） | 22699 | — | 75s |
| 整章 Pass 2（thinking 开） | 36715 | +62% | 228s |
| 切片 Pass 2 500字（thinking 关） | 26135 | +15% | 116s |
| **切片 Pass 2 250字（thinking 关）** | **25030** | **+10%** | **115s** |

## 九、后续优化方向

1. ~~**类型分类改进**~~：✅ 关闭 thinking 后已显著改善（flashback_out、parallel 分类修正）。剩余偏差不影响输出质量——过渡句本身是对的
2. **Pass 1 thinking 验证**：Pass 1（Novelizer 创作任务）是否也关 thinking？需对比开启/关闭的文学质量
3. **章节间一致性**：多章节时，节序号需要跨章递增（第一章 第一~五节，第二章 第六~九节…）
4. **缓存**：同一 novel.md + 同一 prompt 的分节结果可缓存（MD5 hash）
5. **Prompt bundle 化**：提取为 `bundles/01_section_splitter/bundle.json` 以支持 A/B 测试
6. **对齐兼容性验证**：分节后跑 NovelAligner，确认节标题/过渡句不破坏对齐

## 十、验证产物清单

```
Golden/
├── TTS_SCENE_RULES_AB_REPORT.md                    # A/B/C 三轮对比报告
├── LoneTrail_Chapter1_Prompt_novel_baseline.md     # Pass 1 baseline 输出
├── LoneTrail_Chapter1_Prompt_novel_challenge_tts.md # Pass 1 v1.0 输出（有幻觉）
├── LoneTrail_Chapter1_Prompt_novel_challenge_v11.md # Pass 1 v1.1 输出（护栏版）
├── section_split_result_v2.json                     # Pass 2 切片版 JSON 输出
└── pass2_benchmark_v2.txt                           # Pass 2 benchmark

bundles/01_tts_scene_rules/
├── novelizer_system.md          # v1.1 prompt（含禁止性护栏，不再用于 Pass 1）
└── section_splitter_prompt.md   # Pass 2 system prompt

docs/design/
└── tts-section-splitter-integration.md  # 本文档
```
