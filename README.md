<img src="./ArkPlotWpf/assets/Donkey.png" alt="驴头" />
</div>


# ArkPlot


简单来说是一个~~这片大地~~"话剧剧本"生成器。


具体来说是一个基于 .NET 9 + Avalonia 框架，使用正则表达式的，明日方舟剧情文本生成器，可用来生成剧情的 Markdown/HTML 文件，并借助百炼 DeepSeek V4 将剧本转化为连贯小说。


## 设计初衷


Edge 浏览器"大声朗读"功能的 TTS 语音声音好听又富有情感。写 ArkPlot 就是想在打肉鸽的时候补剧情。目前用 ArkPlot 搭配大声朗读，我已经补了不少卡西米尔、莱塔尼亚、炎国、维多利亚……相关的剧情。


你可能会问，咋不去听萧然或者其他 up 主的？原因很简单，他们读的时候只有一个人，但是方舟剧情里常常有五六个 NPC 七嘴八舌的情况。


于是我找到了 [Kengxxiao 的《明日方舟》游戏数据](https://github.com/Kengxxiao/ArknightsGameData/tree/master)。量大管饱，按时 CI。但是方舟剧情文本的 txt 文件是一种使用方括号的"html"，直接扔给"大声朗读"无异于给自己上刑。于是这个简陋的 parser 就诞生了。


## 主要功能

- 支持中日英韩四服务器剧情文本生成
- 随游戏更新自动获取新剧情~~只要 Kengxxiao 不 Keng~~
- 处理游戏内原文本，可自定义 tag 及其处理用的正则表达式
- Avalonia 跨平台 UI（Windows / Linux / macOS）
- 🤖 **百炼 DeepSeek V4 小说化**：自动将剧本转化为连贯小说
<img src="https://github.com/user-attachments/assets/448ff8c3-c062-4b10-93c9-406cff735d6e" style="max-width: 100px;"/>

<img src="https://github.com/user-attachments/assets/cf29c882-a055-4431-9c2b-3b0049caefa7" style="max-width: 100px;"/>

---
---

## 输出效果

### 默认输出 HTML 效果

<img src="https://github.com/drunkenQCat/ArkPlotWpf/assets/39608175/000b04bf-8781-4c66-b472-872129d82657" alt="image" style="zoom: 50%;" />

### 小说形式 HTML 效果

<img src="https://github.com/user-attachments/assets/79529633-8f9b-48f2-bc3f-c9fe89ab0d91" style="zoom: 50%;" />


### Typora 中使用 Autumns 主题的效果

<img src="https://github.com/drunkenQCat/ArkPlotWpf/assets/39608175/67823cf5-5e11-4e0e-8dba-53035f881615" alt="image" style="zoom:50%;" />


## 使用说明

:warning: 本程序基于 .NET 9.0 构建，运行前请确保 [.NET 9.0 运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0) 已安装 :warning:

1. 程序所有内容来自 GitHub 仓库，使用时请务必全程`科学上网`
2. 选好活动名，点开始，一切便会好起来
3. 随游戏更新，《明日方舟》的 AVG 总是会添加新 tag。新 tag 在未收入 `tags.json` 时，相应语句不会处理，直接写入生成文件
4. 上述情况出现时，可以点击"编辑 Tags"对 tag、替换文字及正则表达式进行增删查改
5. `Assets/head.html` 用于调整输出 HTML 的样式，默认使用 [MarkdownPad2AutoCatalog](https://gitee.com/dr_cat/MarkdownPad2AutoCatalog)
6. `Assets/tail.html` 用于写 JS 脚本处理，默认功能是给所有角色名染色
7. 如果出了意外，欢迎 PR


## 下载

前往 [Releases](https://github.com/drunkenQCat/ArkPlotWpf/releases) 下载对应平台的版本：

| 平台 | 文件 |
|------|------|
| Windows x64 | `ArkPlot_x.x.x_win-x64.zip` |
| Linux x64 | `ArkPlot_x.x.x_linux-x64.tar.gz` |
| macOS x64 | `ArkPlot_x.x.x_osx-x64.tar.gz` |


## 开发说明

本项目使用 .NET 9.0，C# 13，低版本可能一些语法特性无法支持。

### 项目结构

| 项目 | 说明 |
|------|------|
| `ArkPlot.Core` | 核心逻辑：解析器、数据模型、Tag 处理 |
| `ArkPlot.Avalonia` | Avalonia 跨平台桌面 GUI（SukiUI 主题） |
| `ArkPlot.Novelizer` | 百炼 DeepSeek V4 小说化管线 |

### 构建

```bash
dotnet build ArkPlot.Avalonia/ArkPlot.Avalonia.csproj
```


## 🤖 AI 小说化（百炼 DeepSeek V4）

ArkPlot 内置了基于**阿里云百炼平台** DeepSeek V4 模型的剧情小说化功能。

### 如何使用

1. 前往 [百炼控制台](https://bailian.console.aliyun.com/) 获取 API Key
2. 设置环境变量 `DASHSCOPE_API_KEY` 为你的 API Key
3. 在 ArkPlot 界面勾选「使用DS生成小说」，选择模型（pro / flash）
4. 点击「开始」——生成 Markdown 后，会自动逐章调用 DeepSeek 将剧本转化为连贯小说

### 输出

输出目录中会额外生成：

```
无忧梦呓_novel_pro.md     # 小说 Markdown
无忧梦呓_novel_pro.html   # 小说 HTML（含样式/角色名染色）
```

### 模型选择

| 模型 | 特点 |
|------|------|
| `deepseek-v4-pro` | 高质量，文学性强，细节丰富 |
| `deepseek-v4-flash` | 快速，成本低，输出简练 |

### 注意事项

- 每章独立调用 API，多章并行处理（最多 3 章同时），不消耗本地 GPU
- 生成结果会缓存（基于源文件 MD5 hash），重复运行不重复调用 API
- ⚠️ 目前**仅支持百炼平台**的 DeepSeek。如需接入其他平台（SiliconFlow、DeepSeek 官方等），欢迎提交 PR

### 技术实现

相关代码位于 `ArkPlot.Novelizer/` 项目，采用标准 OpenAI 兼容接口，`BailianClient` 支持自动重试（429/5xx 退避）。日志回调解耦设计，CLI 和 Avalonia UI 共享同一管线。


## 项目用到的一些开源库

- 游戏数据：[Kengxxiao/ArknightsGameData](https://github.com/Kengxxiao/ArknightsGameData)
- UI 框架：[Avalonia](https://avaloniaui.net/) + [SukiUI](https://github.com/kikipoulet/SukiUI)
- MVVM 工具：[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- Markdown 转换：[Markdig](https://github.com/xoofx/markdig)
- C# JSON 库：[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
- ORM：[SqlSugar](https://github.com/DotNetNext/SqlSugar)
- Markdown 样式：[MarkdownPad2AutoCatalog](https://gitee.com/cayxc/MarkdownPad2AutoCatalog)
