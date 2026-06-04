using ArkPlot.Novelizer;
using ArkPlot.Core.Services;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// 按章节 TTS 命令：对小说化 md 文件按章节生成 MP3 音频。
/// </summary>
public static class ChapterTtsRunner
{
    public static async Task RunAsync(string novelFilePath, int? segmentLimit = null, bool debugVoice = false)
    {
        if (!File.Exists(novelFilePath))
        {
            Console.Error.WriteLine($"❌ 文件不存在: {novelFilePath}");
            return;
        }

        Console.WriteLine("=== Chapter TTS Generator ===");
        Console.WriteLine($"输入: {Path.GetFileName(novelFilePath)}");

        var outputDir = Path.GetDirectoryName(novelFilePath) ?? ".";

        // 调试模式：输出音色分配表
        if (debugVoice)
        {
            await PrintVoiceAssignmentTable(novelFilePath);
            return;
        }

        using var generator = new ChapterTtsGenerator();
        var results = await generator.GenerateAsync(novelFilePath, outputDir, segmentLimit);

        Console.WriteLine($"\n📊 生成统计:");
        Console.WriteLine($"   章节数:   {results.Count}");
        Console.WriteLine($"   总片段:   {results.Sum(r => r.SegmentCount)}");
        Console.WriteLine($"   总对话:   {results.Sum(r => r.DialogCount)}");
        Console.WriteLine($"   总旁白:   {results.Sum(r => r.NarrationCount)}");

        Console.WriteLine($"\n📁 输出文件:");
        foreach (var r in results)
            Console.WriteLine($"   {Path.GetFileName(r.OutputPath)}  ({r.DialogCount} 对话 + {r.NarrationCount} 旁白)");
    }

    private static async Task PrintVoiceAssignmentTable(string novelFilePath)
    {
        var alignedPath = novelFilePath.Replace(".md", "_aligned.json");
        if (!File.Exists(alignedPath))
        {
            Console.Error.WriteLine($"❌ 对齐文件不存在: {alignedPath}");
            Console.Error.WriteLine($"请先运行: ArkPlot.Cli align {novelFilePath}");
            return;
        }

        var json = await File.ReadAllTextAsync(alignedPath);
        var entries = System.Text.Json.JsonSerializer.Deserialize<AlignmentEntry[]>(json) ?? Array.Empty<AlignmentEntry>();

        // 收集第一章角色信息
        var characterStats = new Dictionary<string, (string? Gender, int Count)>();
        foreach (var entry in entries)
        {
            if (!entry.IsDialog || string.IsNullOrEmpty(entry.CharacterName))
                continue;
            
            // 只统计第一章
            if (entry.ChapterTitle != "CR-ST-1 特别参观通道 幕间")
                continue;

            var name = entry.CharacterName;
            if (!characterStats.ContainsKey(name))
                characterStats[name] = (entry.Gender, 0);
            
            var stats = characterStats[name];
            characterStats[name] = (stats.Gender ?? entry.Gender, stats.Count + 1);
        }

        // 输出表格
        Console.WriteLine("\n=== 角色音色分配表 ===\n");
        Console.WriteLine($"{"角色名",-20} {"性别",-6} {"音色",-25} {"对话数",-8} {"状态"}");
        Console.WriteLine(new string('-', 80));

        using var tts = new TtsService();
        var issues = 0;

        foreach (var kvp in characterStats.OrderByDescending(x => x.Value.Count))
        {
            var name = kvp.Key;
            var (gender, count) = kvp.Value;
            var voice = tts.GetVoiceForCharacter(name, gender);
            var voiceName = voice.Replace("zh-CN-", "").Replace("Neural", "");

            // 检查性别匹配
            var isFemaleVoice = voiceName is "Xiaoxiao" or "Xiaoyi" or "Xiaoni" or "Xiaomo" or "liaoning-Xiaobei" or "shaanxi-Xiaoni" or "HsiaoChen" or "HsiaoYu";
            var isMaleVoice = voiceName is "Yunxi" or "Yunyang" or "Yunjian" or "Yunfeng";

            string status;
            if (gender == "女" && isMaleVoice)
            {
                status = "❌ 女角色用了男声";
                issues++;
            }
            else if (gender == "男" && isFemaleVoice)
            {
                status = "❌ 男角色用了女声";
                issues++;
            }
            else if (gender == null)
            {
                status = "⚠️ 性别未知";
            }
            else
            {
                status = "✅";
            }

            Console.WriteLine($"{name,-20} {gender ?? "?",-6} {voiceName,-25} {count,-8} {status}");
        }

        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"\n总计: {characterStats.Count} 个角色, {issues} 个性别不匹配");
    }
}
