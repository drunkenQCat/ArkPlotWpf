using ArkPlot.Core.Model;

namespace ArkPlot.Novelizer;

/// <summary>
/// 小说化管线：读 Markdown → 拆章 → 逐章调 LLM → 合并写小说文件
/// </summary>
public class NovelizerPipeline
{
    private readonly BailianClient _client;
    private readonly Action<string>? _onLog;
    private readonly string _systemPrompt;

    private const string DefaultSystemPrompt = """
## 明日方舟剧情小说化转换协议

### 一、 叙事视角与文体调性

* **视角规范**：严格采用**第三人称有限视角**。叙述焦点应锚定于当前场境的核心角色，通过其感官、逻辑与职责边界推进叙事。
* **文体特征**：承袭《孤星》、《巴别塔》、《乌萨斯的孩子们》等剧作的冷峻、克制与思辨感。剔除网络文学的夸张修辞、影视解说的全知旁白，以及短视频式的断句节奏。
* **组织结构**：采用传统严肃文学的长段落结构。一个合格的叙事单元须将**环境异动、角色动作、观察结果、心理推演**有机融合，严禁无故出现连续的单句成段或刻意制造悬念的破折号断句。
* **句首变化**：连续三句话不得以相同方式开头。禁止"她……""他……""角色名……"的反复循环。用环境、动作、声音、物件来引领句子，而非总是以角色名打头。

### 二、 角色标签的铁律

**输入文本中每段对话前的粗体角色名是该角色的唯一标识。你必须在叙事中使用这些原始名称来指代对应角色，严禁自行替换、合并或混淆角色标签。不同名称的角色是不同的人。**

### 三、 视听语言的叙事转化

* **场景（背景图）**：严禁概括性描述。必须将场景拆解为具象的叙事客体。
* **角色外貌（立绘描述）**：输入文本中的外貌描述来自游戏立绘，其中包含大量模板化表达（如"沉静如深潭"、"仿佛正递出一枚无法言说的契约"）。**你必须用自己的语言重新描写这些外貌特征，不得照抄原文中的任何句子。** 保留关键视觉信息（发色、装备、伤疤等），但用全新的、场景特定的语言重新表达。
  1. **删除所有元描述**：任何"站在空无一物的虚空中"、"站在空茫的背景里"、"望向画面外某处"等描述游戏展示方式的文字必须完全删除。
  2. **碎片化嵌入**：将外貌特征拆解，分散嵌入角色的动作和交互中。
* **声音（音乐/音效）**：严禁出现任何提及音乐或音效的字眼。BGM变化转化为叙事张力，音效转化为物理事件。

### 四、 对话的质感与角色声音

**这是本协议最重要的部分。** 对话不能只是原文的简单搬运。你必须为每句对话编织入"对话纹理"：

* **动作包裹**：每段对话前后必须嵌入角色的具体物理动作、微表情或与环境的小交互。动作必须与对话内容在情绪上呼应。
* **语气分层**：不同角色的说话方式必须有可辨识的差异——
* **潜台词**：在关键对话中添加角色的内心活动，让读者感受到"这句话背后还有什么没说出来的"。
* **对话节奏**：紧张场景用短句交锋，日常场景允许闲聊式长段落。

### 五、 去模板化规则

* 严禁滥用"顿了顿"、"轻轻叹气"、"抬起头看向对方"、"嘴角微微上扬"、"深吸一口气"、"目光沉静如深潭"等通用机械动作。
* 每一次交互必须带有明确的战术意图或心理动机。

### 六、 严重警告事项
* 不要忽略">"开头的文字，那些是需要保留的原文。
* 带有html标签的文本不参与信息筛选，只用于补充信息。
* 输出必须覆盖所有信息单元，不得压缩为摘要。
* 允许改写表达，但禁止信息缺失。
* 若信息过多，应扩展文本长度，而不是删减内容。


---

**【即刻执行】** 请提供你需要改写的《明日方舟》AVG剧情脚本。我将直接输出符合上述标准的小说正文，不包含任何前言、后记或解释性说明。
""";

