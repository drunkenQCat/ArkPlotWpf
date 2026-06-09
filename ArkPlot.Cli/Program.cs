using ArkPlot.Cli.Pipeline;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;

// 命令行参数解析
if (args.Length > 0 && args[0].Equals("align", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: ArkPlot.Cli align <novel_file.md>");
        return;
    }
    await AlignRunner.RunAsync(args[1]);
    return;
}

if (args.Length > 0 && args[0].Equals("dump-db", StringComparison.OrdinalIgnoreCase))
{
    DbDumpRunner.Run();
    return;
}

if (args.Length > 0 && args[0].Equals("copy-avalonia-db", StringComparison.OrdinalIgnoreCase))
{
    DbDumpRunner.CopyFromAvalonia();
    return;
}

if (args.Length > 0 && args[0].Equals("show-misaligned", StringComparison.OrdinalIgnoreCase))
{
    DbDumpRunner.ShowMisalignedCharacters();
    return;
}

if (args.Length > 0 && args[0].Equals("tts-novel", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: ArkPlot.Cli tts-novel <aligned.json> [segment_limit]");
        return;
    }
    int? limit = args.Length >= 3 && int.TryParse(args[2], out var l) ? l : null;
    await NovelTtsRunner.RunAsync(args[1], limit);
    return;
}

if (args.Length > 0 && args[0].Equals("chapter-tts", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: ArkPlot.Cli chapter-tts <novel_file.md> [--limit N] [--debug-voice]");
        Console.Error.WriteLine("  对小说化 md 按章节生成 MP3（自动对齐 + TTS）");
        Console.Error.WriteLine("  --limit N: 每章只生成前 N 个片段（快速测试）");
        Console.Error.WriteLine("  --debug-voice: 只输出音色分配表，不生成音频");
        return;
    }
    int? limit = null;
    bool debugVoice = false;
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--limit" && i + 1 < args.Length && int.TryParse(args[i + 1], out var l))
        {
            limit = l;
            i++;
        }
        else if (args[i] == "--debug-voice")
        {
            debugVoice = true;
        }
    }
    await ChapterTtsRunner.RunAsync(args[1], limit, debugVoice);
    return;
}

if (args.Length > 0 && args[0].Equals("verify-tts", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: ArkPlot.Cli verify-tts <output_dir> [--segments N]");
        Console.Error.WriteLine("  端到端验证整套 TTS 工作流");
        Console.Error.WriteLine("  --segments N: 测试片段数 (默认 3)");
        return;
    }
    int segments = 3;
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--segments" && i + 1 < args.Length && int.TryParse(args[i + 1], out var s))
        {
            segments = s;
            i++;
        }
    }
    await VerifyTtsRunner.RunAsync(args[1], segments);
    return;
}

if (args.Length > 0 && args[0].Equals("diagnose-tts-assets", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: ArkPlot.Cli diagnose-tts-assets <act_name>");
        Console.Error.WriteLine("  诊断立绘和背景图在 DB 中的真实数据");
        return;
    }
    await DiagnoseTtsAssetsRunner.RunAsync(args[1]);
    return;
}

if (args.Length > 0 && args[0].Equals("simulate-tts-click", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("用法: ArkPlot.Cli simulate-tts-click <novel_file.md> <角色名> [点击第几行]");
        Console.Error.WriteLine("  模拟点击某角色的一行，验证立绘和 Gallery 组件输入");
        return;
    }
    int clickRow = args.Length >= 4 && int.TryParse(args[3], out var r) ? r : 3;
    await SimulateTtsClickRunner.RunAsync(args[1], args[2], clickRow);
    return;
}

if (args.Length > 0 && args[0].Equals("verify-tts-component-inputs", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("用法: ArkPlot.Cli verify-tts-component-inputs <novel_file.md> <角色名>");
        Console.Error.WriteLine("  自动化验证立绘和 Gallery 组件输入是否正确");
        return;
    }
    var exitCode = await VerifyTtsComponentInputsRunner.RunAsync(args[1], args[2]);
    Environment.Exit(exitCode);
    return;
}

// 清空旧的 PicDescription 记录
var db = DbFactory.GetClient();
var before = db.Queryable<PicDescription>().Count();
db.Deleteable<PicDescription>().ExecuteCommand();
Console.WriteLine($"已清空 PicDescription 表（{before} 条旧记录）");

var tagsJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tags.json");
var pipeline = new CliPipeline(tagsJson);
await pipeline.RunAsync();
