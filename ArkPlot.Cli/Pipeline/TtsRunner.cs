using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Cli.Infrastructure;

namespace ArkPlot.Cli.Pipeline;

/// <summary>
/// Step 9: TTS 音频生成。
/// </summary>
public static class TtsRunner
{
    public static async Task RunAsync(
        List<FormattedTextEntry> processedEntries, string outputDir, string actName, string chapterName)
    {
        Console.WriteLine("[9/9] 正在生成 TTS 音频...");
        try
        {
            using var ttsService = new TtsService();
            var audioOutputPath = Path.Combine(outputDir,
                $"{FileHelper.SanitizeFileName(actName)}_{FileHelper.SanitizeFileName(chapterName)}.mp3");

            var entriesWithDialog = processedEntries.Where(e => !string.IsNullOrWhiteSpace(e.Dialog)).ToList();
            var characterVoices = new Dictionary<string, string>();

            foreach (var entry in entriesWithDialog)
            {
                var voice = ttsService.GetVoiceForCharacter(entry.CharacterName ?? "");
                characterVoices.TryAdd(entry.CharacterName ?? "(无)", voice);
            }

            Console.WriteLine("    角色音色分配统计：");
            foreach (var (character, voice) in characterVoices.OrderBy(kv => kv.Key))
                Console.WriteLine($"      {character} → {voice}");

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
            Console.WriteLine("    提示：这不影响 Markdown 导出结果。");
        }
    }
}
