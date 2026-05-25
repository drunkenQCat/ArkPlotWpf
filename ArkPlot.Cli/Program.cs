using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities;
using ArkPlot.Core.Utilities.ArknightsDbComponents;
using ArkPlot.Core.Utilities.WorkFlow;
using ArkPlot.Vision;
using Newtonsoft.Json;

namespace ArkPlot.Cli;

/// <summary>
/// CLI 工具：拉取简中插曲第一个活动的第一章，跑完整 Markdown 导出流程，并在导出前 dump JSON 用于验证。
/// </summary>
public class Program
{
    // Debug 开关：开启时强制重写 PicDesc 数据库，方便测试
    private const bool DebugMode = false;

    // Vision 开关：开启时使用 Ollama 视觉模型生成真实图片描述
    private const bool EnableVision = true;

    // TTS 开关：开启时使用 EdgeTTS 生成章节音频
    private const bool EnableTts = true;

    private static async Task Main(string[] args)
    {
        Console.WriteLine("=== ArkPlot CLI - 完整流程验证 ===");
        Console.WriteLine($"目标：简中 ACTIVITY_STORY 第一个活动 → 第一章 → 完整解析流程 → Dump JSON → 导出 Markdown");
        Console.WriteLine($"Debug 模式：{(DebugMode ? "开启（强制重写 PicDesc）" : "关闭")}");
        Console.WriteLine($"Vision 模式：{(EnableVision ? "开启（Ollama 生成真实图片描述）" : "关闭（使用占位符）")}");
        Console.WriteLine($"TTS 模式：{(EnableTts ? "开启（EdgeTTS 生成章节音频）" : "关闭")}");
        Console.WriteLine();

        var tagsJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tags.json");
        if (!File.Exists(tagsJson))
        {
            Console.WriteLine($"❌ 找不到 tags.json，预期路径：{tagsJson}");
            Console.WriteLine("   请确保 ArkPlot.Avalonia/tags.json 已复制到 CLI 输出目录。");
            return;
        }

        try
        {
            // 1. 加载活动列表
            Console.WriteLine("[1/6] 正在加载活动列表...");
            var actsTable = new ReviewTableParser("zh_CN");
            var activities = actsTable.GetStories("ACTIVITY_STORY");

            if (activities.Count == 0)
            {
                Console.WriteLine("❌ 未找到任何 ACTIVITY_STORY 类型的活动。");
                return;
            }

            // 2. 获取第一个活动
            var firstAct = activities[0];
            var actName = firstAct["name"]?.ToString() ?? "未知活动";
            Console.WriteLine($"    活动：{actName}");

            // 3. 创建 ActInfo 并加载章节列表
            var actInfo = new ActInfo("zh_CN", "ACTIVITY_STORY", actName, firstAct);
            var storyLoader = new AkpStoryLoader(actInfo);

            Console.WriteLine("[2/6] 正在获取章节名称...");
            var chapterNames = await storyLoader.GetChapterNamesAsync();
            var chapterList = chapterNames.ToList();

            if (chapterList.Count == 0)
            {
                Console.WriteLine("❌ 该活动没有章节。");
                return;
            }

            var firstChapter = chapterList[0];
            Console.WriteLine($"    第一章：{firstChapter}");

            // 4. 下载第一章
            Console.WriteLine("[3/6] 正在下载章节内容...");
            await storyLoader.GetAllChapters(new[] { firstChapter });

            if (storyLoader.ContentTable.Count == 0)
            {
                Console.WriteLine("❌ 未成功下载任何内容。");
                Console.WriteLine("   请检查网络连接（需要访问 GitHub）。");
                return;
            }

            var plotManager = storyLoader.ContentTable[0];
            var rawContentLength = plotManager.CurrentPlot.Content.Length;
            Console.WriteLine($"    原始内容长度：{rawContentLength} 字符");

            if (rawContentLength == 0)
            {
                Console.WriteLine("⚠️ 章节内容为空，可能是网络问题导致下载失败。");
                Console.WriteLine("   请检查网络连接后重试。");
                return;
            }

            // 5. 预加载 Prts 资源索引（填充 ResourceUrls、Bg、PortraitsInfo 等字段）
            Console.WriteLine("[4/8] 正在加载 Prts 资源索引...");
            var prts = new ArkPlot.Core.Utilities.PrtsComponents.PrtsDataProcessor();
            var prtsLoaded = false;
            try
            {
                await prts.GetAllData();
                prtsLoaded = true;
                Console.WriteLine($"    Prts 资源索引加载完成");
            }
            catch (Exception prtsEx)
            {
                Console.WriteLine($"    ⚠️ Prts 索引加载失败（{prtsEx.Message}），跳过 ResourceUrls 填充");
                Console.WriteLine($"    提示：PicDesc 将没有输入数据，但流程仍然可验证");
            }

            Console.WriteLine("[5/8] 正在预加载资源（填充 ResourceUrls 等字段）...");
            var processedEntries = plotManager.CurrentPlot.TextVariants;
            int entriesWithUrls = 0;
            int preloadCount = 0;

            if (prtsLoaded)
            {
                var preloadInfo = storyLoader.GetPreloadInfo();
                preloadCount = preloadInfo.Count;
                entriesWithUrls = processedEntries.Count(e => e.ResourceUrls.Count > 0);
            }
            else
            {
                // 优雅降级：手动运行 PrtsPreloader（即使没有本地索引也会填充部分字段）
                try
                {
                    var preloader = new ArkPlot.Core.Utilities.PrtsComponents.PrtsPreloader(plotManager);
                    preloader.ParseAndCollectAssets();
                    entriesWithUrls = processedEntries.Count(e => e.ResourceUrls.Count > 0);
                    preloadCount = preloader.Assets.Count;
                }
                catch
                {
                    entriesWithUrls = 0;
                    preloadCount = 0;
                }
            }

            Console.WriteLine($"    资源条目：{preloadCount}");
            Console.WriteLine($"    有 ResourceUrls 的条目：{entriesWithUrls}");

            // 6. 解析文档（完整的 Markdown 转换流程）
            Console.WriteLine("[6/8] 正在解析文档（AkpParser → PlotManager.StartParseLines）...");
            var parser = new AkpParser(tagsJson);
            plotManager.StartParseLines(parser);

            Console.WriteLine($"    解析完成，共 {processedEntries.Count} 个条目");
            Console.WriteLine($"    有效 MdText 条目：{processedEntries.Count(e => !string.IsNullOrWhiteSpace(e.MdText))}");
            Console.WriteLine($"    有效 TypText 条目：{processedEntries.Count(e => !string.IsNullOrWhiteSpace(e.TypText))}");
            Console.WriteLine($"    有 ResourceUrls 的条目：{processedEntries.Count(e => e.ResourceUrls.Count > 0)}");

            // Debug 模式：如果没有 ResourceUrls，注入 mock 数据用于验证 PicDesc 流程
            var currentEntriesWithUrls = processedEntries.Count(e => e.ResourceUrls.Count > 0);
            if (DebugMode && currentEntriesWithUrls == 0)
            {
                Console.WriteLine("    ⚠️ 无 ResourceUrls，注入 mock 数据用于验证 PicDesc 流程...");
                var mockUrls = new List<string>
                {
                    "https://media.prts.wiki/a/ab/Avg_char_293_thorns_1.png",
                    "https://media.prts.wiki/b/bc/Avg_bg_bg_med.png",
                    "https://media.prts.wiki/c/cd/Avg_npc_009.png"
                };

                var mockCounter = 0;
                foreach (var entry in processedEntries)
                {
                    if (entry.Type.Contains("char", StringComparison.OrdinalIgnoreCase) && mockCounter < mockUrls.Count)
                    {
                        entry.ResourceUrls = [mockUrls[mockCounter]];
                        mockCounter++;
                    }
                    else if (entry.Type.Contains("background", StringComparison.OrdinalIgnoreCase) && mockCounter < mockUrls.Count)
                    {
                        entry.ResourceUrls = [mockUrls[mockCounter]];
                        mockCounter++;
                    }
                }
                currentEntriesWithUrls = processedEntries.Count(e => e.ResourceUrls.Count > 0);
                Console.WriteLine($"    ✅ 已注入 {currentEntriesWithUrls} 条 mock ResourceUrls");
            }

            // 7. 运行 MdReconstructor（带 PicDescService），填充 PicDesc 字段
            Console.WriteLine("[7/8] 正在运行 MdReconstructor（填充 PicDesc）...");
            Func<string, Task<string>>? describeByUrl = null;
            IDisposable? visionDisposable = null;
            if (EnableVision)
            {
                try
                {
                    // 优先使用百炼（直接传 URL，无需下载图片）
                    var bailianApiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? "";
                    if (!string.IsNullOrEmpty(bailianApiKey))
                    {
                        var bailianConfig = new BailianVisionConfig
                        {
                            ApiKey = bailianApiKey,
                            Model = "qwen3-vl-flash",
                            TimeoutSeconds = 60,
                            SystemPrompt = "请用中文详细描述这张图片中的所有视觉元素，包括角色、场景、动作、服饰、背景等细节。直接输出描述内容，不要加任何前缀或总结性语句。",
                            MaxTokens = 2048
                        };
                        var bailianClient = new BailianVisionClient(bailianConfig, onLog: msg => Console.WriteLine($"  [Vision] {msg}"));
                        describeByUrl = async (url) => await bailianClient.DescribeImageUrlAsync(url);
                        visionDisposable = bailianClient;
                        Console.WriteLine("    ✅ 百炼视觉客户端已初始化（qwen3-vl-flash，直接 URL 调用）");
                    }
                    else
                    {
                        // 回退到 Ollama（需要下载图片）
                        Console.WriteLine("    ⚠️ 未配置 DASHSCOPE_API_KEY，尝试使用 Ollama...");
                        try
                        {
                            var visionConfig = new VisionConfig
                            {
                                BaseUrl = "http://localhost:11434",
                                Model = "qwen3-vl:8b",
                                TimeoutSeconds = 600,
                                SystemPrompt = "请用中文详细描述这张图片中的所有视觉元素，包括角色、场景、动作、服饰、背景等细节。直接输出描述内容，不要加任何前缀或总结性语句。",
                                Temperature = 0.7f,
                                MaxTokens = 2048
                            };
                            var ollamaClient = new OllamaVisionClient(visionConfig, onLog: msg => Console.WriteLine($"  [Vision] {msg}"));
                            describeByUrl = async (url) =>
                            {
                                // Ollama 需要下载图片到本地
                                var tempPath = Path.GetTempFileName();
                                try
                                {
                                    using var http = new HttpClient();
                                    http.Timeout = TimeSpan.FromMinutes(5);
                                    var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                                    response.EnsureSuccessStatusCode();
                                    await using var fs = File.Create(tempPath);
                                    await (await response.Content.ReadAsStreamAsync()).CopyToAsync(fs);
                                    return await ollamaClient.DescribeImageAsync(tempPath);
                                }
                                finally
                                {
                                    if (File.Exists(tempPath)) File.Delete(tempPath);
                                }
                            };
                            visionDisposable = ollamaClient;
                            Console.WriteLine("    ✅ Ollama 视觉客户端已初始化（需要下载图片）");
                        }
                        catch
                        {
                            Console.WriteLine("    ⚠️ Ollama 不可用，将使用占位符模式。");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ⚠️ 视觉客户端初始化失败：{ex.Message}");
                    Console.WriteLine("    将使用占位符模式生成图片描述。");
                }
            }

            using var picDescService = new PicDescService(describeByUrl, debugMode: DebugMode);
            if (visionDisposable != null) { /* dispose at end of scope */ }
            picDescService.InitializeCleanup();
            var reconstructorDone = false;
            try
            {
                foreach (var plotManagerEntry in storyLoader.ContentTable)
                {
                    var textList = plotManagerEntry.CurrentPlot.TextVariants;
                    _ = new MdReconstructor(textList, picDescService);
                }
                reconstructorDone = true;
            }
            finally
            {
                visionDisposable?.Dispose();
            }
            if (!reconstructorDone) return;
            var picDescEntries = processedEntries.Count(e => !string.IsNullOrWhiteSpace(e.PicDesc));
            Console.WriteLine($"    ✅ MdReconstructor 完成，PicDesc 条目：{picDescEntries}");

            // 7. Dump JSON（按 Model 定义，导出前验证用）
            Console.WriteLine("[7/8] 正在 Dump JSON（导出前验证）...");
            var dumpResult = DumpPlotToJson(plotManager.CurrentPlot, actName, firstChapter);
            Console.WriteLine($"    ✅ 已保存：{dumpResult.DumpPath}");
            Console.WriteLine($"    📊 统计：{dumpResult.Stats}");
            Console.WriteLine($"    🖼️  PicDesc 条目：{dumpResult.PicDescCount}");

            // 8. 导出 Markdown
            Console.WriteLine("[8/8] 正在导出 Markdown...");
            var mdContent = AkpProcessor.ExportPlots(storyLoader.ContentTable, picDescService);
            var mdWithTitle = $"# {actName}\n\n{mdContent}";
            var markdown = new Plot(actName, new System.Text.StringBuilder(mdWithTitle));

            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cli_output");
            Directory.CreateDirectory(outputDir);
            AkpProcessor.WriteMd(outputDir, markdown);

            var picDescStats = picDescService.GetStats();
            Console.WriteLine($"    ✅ Markdown 已保存：{outputDir}");
            Console.WriteLine($"    📄 文件大小：{markdown.Content.Length} 字符");
            Console.WriteLine($"    🗄️  PicDesc 数据库：总计 {picDescStats.DbCount} 条记录");
            Console.WriteLine($"    📁 图片缓存目录：{picDescStats.CacheFileCount} 个文件，{picDescStats.CacheSizeBytes / 1024} KB");

            // 9. 生成 TTS 音频（可选）
            if (EnableTts)
            {
                Console.WriteLine("[9/9] 正在生成 TTS 音频...");
                try
                {
                    using var ttsService = new TtsService();
                    var audioOutputPath = Path.Combine(outputDir, $"{SanitizeFileName(actName)}_{SanitizeFileName(firstChapter)}.mp3");
                    
                    Console.WriteLine($"    角色音色分配统计：");
                    var entriesWithDialog = processedEntries.Where(e => !string.IsNullOrWhiteSpace(e.Dialog)).ToList();
                    var characterVoices = new Dictionary<string, string>();
                    
                    foreach (var entry in entriesWithDialog)
                    {
                        var voice = ttsService.GetVoiceForCharacter(entry.CharacterName ?? "");
                        if (!characterVoices.ContainsKey(entry.CharacterName ?? "(无)"))
                        {
                            characterVoices[entry.CharacterName ?? "(无)"] = voice;
                        }
                    }
                    
                    foreach (var (character, voice) in characterVoices.OrderBy(kv => kv.Key))
                    {
                        Console.WriteLine($"      {character} → {voice}");
                    }
                    
                    Console.WriteLine($"\n    开始合成 {entriesWithDialog.Count} 条对话...");
                    await ttsService.GenerateChapterAudioAsync(processedEntries, audioOutputPath);
                    
                    var ttsStats = ttsService.GetStats();
                    Console.WriteLine($"    🎵 音频文件：{audioOutputPath}");
                    Console.WriteLine($"    👥 已分配音色角色数：{ttsStats.CharacterVoiceCount}");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("    ⚠️ TTS 生成已取消。");
                }
                catch (Exception ttsEx)
                {
                    Console.WriteLine($"    ⚠️ TTS 生成失败：{ttsEx.Message}");
                    Console.WriteLine($"    提示：这不影响 Markdown 导出结果。");
                }
            }

            Console.WriteLine("\n=== 流程完成 ===");
            Console.WriteLine($"活动：{actName}");
            Console.WriteLine($"章节：{firstChapter}");
            Console.WriteLine($"Dump JSON：{dumpResult.DumpPath}");
            Console.WriteLine($"Markdown：{Path.Combine(outputDir, $"{markdown.Title}.md")}");
            Console.WriteLine($"PicDesc 数据库：{picDescStats.DbCount} 条记录");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 发生错误：{ex.Message}");
            Console.WriteLine($"详细：{ex}");
        }
    }

    /// <summary>
    /// 按 Model 定义 dump Plot 对象为 JSON，用于验证输出结果。
    /// </summary>
    private static DumpResult DumpPlotToJson(Plot plot, string actName, string chapterName)
    {
        var picDescCount = plot.TextVariants.Count(e => !string.IsNullOrWhiteSpace(e.PicDesc));

        var dump = new PlotDump
        {
            Meta = new DumpMeta
            {
                Activity = actName,
                Chapter = chapterName,
                Title = plot.Title,
                DumpTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TotalEntries = plot.TextVariants.Count,
                ValidMdEntries = plot.TextVariants.Count(e => !string.IsNullOrWhiteSpace(e.MdText)),
                ValidTypEntries = plot.TextVariants.Count(e => !string.IsNullOrWhiteSpace(e.TypText))
            },
            TextVariants = plot.TextVariants.Select(entry => new FormattedTextEntryDump
            {
                Index = entry.Index,
                OriginalText = entry.OriginalText,
                MdText = entry.MdText,
                MdDuplicateCounter = entry.MdDuplicateCounter,
                TypText = entry.TypText,
                Type = entry.Type,
                IsTagOnly = entry.IsTagOnly,
                CharacterName = entry.CharacterName,
                Dialog = entry.Dialog,
                PngIndex = entry.PngIndex,
                Bg = entry.Bg,
                ResourceUrls = entry.ResourceUrls,
                PortraitsInfo = entry.PortraitsInfo,
                CommandSet = entry.CommandSet,
                PicDesc = entry.PicDesc
            }).ToList()
        };

        var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cli_output");
        Directory.CreateDirectory(outputDir);

        var fileName = SanitizeFileName($"{actName}_{chapterName}");
        var dumpPath = Path.Combine(outputDir, $"{fileName}_dump.json");

        var jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        var json = JsonConvert.SerializeObject(dump, jsonSettings);
        File.WriteAllText(dumpPath, json, System.Text.Encoding.UTF8);

        var stats = $"Total={dump.Meta.TotalEntries}, Md={dump.Meta.ValidMdEntries}, Typ={dump.Meta.ValidTypEntries}";
        return new DumpResult(dumpPath, stats, picDescCount);
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    private record DumpResult(string DumpPath, string Stats, int PicDescCount);
}

/// <summary>
/// Dump 用的 DTO，按照 Model 定义序列化。
/// </summary>
public class PlotDump
{
    [JsonProperty("meta")]
    public required DumpMeta Meta { get; init; }

    [JsonProperty("text_variants")]
    public required List<FormattedTextEntryDump> TextVariants { get; init; }
}

public class DumpMeta
{
    [JsonProperty("activity")]
    public required string Activity { get; init; }

    [JsonProperty("chapter")]
    public required string Chapter { get; init; }

    [JsonProperty("title")]
    public required string Title { get; init; }

    [JsonProperty("dump_time")]
    public required string DumpTime { get; init; }

    [JsonProperty("total_entries")]
    public required int TotalEntries { get; init; }

    [JsonProperty("valid_md_entries")]
    public required int ValidMdEntries { get; init; }

    [JsonProperty("valid_typ_entries")]
    public required int ValidTypEntries { get; init; }
}

public class FormattedTextEntryDump
{
    [JsonProperty("index")]
    public int Index { get; init; }

    [JsonProperty("original_text")]
    public string OriginalText { get; init; } = "";

    [JsonProperty("md_text")]
    public string MdText { get; init; } = "";

    [JsonProperty("md_duplicate_counter")]
    public int MdDuplicateCounter { get; init; }

    [JsonProperty("typ_text")]
    public string TypText { get; init; } = "";

    [JsonProperty("type")]
    public string Type { get; init; } = "";

    [JsonProperty("is_tag_only")]
    public bool IsTagOnly { get; init; }

    [JsonProperty("character_name")]
    public string CharacterName { get; init; } = "";

    [JsonProperty("dialog")]
    public string Dialog { get; init; } = "";

    [JsonProperty("png_index")]
    public int PngIndex { get; init; }

    [JsonProperty("bg")]
    public string Bg { get; init; } = "";

    [JsonProperty("resource_urls")]
    public List<string> ResourceUrls { get; init; } = new();

    [JsonProperty("portraits_info")]
    public PortraitInfo PortraitsInfo { get; init; } = new(new List<string>(), 0);

    [JsonProperty("command_set")]
    public StringDict CommandSet { get; init; } = new();

    [JsonProperty("pic_desc")]
    public string PicDesc { get; init; } = "";
}
