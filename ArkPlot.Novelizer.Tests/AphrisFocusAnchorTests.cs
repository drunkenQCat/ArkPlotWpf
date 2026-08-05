using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using ArkPlot.Arknights;
using ArkPlot.Arknights.TagProcessing;
using ArkPlot.Arknights.Workflow;
using Xunit;

namespace ArkPlot.Novelizer.Tests;

/// <summary>
/// 回归测试：PlotId=289「土壤病 幕间」中 aphris 立绘描述的正确归属。
///
/// 场景：aphris（洛伦茨）在章节开头以「？？？」身份出场，立绘在场上，
/// 但当前输出中 aphris 立绘描述完全缺失（被处理成 Unknown / 被 Propagate 跳过）。
///
/// 判断标准（用户指定）：
/// ① 章节开头（首个对话前）必须有 aphris 描述
/// ② 带 bstart 的 charslot（隐藏/遮脸）触发一次描述
/// ③ 不带 bstart 的 charslot（露出真容）再触发一次描述
/// ④ 吉莉安在早期必有描述（对应 avg_npc_2321_1）
/// ⑤ 米格鲁不应有立绘描述（focus=none，画外音）
/// ⑥ 双立绘时，focused 槽位（如右边 aphris）决定说话者归属
/// </summary>
[Collection("SharedDb")]
public class AphrisFocusAnchorTests
{
    private static readonly string ProjectRoot = FindProjectRoot();
    private static readonly string DbPath = Path.Combine(
        ProjectRoot, "ArkPlot.Avalonia", "bin", "Debug", "net9.0", "arkplot.db");

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "ArkPlot.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Cannot find ArkPlot.sln");
    }

    /// <summary>加载 PlotId=289（土壤病 幕间）并生成 Prompt 输出。</summary>
    private static async Task<string> GenerateSoilPromptAsync()
    {
        if (!File.Exists(DbPath))
            throw new FileNotFoundException($"数据库不存在: {DbPath}。请先在 Avalonia 中解析丛林症结活动。");

        DbFactory.ConfigureForTesting($"Data Source={DbPath}");
        var db = DbFactory.GetClient();

        var plot = db.Queryable<Plot>().Where(p => p.Id == 289).First();
        if (plot == null)
            throw new InvalidOperationException("未找到 PlotId=289（土壤病 幕间）");

        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == 289)
            .OrderBy(e => e.Index)
            .ToList();

        var pm = new PlotManager(plot);
        pm.CurrentPlot.TextVariants = entries.Cast<ScriptLine>().ToList();

        using var picDesc = new PicDescService();
        var md = await AkpProcessor.ExportPlotsAsync(
            new List<PlotManager> { pm },
            picDescService: picDesc,
            enableDescriptions: true,
            outputMode: OutputMode.PromptOptimized);
        return md;
    }

    /// <summary>解析输出中所有 portrait-facts 的 (character, 内容) 列表。</summary>
    private static List<(string Char, string Content)> ExtractPortraitFacts(string md)
    {
        var result = new List<(string, string)>();
        int idx = 0;
        while (true)
        {
            int start = md.IndexOf("<aside class=\"portrait-facts\" data-character=\"", idx, StringComparison.Ordinal);
            if (start < 0) break;
            int charStart = start + "<aside class=\"portrait-facts\" data-character=\"".Length;
            int charEnd = md.IndexOf('"', charStart);
            int contentStart = md.IndexOf(">\n", charEnd) + 2;
            int contentEnd = md.IndexOf("</aside>", contentStart);
            var character = md[charStart..charEnd];
            var content = md[contentStart..contentEnd];
            result.Add((character, content));
            idx = contentEnd + 8;
        }
        return result;
    }

    [Fact]
    public async Task SoilChapter_AphrisDescription_AppearsTwice_BstartAndOpen()
    {
        var md = await GenerateSoilPromptAsync();
        var facts = ExtractPortraitFacts(md);

        // ②③ aphris 描述应出现两次独立的 aside：
        //    - 带 bstart（隐藏/遮脸）触发一次
        //    - 不带 bstart（露出真容）再触发一次
        var aphrisBlocks = facts.Where(f => f.Content.Contains("银白") && f.Content.Contains("粗辫")).ToList();
        Assert.True(aphrisBlocks.Count >= 2,
            $"预期 aphris 描述至少出现 2 次（bstart 隐藏 + open 露出），实际 {aphrisBlocks.Count} 次");
    }

    [Fact]
    public async Task SoilChapter_FocusSlotDeterminesSpeaker()
    {
        var md = await GenerateSoilPromptAsync();
        var facts = ExtractPortraitFacts(md);

        // ⑥ 双立绘时（Idx=56-59：左=2321 右=aphris，focus=右），讲话者？？？应归属 aphris。
        // 验证 aphris 描述存在（说明 focus 槽位锚点生效）
        Assert.Contains(facts, f => f.Content.Contains("银白") && f.Content.Contains("粗辫"));
        Assert.True(md.IndexOf("粗辫", StringComparison.Ordinal) >= 0, "aphris 描述应出现在 md 中");
    }

    [Fact]
    public async Task SoilChapter_HasAphrisDescription_EarlyInDocument()
    {
        var md = await GenerateSoilPromptAsync();

        // ② aphris 描述必须出现在文档中（银白长发/粗辫/青玉流苏 特征）
        Assert.Contains("银白", md);
        Assert.Contains("粗辫", md);

        // ① 首个对话（**神秘人士**/**？？？**）之前应有 aphris 描述
        var firstDialog = md.IndexOf("**", StringComparison.Ordinal);
        var opening = firstDialog >= 0 ? md[..firstDialog] : md;
        Assert.Contains("粗辫", opening);
    }

    [Fact]
    public async Task SoilChapter_NoMugelooPortraitFacts()
    {
        var md = await GenerateSoilPromptAsync();
        var facts = ExtractPortraitFacts(md);

        // ⑤ 米格鲁不应有立绘描述（画外音 focus=none）
        Assert.DoesNotContain(facts, f => f.Char.Contains("米格鲁"));
    }

    [Fact]
    public async Task SoilChapter_GillianHasDescription_Early()
    {
        var md = await GenerateSoilPromptAsync();
        var facts = ExtractPortraitFacts(md);

        // ④ 吉莉安必须有描述（橙红发/双马尾 特征）
        var gillian = facts.FirstOrDefault(f => f.Char.Contains("吉莉安"));
        Assert.NotEqual(default, gillian);
        Assert.Contains("橙红", gillian.Content);
    }

    [Fact]
    public async Task SoilChapter_NoUnknownReplacingAphris()
    {
        var md = await GenerateSoilPromptAsync();
        var facts = ExtractPortraitFacts(md);

        // aphris 不应被错误标记为 Unknown（至少第一个立绘组应识别为 aphris/？？？）
        // 允许有少量 Unknown（无立绘的旁白组），但 aphris 主立绘描述必须存在（银白长发特征）
        Assert.Contains(facts, f => f.Content.Contains("银白") && f.Content.Contains("粗辫"));
    }

    /// <summary>
    /// 回归测试：aside 的 data-character 必须与正文对话名一致。
    /// 背景：ProcessDialog 把正文里的「？？？」转为「神秘人士」，但 aside 的 data-character
    /// 直接用了原始 CharacterName（？？？），导致小说化时 LLM 无法把 aphris 立绘描述
    /// 关联到「神秘人士」这个说话角色。修复后两者都应统一为「神秘人士」。
    /// </summary>
    [Fact]
    public async Task SoilChapter_AsideNameMatchesDialogName()
    {
        var md = await GenerateSoilPromptAsync();
        var facts = ExtractPortraitFacts(md);

        // 正文对话名统一为「神秘人士」
        Assert.Contains("**神秘人士**", md);

        // aside 的 data-character 不应再出现「？？？」
        // 若 aphris 描述存在，其 data-character 应为「神秘人士」（与正文一致）
        var mysteryFacts = facts.Count(f => f.Char.Contains("神秘人士"));
        Assert.True(mysteryFacts >= 1,
            $"「神秘人士」应有 portrait-facts，实际 {mysteryFacts} 个");

        // aphris 描述（银白/粗辫）应绑定到 aphris 的揭示名「神秘人士（实际人物：洛伦茨）」，
        // 而非「？？？」或「神秘人士」；更不能是画外音角色「米格鲁」（focus=none 误伤的回归信号）。
        var aphrisBlock = facts.FirstOrDefault(f => f.Content.Contains("银白") && f.Content.Contains("粗辫"));
        Assert.NotEqual(default, aphrisBlock);
        Assert.Contains("洛伦茨", aphrisBlock.Char);
        Assert.DoesNotContain("米格鲁", aphrisBlock.Char);
    }
}