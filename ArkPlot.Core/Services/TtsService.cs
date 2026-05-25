using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EdgeTTS.DotNet;
using NAudio.Wave;
using ArkPlot.Core.Model;

namespace ArkPlot.Core.Services;

/// <summary>
/// TTS 服务：使用 EdgeTTS 将角色对话转换为语音，并合并为单个 MP3 文件。
/// </summary>
public class TtsService : IDisposable
{
    /// <summary>
    /// 中文音色池（按性别分组）
    /// </summary>
    private static readonly string[] FemaleVoices = new[]
    {
        "zh-CN-XiaoxiaoNeural",  // 温暖
        "zh-CN-XiaoyiNeural",    // 活泼
        "zh-CN-liaoning-XiaobeiNeural",  // 幽默
        "zh-CN-shaanxi-XiaoniNeural"      // 明亮
    };

    private static readonly string[] MaleVoices = new[]
    {
        "zh-CN-YunxiNeural",     // 活泼、阳光
        "zh-CN-YunjianNeural",   // 激情
        "zh-CN-YunxiaNeural",    // 可爱
        "zh-CN-YunyangNeural"    // 专业、可靠
    };

    /// <summary>
    /// 默认音色（无角色名时使用）
    /// </summary>
    private const string DefaultVoice = "zh-CN-XiaoxiaoNeural";

    /// <summary>
    /// 角色音色缓存（角色名 → 音色）
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _characterVoiceCache = new();

    /// <summary>
    /// 语速调整（默认 +0%）
    /// </summary>
    private readonly string _rate;

    /// <summary>
    /// 音量调整（默认 +0%）
    /// </summary>
    private readonly string _volume;

    /// <summary>
    /// 临时文件目录
    /// </summary>
    private readonly string _tempDir;

    /// <summary>
    /// 创建 TtsService 实例。
    /// </summary>
    /// <param name="rate">语速调整，如 "+10%"、"-5%"，默认 "+0%"。</param>
    /// <param name="volume">音量调整，如 "+10%"、"-5%"，默认 "+0%"。</param>
    /// <param name="tempDir">临时文件目录，默认为 Path.GetTempPath() 下的随机目录。</param>
    public TtsService(string rate = "+0%", string volume = "+0%", string? tempDir = null)
    {
        _rate = rate;
        _volume = volume;
        _tempDir = tempDir ?? Path.Combine(Path.GetTempPath(), $"arkplot_tts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// 根据角色名获取音色。
    /// 无角色名时返回默认音色 XiaoXiao。
    /// </summary>
    /// <param name="characterName">角色名，为空时返回默认音色。</param>
    /// <returns>音色名称。</returns>
    public string GetVoiceForCharacter(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return DefaultVoice;
        }

        return _characterVoiceCache.GetOrAdd(characterName, name =>
        {
            var hash = GetStableHash(name);
            var isFemale = hash % 2 == 0; // 偶数为女声，奇数为男声
            var voicePool = isFemale ? FemaleVoices : MaleVoices;
            var index = Math.Abs(hash) % voicePool.Length;
            return voicePool[index];
        });
    }

    /// <summary>
    /// 将单句台词转换为 MP3 音频文件。
    /// </summary>
    /// <param name="text">台词文本。</param>
    /// <param name="voice">音色名称，如 "zh-CN-XiaoxiaoNeural"。</param>
    /// <param name="outputPath">输出 MP3 文件路径。</param>
    public async Task SynthesizeAsync(string text, string voice, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("台词文本不能为空", nameof(text));
        }

        var request = new Communicate(text, voice: voice, rate: _rate, volume: _volume);
        await request.SaveAsync(outputPath);
    }

    /// <summary>
    /// 批量处理章节所有对话，合并为单个 MP3 文件。
    /// </summary>
    /// <param name="entries">格式化文本条目列表。</param>
    /// <param name="outputPath">输出 MP3 文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task GenerateChapterAudioAsync(
        List<FormattedTextEntry> entries,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var tempFiles = new List<string>();

