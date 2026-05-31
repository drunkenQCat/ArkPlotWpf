using ArkPlot.Core.Model;

namespace ArkPlot.Novelizer;

/// <summary>
/// 小说化管线：读 Markdown → 拆章 → 逐章调 LLM → 合并写小说文件
/// </summary>
public class NovelizerPipeline
{
    private readonly BailianClient _client;
    private readonly ApiConfig _config;
    private readonly Action<string>? _onLog;
    private readonly string _systemPrompt;

    private const string DefaultSystemPrompt = """
## 明日方舟剧情小说化转换协议

### 一、 叙事视角与文体调性

* **视角规范**：严格采用**第三人称有限视角**。叙述焦点应锚定于当前场境的核心角色，通过其感官、逻辑与职责边界推进叙事。
* **文体特征**：承袭《孤星》、《巴别塔》、《乌萨斯的孩子们》等剧作的冷峻、克制与思辨感。剔除网络文学的夸张修辞、影视解说的全知旁白，以及短视频式的断句节奏。
* **组织结构**：采用传统严肃文学的长段落结构。一个合格的叙事单元须将**环境异动、角色动作、观察结果、心理推演**有机融合，严禁无故出现连续的单句成段或刻意制造悬念的破折号断句。

### 二、 视听语言的叙事转化

* **场景（背景图）**：严禁概括性描述（如“满目疮痍”、“气氛死寂”）。必须将场景拆解为具象的叙事客体——例如通过“管线渗出的冷却液腐蚀了地板”、“终端屏幕跳动的异常波形”来交待环境的破败与危机。
* **动态（立绘/演出）**：禁止机械性复述角色的神态变化（如“眉头紧锁”、“露出微笑”）。角色外貌除首秀或服务于核心剧情外不予赘述。将立绘的细微调整转化为角色内在的思维波动或肢体战术动作。
* **声音（音乐/音效）**：严禁出现任何提及音乐或音效的字眼。
* *BGM变化* $\rightarrow$ 转化为叙事张力的松紧、对话密度的调整或环境压迫感的骤增。
* *效能音（爆破、警报、开门）* $\rightarrow$ 直接转化为物理层面的环境剧变或不可逆的事实发生。



### 三、 角色声音与行为逻辑

* **身份锚定**：角色台词与行动必须符合其泰拉世界的职业背景与社会阶层。
* *指挥官（如博士、凯尔希）*：全局审视，剥离主观情绪，计算战损与概率，聚焦于决策推进。
* *技术/医疗人员*：定量分析，关注生理体征数据、源石病扩散速率及设备参数。
* *一线干员*：战术占位、危险感知、路径规划，台词去长期化、指令化。


* **去模版化**：严禁滥用“顿了顿”、“轻轻叹气”、“抬起头看向对方”等无意义的通用机械动作。每一次交互必须带有明确的战术意图或心理动机。

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
        _config = config;
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
