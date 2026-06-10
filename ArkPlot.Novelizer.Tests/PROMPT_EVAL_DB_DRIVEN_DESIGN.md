# ArkPlot 提示词评测方案（数据库驱动版）

## 文档目的

这份文档用于纠正此前评测方案中的关键偏差，并重新定义 `Novelizer + Vision` 提示词优化的目标、边界与执行方式。

核心结论只有一句：

**最终要优化的不是一个静态 Markdown 文件，而是一条由数据库驱动、图片描述动态参与、最终产出小说的端到端工作流。**

---

## 一、先纠偏：此前思路哪里不对

此前的思路里，有一条隐含假设：

- 先从数据库或章节文本导出一份 `samples/*.md`
- 再把这份 md 当作固定输入去优化 `Novelizer prompt`

这个假设对于“局部分析”有用，但**不适合作为主评测框架**。原因如下。

### 1. Markdown 不是源数据，而是中间产物

在 ArkPlot 现有链路里，供 `Novelizer` 使用的 Markdown 不是手工写出来的，也不是数据库里天然存在的一份稳定文本。

它是由以下步骤动态生成的：

1. 从 DB 读取活动、章节、剧情条目、资源索引、图片描述缓存
2. 解析标签与剧情结构
3. 针对背景图/立绘图生成或读取 `PicDesc`
4. 把 `PicDesc`、对白、场景、资源共同拼装成最终导出的 Markdown
5. 再把这份 Markdown 喂给 `Novelizer`

也就是说：

- `Vision prompt` 会影响 `PicDesc`
- `PicDesc` 会影响导出的 Markdown
- Markdown 会影响 `Novelizer` 的输入
- `Novelizer prompt` 才会进一步影响最终小说

所以如果把 `Markdown` 冻结掉，很多真正重要的 prompt 变化都会在评测中失真。

### 2. Vision prompt 不是独立模块，而是会改写 Novelizer 的输入

对于这个项目来说，`Vision` 不是一个单独的“附属功能”，它会直接参与小说输入的构造。

因此：

- 优化 `Vision system prompt`
- 优化 `Vision user prompt`
- 优化 `Novelizer system prompt`

这三者并不是彼此独立的，它们共同构成一个端到端 bundle。

### 3. 静态样本只能用于复盘，不能作为主评测输入

静态 `samples/*.md` 仍然有价值，但它的定位应该是：

- 调试快照
- 人工阅读材料
- 回归比对材料
- 问题复现样本

而不是：

- 主评测输入源

主评测输入源必须是：

- `DB + 当前 prompt bundle + 当前导出链路`

---

## 二、真正要优化的对象

正确的评测对象不是单个 prompt 文件，而是一个端到端配置单元。

建议称为：`Prompt Bundle`

一个 bundle 至少包含以下内容：

- `novelizer_system_prompt`
- `vision_system_prompt`
- `vision_user_background_prompt`
- `vision_user_portrait_prompt`

必要时还可包含：

- `vision_model`
- `novelizer_model`
- `picdesc_refresh_policy`
- `md_export_options`

因此，每一轮的比较不应是：

- prompt A vs prompt B

而应是：

- bundle A vs bundle B

---

## 三、正确的目标工作流

目标工作流应该定义为：

```text
数据库
-> 读取活动/章节/剧情条目/资源索引/图片描述缓存
-> 使用当前 Vision prompt 生成或刷新图片描述
-> 将剧情文本 + 图片描述 + 资源信息导出为 Markdown
-> 将导出的 Markdown 喂给 Novelizer
-> 生成小说
-> 记录中间产物与最终结果
```

这是唯一能真实反映“提示词是否提升成书质量”的路径。

---

## 四、数据库在这个流程中的角色

数据库不是“一个方便拿样本的地方”，而是整个评测系统的真实源头。

数据库里承载了以下信息：

- 活动与章节索引
- 原始/解析后的剧情条目
- 背景图与立绘相关资源
- 角色立绘关联
- 历史 `PicDesc` 缓存

如果离开数据库，以下需求都会变得不成立：

