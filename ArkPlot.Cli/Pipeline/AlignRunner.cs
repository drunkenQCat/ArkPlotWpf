using System.Text.Json;
using ArkPlot.Core.Services;
using ArkPlot.Novelizer;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// 对齐命令：将小说化文本与原始 FormattedTextEntry 对齐，输出带角色信息的 JSON。
/// </summary>
public static class AlignRunner
{
    public static async Task RunAsync(string novelFilePath)
    {
        if (!File.Exists(novelFilePath))
        {
            Console.Error.WriteLine($"❌ 文件不存在: {novelFilePath}");
            return;
        }

        Console.WriteLine("=== Novel Aligner ===");
        Console.WriteLine($"输入: {Path.GetFileName(novelFilePath)}");

        var aligner = new NovelAligner();
        var (entries, stats) = await aligner.AlignByFileNameAsync(novelFilePath);

        Console.WriteLine($"\n📊 对齐统计:");
        Console.WriteLine($"   小说章节: {stats.TotalNovelChapters}");
        Console.WriteLine($"   匹配章节: {stats.MatchedChapters}");
        Console.WriteLine($"   总对话数: {stats.TotalDialogs}");
        Console.WriteLine($"   已对齐:   {stats.AlignedDialogs}");
        Console.WriteLine($"   未对齐:   {stats.UnalignedDialogs}");

        // TTS voice 分配预览
        var ttsService = new TtsService();
        var dialogEntries = entries.Where(e => e.IsDialog).ToList();
        var voiceMap = dialogEntries
            .Where(e => e.CharacterName != null)
            .GroupBy(e => e.CharacterName!)
            .Select(g => new
            {
                Character = g.Key,
                Voice = ttsService.GetVoiceForCharacter(g.Key),
                Gender = g.First().Gender ?? "未知",
                DialogCount = g.Count()
            })
            .OrderBy(v => v.Character)
            .ToList();

        if (voiceMap.Count > 0)
        {
            Console.WriteLine($"\n🎤 TTS 音色分配:");
            foreach (var v in voiceMap)
                Console.WriteLine($"   {v.Character} ({v.DialogCount}句, {v.Gender}) → {v.Voice}");
        }

        // 输出 JSON
        var outputDir = Path.GetDirectoryName(novelFilePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(novelFilePath);
        var jsonPath = Path.Combine(outputDir, $"{baseName}_aligned.json");

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(jsonPath, json);

        Console.WriteLine($"\n✅ 对齐结果已保存: {jsonPath}");
        Console.WriteLine($"   总片段数: {entries.Count} (对话: {dialogEntries.Count}, 旁白: {entries.Count - dialogEntries.Count})");
    }
}