        try
        {
            // 1. 为每个有对话的条目生成音频
            var audioFiles = new List<(string FilePath, int Index)>();

            foreach (var entry in entries.Where(e => !string.IsNullOrWhiteSpace(e.Dialog)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var voice = GetVoiceForCharacter(entry.CharacterName ?? "");
                var tempFile = Path.Combine(_tempDir, $"dialog_{entry.Index:D6}.mp3");

                Console.WriteLine($"  [{entry.Index}] 角色: {entry.CharacterName ?? "(无)"} | 音色: {voice}");
                Console.WriteLine($"      台词: {entry.Dialog[..Math.Min(50, entry.Dialog.Length)]}...");

                await SynthesizeAsync(entry.Dialog, voice, tempFile);
                audioFiles.Add((tempFile, entry.Index));
                tempFiles.Add(tempFile);
            }

            if (audioFiles.Count == 0)
            {
                Console.WriteLine("  ⚠️ 没有可合成的对话，跳过 TTS。");
                return;
            }

            // 2. 按索引排序
            audioFiles = audioFiles.OrderBy(a => a.Index).ToList();

            // 3. 合并所有音频
            Console.WriteLine($"\n  正在合并 {audioFiles.Count} 个音频文件...");
            MergeAudioFiles(audioFiles.Select(a => a.FilePath).ToList(), outputPath);

            Console.WriteLine($"  ✅ 章节音频已生成：{outputPath}");
        }
        finally
        {
            // 4. 清理临时文件
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // 忽略删除失败
                }
            }
        }
    }

    /// <summary>
    /// 合并多个 MP3 文件为单个 MP3 文件。
    /// </summary>
    /// <param name="inputFiles">输入 MP3 文件列表（按顺序）。</param>
    /// <param name="outputFile">输出 MP3 文件路径。</param>
    private static void MergeAudioFiles(List<string> inputFiles, string outputFile)
    {
        using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        var readers = new List<AudioFileReader>();

        try
        {
            foreach (var file in inputFiles)
            {
                var reader = new AudioFileReader(file);
                readers.Add(reader);
            }

            // 使用第一个文件的格式创建转换器
            var targetFormat = readers[0].WaveFormat;

            // 逐个读取并写入
            foreach (var reader in readers)
            {
                reader.Position = 0;
                int bytesRead;
                var buffer = new byte[targetFormat.AverageBytesPerSecond / 10]; // 100ms 缓冲区

                while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outputStream.Write(buffer, 0, bytesRead);
                }
            }
        }
        finally
        {
            foreach (var reader in readers)
            {
                reader.Dispose();
            }
            outputStream.Dispose();
        }
    }

    /// <summary>
    /// 计算字符串的稳定哈希值（跨进程、跨平台一致）。
    /// </summary>
    private static int GetStableHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return BitConverter.ToInt32(hashBytes, 0);
    }

    /// <summary>
    /// 获取 TTS 统计信息。
    /// </summary>
    public TtsStats GetStats()
    {
        return new TtsStats
        {
            CharacterVoiceCount = _characterVoiceCache.Count,
            TempDirectory = _tempDir,
            TempFileCount = Directory.Exists(_tempDir) ? Directory.GetFiles(_tempDir).Length : 0
        };
    }

    /// <summary>
    /// 清理临时目录。
    /// </summary>
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // 忽略删除失败
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Cleanup();
    }

    /// <summary>
    /// TTS 统计信息。
    /// </summary>
    public record TtsStats
    {
        /// <summary>
        /// 已分配音色的角色数量。
        /// </summary>
        public int CharacterVoiceCount { get; init; }

        /// <summary>
        /// 临时目录路径。
        /// </summary>
        public required string TempDirectory { get; init; }

        /// <summary>
        /// 临时文件数量。
        /// </summary>
        public int TempFileCount { get; init; }
    }
}