1. 动态刷新图片描述
2. 对同一章节反复导出不同版本的 md
3. 对不同 prompt bundle 做真实 A/B 对比
4. 分析缓存是否污染了结果
5. 复现实战中的“图片描述影响小说输入”的现象

因此，后续评测方案必须默认：

- **数据库是主输入源**

而不是：

- 静态 md 是主输入源

---

## 五、图片描述为什么必须动态参与

你最终关心的是“生成一本质量更高的小说”，而不是“某一层的文本单独看更好”。

在这个项目里，图片描述的作用不是旁路信息，而是会直接进入 Markdown。

因此同一个章节，在不同 Vision prompt 下，理论上会生成不同的中间 Markdown：

- 不同的背景描述
- 不同的立绘描述
- 不同的描述颗粒度
- 不同的细节偏向

这些差异会进一步影响：

- 场景感
- 角色外貌嵌入
- 文本节奏
- 信息密度
- 模板化程度

所以对于 prompt 评测来说，必须支持：

- 按当前 bundle 重新生成图片描述
- 再据此重新导出 Markdown

否则就不是真正的端到端比较。

---

## 六、静态样本在新框架里的正确定位

静态样本并不是要废弃，而是要降级为“观察材料”。

它适合用于：

### 1. 快照存档

保存某一轮 bundle 生成出来的中间 Markdown，方便人工阅读与回溯。

### 2. 回归比对

对比：

- bundle A 生成的 md
- bundle B 生成的 md

看图片描述的差异是否真的进入了导出结果。

### 3. 定向调试

例如：

- 某张背景图描述不稳定
- 某个角色立绘描述过度模板化
- 某个章节导出的 Markdown 结构异常

这时静态样本非常有帮助。

但静态样本**不能作为主评测入口**。

---

## 七、建议的评测模式

新的方案建议分成两层：

## 模式 A：数据库驱动的真实主流程评测

这是主评测模式，也是最终用于“打擂台”的模式。

流程：

1. 选择 bundle
2. 选择活动/章节
3. 从数据库读取剧情与资源
4. 根据 bundle 刷新或复用 `PicDesc`
5. 重新导出 Markdown
6. 用 bundle 中的 `Novelizer prompt` 生成小说
7. 保存：
   - 中间 Markdown
   - 最终小说
   - 本轮使用的 `PicDesc` 快照
   - 运行元数据
8. 打分

### 适用场景

- 真正比较 bundle 优劣
- 自动循环评测
- 最终选 champion

## 模式 B：冻结中间层的局部分析评测

这是辅助模式，不是主模式。

它适合做：

- 固定 md，只比较 `Novelizer prompt`
- 固定图片，只比较 `Vision prompt`
- 固定 `PicDesc`，分析 `Novelizer` 的纯文本能力

### 适用场景

- 定位问题来源
- 控制变量分析
- 失败归因

结论：

- **模式 A 是主流程**
- **模式 B 是调试工具**

---

## 八、评测 runner 应该长什么样

后续不应该把“样本导出”和“小说生成”拆成互不相干的几个小脚本，而应有一个统一的 DB-driven runner。

建议接口形态如下：

```text
RunBundleAsync(
  bundleId,
  actId,
  chapterCode,
  refreshPicDesc,
  outputRunDir)
```

### 最低要求

runner 至少需要支持以下能力：

1. 读取 bundle 配置
2. 读取数据库中的活动与章节
3. 驱动真实导出流程
4. 在当前 bundle 下生成或刷新图片描述
5. 导出 Markdown
6. 调用 `Novelizer` 生成小说
7. 将本轮所有产物落盘

### 推荐落盘内容

每一轮建议至少输出：

- `input.md`：本轮导出的 Markdown
- `novel.md`：本轮生成的小说
- `picdesc.json`：本轮涉及的图片描述
- `bundle.json`：本轮使用的 bundle 快照
- `run.json`：本轮元数据与打分结果

---

## 九、缓存策略必须纳入设计

这是后续最容易出问题的部分之一。

