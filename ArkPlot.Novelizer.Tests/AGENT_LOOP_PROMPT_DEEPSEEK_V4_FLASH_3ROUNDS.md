# Agent 循环优化提示词（`deepseek-v4-flash` / 三轮版）

这份文件用于直接交给 agent，要求它基于 ArkPlot 当前真实链路，自主完成 3 轮提示词优化与验证。

适用目标：

- 小说化模型固定为 `deepseek-v4-flash`
- 优先利用其速度快、成本低的特点做小步快跑
- 评测必须走数据库驱动的真实链路
- 只允许做可归因的小改动

可直接将下面整段提示词原样发给 agent：

---

```text
你现在是 ArkPlot 的提示词优化代理。

你的任务不是泛泛讨论方案，而是在现有代码和现有 bootstrap 基础上，使用真实数据库驱动流程，自主完成 3 轮 prompt 优化循环，并在每一轮结束后用实际产物验证结果。

## 一、固定前提

1. 唯一标准流程是 `ArkPlot.Avalonia` 的：
   `LoadMd -> ExportDocuments -> RunNovelizerIfEnabled`

2. 主输入源是数据库，不是静态 `samples/*.md`

3. 你必须优先遵守并阅读以下文件：
   - `ArkPlot.Novelizer.Tests/PROMPT_EVAL_DB_DRIVEN_DESIGN.md`
   - `ArkPlot.Avalonia/Services/PromptEvalBootstrapper.cs`
   - `ArkPlot.Avalonia.Tests/PromptEvalBootstrapIntegrationTests.cs`
   - `ArkPlot.Novelizer/NovelizerPipeline.cs`
   - `ArkPlot.Vision/BailianVisionClient.cs`
   - `ArkPlot.Novelizer.Tests/bootstrap/current_prompts/bootstrap_summary.json`

4. 本次小说化模型固定为：
   - `deepseek-v4-flash`

5. 你必须把“`deepseek-v4-flash` 速度快、便宜，但输出方差可能更大”视为本轮优化的现实约束。
   因此你要特别注意：
   - 信息覆盖率优先于文笔华丽
   - 稳定性优先于偶发神作
   - 每轮只改一个主要变量

## 二、你的优化目标

你要优化的是 prompt bundle，而不是单个 prompt。

本轮允许关注的 prompt 范围：

- `NovelizerPipeline.DefaultSystemPrompt`
- `BailianVisionClient` 使用的 system prompt
- `BailianVisionClient` 使用的 user prompt

但每一轮只能选择一个主要方向：

- 要么主要改 `Novelizer`
- 要么主要改 `Vision`

不要在同一轮同时大改两边。

## 三、评价优先级

你的评价优先级必须固定为：

1. 信息覆盖率
2. 角色/事件/场景对应正确率
3. 场景与立绘融入自然度
4. 输出稳定性
5. 文学性

任何“更像小说但漏信息”的结果，都必须判为退步。

## 四、现有基线

当前基线产物已经存在于：

- `ArkPlot.Novelizer.Tests/bootstrap/current_prompts/`

其中包含以下活动第一章的真实链路输出：

- `长夜临光`
- `孤星`
- `巴别塔`
- `辞岁行`

每一轮开始前，你都必须至少阅读：

- 一个活动的导出 `md`
- 同活动的 `picdesc.snapshot.json`
- 同活动的最终 `*_novel_*.md`

优先建议从以下两个活动里选一个作为主精读样本：

- `孤星`
- `辞岁行`

同时用另外 1 到 2 个活动做交叉验证，防止只对单一样本有效。

## 五、缓存策略

你必须严格遵守以下缓存规则：

- 如果本轮只改 `Novelizer` prompt，可以复用历史 `PicDesc`
- 如果本轮改了 `Vision` prompt，必须刷新本轮涉及图片的 `PicDesc`，并重新导出 Markdown，再运行 Novelizer

每轮输出里必须明确写出：

- `是否复用 PicDesc`
- `为什么可以复用 / 为什么必须刷新`

## 六、三轮循环规则

你只执行 3 轮，不要无限循环。

### Round 1

目标：
- 先读当前基线产物
- 找到一个最具体、最可验证、最值得优先修复的问题
- 只做一次最小修改

建议优先方向：
- 如果当前最终小说存在明显信息丢失，优先改 `Novelizer` prompt
- 如果当前导出 md 已经信息充分，但图片描述明显空泛或过度散文化，优先改 `Vision` prompt

### Round 2

目标：
- 基于 Round 1 的结果继续推进
- 如果 Round 1 成功，则继续沿同一路径微调一个更细的点
- 如果 Round 1 失败，则回退并换一个单点问题

### Round 3

目标：
- 做最后一轮最小优化或确认收敛
- 如果已经得到明显更优版本，则用这一轮做稳健性复核
- 如果仍无明确提升，则输出“不建议晋升 champion”的结论

## 七、每轮严格步骤

每一轮必须按以下顺序执行：

### 第 1 步：读证据

至少阅读并总结：

- 导出的 `md`
- `picdesc.snapshot.json`
- 最终 `novel md`

你必须先回答：

- 当前最具体的问题是什么
- 这个问题更像是 `Novelizer`、`Vision` 还是导出链路的问题

### 第 2 步：提出单一假设

每轮只允许提出一个主要假设。

例如：

- `deepseek-v4-flash` 在当前 Novelizer prompt 下更容易为了流畅性牺牲覆盖率，因此需要更强的覆盖约束
- Vision 描述太像散文，进入 Markdown 后反而削弱了 Novelizer 的结构化吸收
- 当前 prompt 对场景/立绘的嵌入约束不够清晰，导致最终正文吸收不稳定

### 第 3 步：做最小改动

只改最小必要内容。

优先改 prompt。

只有在以下情况下才允许顺手改代码：

- 为了让真实链路更可验证
- 为了让缓存行为与本轮假设一致
- 为了让运行结果更容易落盘与比较

不允许为了省事重写整条流程。

### 第 4 步：运行真实验证

你必须走真实链路验证，而不是仅凭阅读判断。

验证后至少要对比：

- 修改前后的导出 md 是否变化
- 修改前后的 PicDesc 是否变化
- 修改前后的 novel md 是否变化

如果只改 `Novelizer`，你应该重点确认：

- md 基本不变
- novel 输出的覆盖率/融入自然度是否改善

如果改 `Vision`，你应该重点确认：

- PicDesc 变化
- md 随之变化
- novel 也随之变化

### 第 5 步：判定成败

判定标准固定如下：

- 先看是否减少信息丢失
- 再看角色/场景/事件对应是否更稳
- 再看场景与立绘融入是否更自然
- 最后才看文笔

如果没有明确提升，不得宣布成功。

### 第 6 步：写轮次复盘

每轮结束都必须写出：

- 这一轮改了什么
- 为什么改
- 证据是什么
- 成功还是失败
- 下一轮准备怎么做

## 八、`deepseek-v4-flash` 专项约束

因为小说化模型固定为 `deepseek-v4-flash`，你必须额外遵守：

1. 不要把 prompt 写得过长、过散、过多层嵌套
2. 不要在一轮中叠加太多规则
3. 不要堆大量“禁止事项”黑名单
4. 优先使用少量、清晰、强约束的规则
5. 明确告诉模型“不得为了流畅而删减信息”
6. 明确告诉模型“场景与立绘信息必须进入正文，而不是被忽略”
7. 如果发现它容易摘要化，优先强化覆盖与保留信息单元的约束

换句话说：

- 对 `deepseek-v4-flash`，你应倾向于“短、硬、清楚”的 Novelizer prompt 微调
- 而不是“长、全、面面俱到”的复杂改写

## 九、停止条件

你必须在 Round 3 结束后停止。

不要进入第 4 轮。

最终必须输出一个总总结，只能有以下三种结论之一：

1. `Round N` 产生了可晋升的新 champion
2. 三轮内没有得到稳定更优结果，不建议晋升
3. 问题已超出 prompt 工程范围，需要转向导出/缓存/编排层

## 十、每轮输出格式

你每一轮都必须严格使用这个格式：

## Round N
- 主样本：
- 交叉验证样本：
- 当前最具体问题：
- 问题归因：
- 本轮唯一假设：
- 改动位置：
- 是否改 Vision：
- 是否改 Novelizer：
- 是否复用 PicDesc：
- 验证方式：
- 结果：
- 是否优于上一版：
- 下一轮计划：

## Final Summary
- 最优轮次：
- 是否产生新 champion：
- 最值得保留的改动：
- 最大副作用：
- 是否建议继续做第 2 轮三轮循环：

## 十一、立即开始执行

现在就开始，不要先空谈方案。

你第一步要做的是：

1. 阅读 `bootstrap_summary.json`
2. 选 `孤星` 或 `辞岁行` 作为主样本
3. 对照该活动的 `md / picdesc / novel md`
4. 找出当前最具体的一个问题
5. 开始 Round 1

除非遇到阻断错误，否则不要把控制权交还给用户，直到完成 3 轮。
```

---

## 使用建议

如果你希望 agent 更保守一点，可以在发给它时额外补一句：

```text
优先从 Novelizer prompt 下手；只有当证据明确显示问题来自 PicDesc 质量时，才切到 Vision。
```

如果你希望 agent 更激进一点，可以额外补一句：

```text
如果 Round 1 已经明确发现 `deepseek-v4-flash` 的主要问题是覆盖率不足，请优先把 Round 2 和 Round 3 都用于收紧 Novelizer prompt，而不是切换方向。
```
