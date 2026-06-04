<img src="./ArkPlotWpf/assets/Donkey.png" alt="驴头" />
</div>


# ArkPlot


简单来说是一个~~这片大地~~"话剧剧本"生成器。


具体来说是一个基于 .NET 9 + Avalonia 框架，使用正则表达式的，明日方舟剧情文本生成器，可用来生成剧情的 Markdown/HTML 文件，并借助大语言模型将剧本转化为连贯小说，还能调用视觉模型为立绘和场景图生成文字描述。


## 设计初衷


Edge 浏览器"大声朗读"功能的 TTS 语音声音好听又富有情感。写 ArkPlot 就是想在打肉鸽的时候补剧情。目前用 ArkPlot 搭配大声朗读，我已经补了不少卡西米尔、莱塔尼亚、炎国、维多利亚……相关的剧情。


你可能会问，咋不去听萧然或者其他 up 主的？原因很简单，他们读的时候只有一个人，但是方舟剧情里常常有五六个 NPC 七嘴八舌的情况。


于是我找到了 [Kengxxiao 的《明日方舟》游戏数据](https://github.com/Kengxxiao/ArknightsGameData/tree/master)。量大管饱，按时 CI。但是方舟剧情文本的 txt 文件是一种使用方括号的"html"，直接扔给"大声朗读"无异于给自己上刑。于是这个简陋的 parser 就诞生了。


## 主要功能

- 支持中日英韩四服务器剧情文本生成
- 随游戏更新自动获取新剧情~~只要 Kengxxiao 不 Keng~~
- 处理游戏内原文本，可自定义 tag 及其处理用的正则表达式
- Avalonia 跨平台 UI（Windows / Linux / macOS）
- 🤖 **AI 小说化**：支持 DeepSeek、百炼及自定义 OpenAI 兼容接口，自动将剧本转化为连贯小说
- 🖼️ **图片描述**：调用视觉模型为立绘和场景图生成文字描述，融入小说文本
- 📚 **epub 导出**：小说化文本可自动生成 epub 电子书（需安装 [Pandoc](https://pandoc.org/)）
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
| `ArkPlot.Core` | 核心逻辑：解析器、数据模型、Tag 处理、图片描述管线 |
| `ArkPlot.Avalonia` | Avalonia 跨平台桌面 GUI（SukiUI 主题） |
| `ArkPlot.Novelizer` | LLM 小说化管线，支持 DeepSeek/百炼/自定义 OpenAI 兼容接口 |
| `ArkPlot.Vision` | 视觉模型调用模块，支持百炼和 Ollama |

### 构建

```bash
dotnet build ArkPlot.Avalonia/ArkPlot.Avalonia.csproj
```


## 🤖 AI 小说化

ArkPlot 内置了基于大语言模型的剧情小说化功能，将方舟的 AVG 剧本转化为连贯的小说文本。

### 支持的平台

| 平台 | API 地址 | 环境变量 | 说明 |
|------|----------|----------|------|
| DeepSeek 官方 | `api.deepseek.com` | `DEEPSEEK_API_KEY` | deepseek-v4-pro / flash |
| 阿里云百炼 | `dashscope.aliyuncs.com` | `DASHSCOPE_API_KEY` | DeepSeek、GLM、MiniMax、Kimi 等多种模型 |
| 自定义 Provider | 用户指定 | 设置页配置 | 任何 OpenAI 兼容接口（如 OpenRouter、Groq 等） |

### 模型选择

| 模型 | 特点 |
|------|------|
| `deepseek-v4-pro` | 高质量，文学性强，细节丰富 |
| `deepseek-v4-flash` | 快速，成本低，日常够用 |
| `glm-5` | 智谱 GLM，百炼平台可用 |
| `MiniMax-M2.5` | MiniMax，百炼平台可用 |
| `kimi-k2.5` | 月之暗面 Kimi，百炼平台可用 |
| 自定义模型 | 取决于你配置的 Provider |

### 如何使用

1. 前往 [DeepSeek 开放平台](https://platform.deepseek.com/) 或 [百炼控制台](https://bailian.console.aliyun.com/) 获取 API Key
2. 设置对应的环境变量（`DEEPSEEK_API_KEY` 或 `DASHSCOPE_API_KEY`），或在**设置页 → 小说化设置**中直接填写
3. 在主界面勾选「启用小说化生成」
4. 点击「开始」——生成 Markdown 后，会自动逐章调用 LLM 将剧本转化为连贯小说

### 输出

输出目录中会额外生成：

```
无忧梦呓_novel_deepseek-v4-pro.md     # 小说 Markdown
无忧梦呓_novel_deepseek-v4-pro.html   # 小说 HTML（含样式/角色名染色）
无忧梦呓_novel_deepseek-v4-pro.epub   # epub 电子书（需安装 Pandoc）
```

文件名后缀会随实际使用的模型名变化。

### 自定义 Provider

如果需要使用其他 OpenAI 兼容接口（如 OpenRouter、Groq、Together AI 等），可以在**设置页 → 小说化设置 → 自定义平台管理**中添加：

- **平台名称**：随意取名，如 `OpenRouter`
- **API Base URL**：如 `https://openrouter.ai/api/v1`
- **API Key**：对应平台的密钥
- **模型列表**：逗号分隔，如 `gpt-4o,claude-3.5-sonnet`

添加后在平台选择下拉框中即可看到新平台。

### 注意事项

- 每章独立调用 API，多章并行处理（最多 3 章同时），不消耗本地 GPU
- 生成结果会缓存（基于源文件 MD5 hash），重复运行不重复调用 API
- 如需 epub 输出，请先安装 [Pandoc](https://pandoc.org/installing.html) 并确保 `pandoc` 命令可用


## 🖼️ 图片描述

ArkPlot 可以调用视觉模型为游戏中的立绘和场景图生成文字描述，并将描述融入小说文本中，让 AI 在小说化时能参考角色的外观和场景的氛围。

### 支持的 Provider

| Provider | 说明 |
|----------|------|
| 百炼 | 云端调用，支持 qwen-vl 系列模型 |
| Ollama | 本地运行，无需 API Key，需先安装 Ollama 并拉取视觉模型 |
| 自定义 Provider | 任何 OpenAI 兼容的视觉接口 |

### 如何使用

1. 在**设置页 → 图片描述设置**中选择 Provider 和模型
2. 百炼平台：选择模型（如 `qwen-vl-max`），API Key 从环境变量或自定义 Provider 获取
3. Ollama：确保 Ollama 服务运行中，选择模型（如 `qwen3-vl:8b`）
4. 在主界面勾选「启用图片描述」
5. 点击「开始」——图片描述会在小说化之前自动执行，结果缓存到数据库

### 系统提示词

图片描述使用专门的系统提示词，可在设置页编辑。默认提示词要求模型：
- 禁止元评论（"这是一幅画"之类的分析性开场白）
- 禁止风格分析（"动漫风格"、"游戏美术"等）
- 使用叙事语言描述环境氛围、关键物体、人物姿态
- 控制在 200 字以内

### 注意事项

- 图片描述结果会缓存到数据库，同一张图片不会重复调用 API
- 已知干扰图（黑色背景 `bg_black.png`、空白角色 `char_empty.png`）会被自动跳过
- 首次使用时图片描述耗时较长（取决于模型和网络），后续重复运行会很快


## 项目用到的一些开源库

- 游戏数据：[Kengxxiao/ArknightsGameData](https://github.com/Kengxxiao/ArknightsGameData)
- UI 框架：[Avalonia](https://avaloniaui.net/) + [SukiUI](https://github.com/kikipoulet/SukiUI)
- MVVM 工具：[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- Markdown 转换：[Markdig](https://github.com/xoofx/markdig)
- C# JSON 库：[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
- ORM：[SqlSugar](https://github.com/DotNetNext/SqlSugar)
- Markdown 样式：[MarkdownPad2AutoCatalog](https://gitee.com/cayxc/MarkdownPad2AutoCatalog)
