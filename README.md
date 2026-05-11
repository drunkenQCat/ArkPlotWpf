<div align="center">
<img src="./assets/Donkey.png" alt="驴头" />
</div>


# ArkPlotWpf


简单来说是一个~~这片大地~~“话剧剧本”生成器。


具体来说是一个使用.NET WPF框架，基于正则表达式的，明日方舟剧情文本生成器，可用来生成剧情的markdown/html文件。


## 设计初衷


edge浏览器“大声朗读”功能的TTS语音声音好听又富有情感。写ArkPlot就是想在打肉鸽的时候补剧情。目前用ArkPlot搭配大声朗读，我已经补了不少卡西米尔、莱塔尼亚、炎国、维多利亚……相关的剧情。


你可能会问，咋不去听萧然或者其他up主的？原因很简单，他们读的时候只有一个人，但是方舟剧情里常常有五六个NPC七嘴八舌的情况。


于是我找到了[Kengxxiao的《明日方舟》游戏数据 ](https://github.com/Kengxxiao/ArknightsGameData/tree/master)。量大管饱，按时CI。但是方舟剧情文本的txt文件是一种使用方括号的“html”，直接扔给“大声朗读”无异于给自己上刑。于是这个简陋的“parser”就诞生了。


## 主要功能




* 支持中日英韩四服务器剧情文本生成
* 随游戏更新自动获取新剧情~~只要Kengxxiao不Keng~~
* ~~粗暴~~处理游戏内原文本，可自定义tag及其处理用的正则表达式
* ~~简陋~~精美UI




<div align="center">
<img src="https://github.com/drunkenQCat/ArkPlotWpf/assets/39608175/347f18d9-9139-4239-bf84-11802aa2ccf5" alt="image" style="max-width: 100px;" />
</br>
<img src="https://github.com/drunkenQCat/ArkPlotWpf/assets/39608175/836af671-1cda-42e4-8c2c-6e7c0274d5c5" alt="image" style="max-width: 100px;" />
</div>

---
---
## 输出效果

### 默认输出html效果

<img src="https://github.com/drunkenQCat/ArkPlotWpf/assets/39608175/000b04bf-8781-4c66-b472-872129d82657" alt="image" style="zoom: 50%;" />



### Typora 中使用 Autumns 主题的效果

<img src="https://github.com/drunkenQCat/ArkPlotWpf/assets/39608175/67823cf5-5e11-4e0e-8dba-53035f881615" alt="image" style="zoom:50%;" />




## 使用说明



:warning:本程序基于 .Net 7.0 构建，运行前请保证[.Net 7.0 运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/7.0)已安装:warning:
1. 因为程序所有内容都来自GitHub仓库，所以在使用时请务必全程`科学上网`
2. 如果不出意外，选好活动名，点开始，一切便会好起来
3. 随游戏更新，《明日方舟》的AVG总是会添加新tag~~或者错别字~~。新tag在没有收入tags.json中的时候，相应的语句不会处理，直接写入生成文件，例如:假如明日方舟新出了一个立绘变形方式，取名characteraction，tags.json中没有收录这个tag，那么```[characteraction(name="middle",type="move",ypos=-50,fadetime=0.51)]```会直接写到生成的文件里，而不是写入```人物动作：移动```这样的中文缩写，~~耳朵要偶尔受刑~~
4. 如果上述情况出现，可以尝试点击软件中的“编辑Tags”对tag、替换文字及相应的正则表达式进行增删查改
5. assets/head.html 是用来调整输出html的样式的。默认使用[MarkdownPad2AutoCatalog](https://gitee.com/dr_cat/MarkdownPad2AutoCatalog)
6. assets/tail.html 是用来写一些用来处理的js脚本的。默认功能是给所有角色名染色
7. 如果出了意外，欢迎PR。~~俺随缘更新~~




以上。




## 开发说明

本项目使用net9.0，C#13，低版本可能一些语法特性无法支持。

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
无忧梦呓_novel_pro.md    # 小说 Markdown
无忧梦呓_novel_pro.html  # 小说 HTML（含样式/角色名染色）
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




* 游戏数据：[Kengxxiao/ArknightsGameData: 《明日方舟》游戏数据 (github.com)](https://github.com/Kengxxiao/ArknightsGameData/tree/master)




* WPF 控件库：[HandyControl](https://github.com/HandyOrg/HandyControl) 




* C# JSON库：[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) 




* Markdown样式库：[MarkdownPad2AutoCatalog](https://gitee.com/cayxc/MarkdownPad2AutoCatalog)