如果 `PicDescription` 已经缓存了旧 prompt 下的描述，而新 prompt 运行时直接复用缓存，那么：

- 你以为自己在评新的 Vision prompt
- 实际上用的还是旧描述

这样评测结果就会失真。

因此 runner 需要明确支持两种模式：

### 1. `reuse_picdesc_cache`

含义：

- 优先复用数据库里已有的图片描述

适用：

- 日常运行
- 快速查看
- 非严格评测

### 2. `refresh_picdesc_for_bundle`

含义：

- 根据当前 bundle 重新生成本轮涉及图片的描述

适用：

- 正式 prompt 评测
- A/B 对比
- champion challenge

对于正式评测，建议默认使用：

- `refresh_picdesc_for_bundle`

---

## 十、为什么 Headless 仍然是对的

虽然主流程必须是数据库驱动，但这并不否定 `Avalonia.Headless`。

恰恰相反，`Headless` 的价值在于：

- 可以直接复用 Avalonia 现有的真实工作流
- 避免我再人为拼一条“近似流程”
- 让数据库、导出、设置、ViewModel 编排都尽可能贴近生产路径

正确理解应当是：

- **数据库是数据源**
- **Headless 是编排宿主**

二者并不冲突。

所以更合理的实施方式是：

1. 使用真实 DB 作为输入源
2. 使用 `Avalonia.Headless` 复用现有工作流
3. 在 headless 模式下去掉最后的人机交互弹窗
4. 将输出结果作为评测产物落盘

---

## 十一、后续文档和代码应该怎样调整

此前的文档与框架中，最需要修正的是“静态 md 是主输入”的暗示。

后续建议：

### 文档层面

需要明确写清：

- `samples/*.md` 是快照，不是主输入
- 主输入来自数据库
- 图片描述必须动态参与 md 导出
- 正式评测必须支持 `refresh_picdesc`

### 代码层面

建议分成三块：

#### 1. DB-driven eval runner

负责：

- 读取数据库
- 运行导出
- 运行 novelizer
- 保存产物

#### 2. Snapshot exporter

负责：

- 把某一轮的中间 md 导出成快照

#### 3. Bundle manager

负责：

- 管理 prompt bundle
- 管理版本
- 管理父子关系

---

## 十二、推荐的分阶段落地顺序

### 第一阶段：先把文档和目标定正

不再把“静态 md 样本评测”当主线，而是明确：

- 评测目标是 DB-driven end-to-end

### 第二阶段：做一个最小可跑的 DB-driven runner

只要求它能：

- 指定一个 bundle
- 指定一个活动第一章
- 从 DB 出发
- 刷新图片描述
- 导出 md
- 生成小说
- 保存结果

### 第三阶段：再补自动评分和循环对战

等 runner 真能稳定跑起来，再补：

- facts 校验
- 排行榜
- champion challenge
- 自动循环评测

### 第四阶段：静态快照变成辅助材料

保留 `samples/*.md`，但只作为：

- 调试材料
- 回归材料
- 人工阅读材料

---

## 十三、最终结论

这套评测框架的正确中心，不是某个 prompt，也不是某一份 md，而是：

**数据库驱动下，由 Vision prompt 动态生成图片描述、再参与 Markdown 导出、最终喂给 Novelizer 形成小说的整条工作流。**

因此，后续所有设计都应该围绕以下原则展开：

1. 数据库是主输入源
2. Markdown 是中间产物，不是主输入
3. Vision prompt 会改变 Markdown，因此必须纳入主评测
4. Novelizer prompt 只是端到端 bundle 的一部分
5. 静态样本只能作为快照与辅助分析材料
6. 正式打擂台必须走数据库驱动的真实工作流

---

## 十四、给后续 Agent 的一句话任务定义

如果后续要把这份文档交给 agent，最准确的一句话应该是：

**请不要再把 `samples/*.md` 当成主输入，而是实现一个数据库驱动的端到端评测 runner，使 Vision prompt 能动态影响图片描述与 Markdown 导出，再与 Novelizer prompt 一起决定最终小说质量。**