    /// <param name="onLog">可选日志回调，同时写入 Console 和此回调（用于 Avalonia UI 同步）</param>
    /// <param name="systemPrompt">可选的自定义系统提示词，未提供时使用默认值</param>
    public NovelizerPipeline(
        BailianClient client,
        ApiConfig config,
        Action<string>? onLog = null,
        string? systemPrompt = null
    )
    {
        _client = client;
        _onLog = onLog;
        _systemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? DefaultSystemPrompt
            : systemPrompt;
    }

    private void Log(string msg)
    {
        Console.WriteLine(msg);
        _onLog?.Invoke(msg);
    }

    private void LogError(string msg)
    {
        Console.Error.WriteLine(msg);
        _onLog?.Invoke(msg);
    }

    /// <summary>
    /// 处理一个 .md 文件：
    /// 1. 读取 + 预处理（去 HTML）
    /// 2. 按 ## 标题拆成独立章节
    /// 3. 每一章单独调 LLM
    /// 4. 所有章节合并成一个小说文件
    /// </summary>
    public async Task<string> ProcessMdFileAsync(string mdPath, string model, string outputDir)
    {
        Log($"[DIAG] ProcessMdFileAsync 开始。file={Path.GetFileName(mdPath)}, model={model}");

        Log($"[DIAG] 读取文件...");
        var mdContent = File.ReadAllText(mdPath);
        Log($"[DIAG] 文件读取完成，{mdContent.Length} 字符");

        Log($"[DIAG] 预处理（去 HTML）...");
        var processed = MarkdownBuilder.PreprocessMdContent(mdContent);
        Log($"[DIAG] 预处理完成，{processed.Length} 字符（原始 {mdContent.Length}）");

        // 拆章
        Log($"[DIAG] 按 ## 标题拆章...");
        var chapters = ChapterSplitter.SplitChapters(processed);
        Log($"[DIAG] 拆分为 {chapters.Count} 章");

        Log($"\n{'=' * 60}");
        Log($"📖 模型: {model}");
        Log($"📄 输入: {Path.GetFileName(mdPath)} → 共 {chapters.Count} 章");
        Log($"📝 输出: {Path.GetFileName(NovelComposer.GetNovelPath(mdPath, model))}");
        Log($"{'=' * 60}");

        // 处理所有章节
        var processor = new ChapterProcessor(_client, _systemPrompt, Log, LogError);
        var results = await processor.ProcessAllAsync(chapters, model);

        // 组装并写入
        var novelPath = NovelComposer.ComposeAndWrite(results, mdPath, model, Log);

        var tracker = new TokenTracker();
        foreach (var r in results)
        {
            // Token 统计已在 ChapterProcessor 中输出
        }

        Log($"\n{'=' * 60}");
        Log($"✅ 已保存: {novelPath}\n");

        return novelPath;
    }

    /// <summary>
    /// 从 FormattedTextEntry 数组构建输入 → 调 LLM → 写小说。
    /// 适用于从 JSON 反序列化后直接调用的场景。
    /// </summary>
    public async Task<string> ProcessEntriesAsync(
        IReadOnlyList<FormattedTextEntry> entries,
        string model,
        string outputPath,
        string? sourceLabel = null
    )
    {
        var novelInput = MarkdownBuilder.BuildNovelInput(entries);

        Log($"\n{'=' * 60}");
        Log($"📖 模型: {model}");
        Log($"📄 来源: {sourceLabel ?? "(entries)"} ({novelInput.Length} 字符)");
        Log($"📝 输出: {Path.GetFileName(outputPath)}");
        Log($"{'=' * 60}");

        var result = await _client.ChatAsync(model, _systemPrompt, novelInput);

        File.WriteAllText(outputPath, ChapterSplitter.StripHeadings(result.AnswerContent));

        if (result.Usage is not null)
        {
            Log(
                $"📊 Token: 入 {result.Usage.PromptTokens} / 出 {result.Usage.CompletionTokens} / 共 {result.Usage.TotalTokens}"
            );
        }
        Log($"✅ 已保存: {outputPath}\n");

        return outputPath;
    }

    /// <summary>
    /// 批量处理目录下所有 .md 文件
    /// </summary>
    public async Task BatchProcessAsync(
        string inputDir,
        string[] models,
        bool force,
        string? outputDir = null
    )
    {
        outputDir ??= inputDir;
        Log(
            $"[DIAG] BatchProcessAsync 开始。dir={inputDir}, models=[{string.Join(", ", models)}], force={force}"
        );

        var cache = new ChapterCache(outputDir);

        Log($"[DIAG] 扫描 .md 文件: {inputDir}");
        var mdFiles = Directory
            .GetFiles(inputDir, "*.md", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileNameWithoutExtension(f).Contains("_novel_"))
            .ToArray();
        if (mdFiles.Length == 0)
        {
            Log($"❌ 目录中没有 .md 文件: {inputDir}");
            Log($"[DIAG] 无 .md 文件，BatchProcessAsync 返回");
            return;
        }

        Log($"📂 发现 {mdFiles.Length} 个 .md 文件");
        Log($"[DIAG] 文件列表: {string.Join(", ", mdFiles.Select(Path.GetFileName))}");

        foreach (var mdFile in mdFiles.OrderBy(f => f))
        {
            var fn = Path.GetFileName(mdFile);
            foreach (var model in models)
            {
                Log($"[DIAG] Batch 处理: file={fn}, model={model}");

                var cached = cache.Check(mdFile, model, force);
                if (cached is not null)
                {
                    Log($"⏭️  跳过（缓存命中）: {Path.GetFileName(cached)}");
                    Log($"[DIAG] 缓存命中，跳过: {fn}");
                    continue;
                }

                Log($"[DIAG] 调用 ProcessMdFileAsync: {fn}, {model}");
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    await ProcessMdFileAsync(mdFile, model, outputDir);
                    sw.Stop();
                    Log(
                        $"[DIAG] ProcessMdFileAsync 返回成功，{fn} 耗时 {sw.Elapsed.TotalSeconds:F1}s"
                    );
                    cache.Update(mdFile, model);
                }
                catch (BailianException ex)
                {
                    Log(
                        $"[DIAG] ProcessMdFileAsync 抛出 BailianException: {fn}, {model}, {ex.Message}"
                    );
                    LogError($"❌ [{model}] 失败: {ex.Message}");
                    var failedLog = Path.Combine(outputDir, "failed.txt");
                    File.AppendAllText(
                        failedLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{mdFile}\t{model}\t{ex.Message}\n"
                    );
                }
            }
        }

        Log("\n🏁 批处理完成");
        Log("[DIAG] BatchProcessAsync 执行完毕，即将生成 epub");

        // 为每个小说 md 生成 epub
        await GenerateEpubsForNovelsAsync(outputDir);
    }

    /// <summary>
    /// 为目录下所有小说 MD 生成 epub
    /// </summary>
    private async Task GenerateEpubsForNovelsAsync(string outputDir)
    {
        try
        {
            var novelMdFiles = Directory.GetFiles(outputDir, "*_novel_*.md");
            if (novelMdFiles.Length > 0)
            {
                Log($"[DIAG] 找到 {novelMdFiles.Length} 个小说 MD，开始生成 epub");
                foreach (var mdPath in novelMdFiles)
                {
                    var title = Path.GetFileNameWithoutExtension(mdPath);
                    var epubPath = await PandocService.GenerateEpubAsync(mdPath, title);
                    if (epubPath != null)
                    {
                        Log($"📚 已生成 epub: {Path.GetFileName(epubPath)}");
                    }
                    else
                    {
                        Log($"⚠️  epub 生成失败或 pandoc 不可用: {Path.GetFileName(mdPath)}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[DIAG] epub 生成过程异常: {ex.Message}");
        }
    }
}
