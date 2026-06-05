using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ArkPlot.Tts.Model;
using SqlSugar;

namespace ArkPlot.Tts;

/// <summary>
/// 音色分配管理器。
/// 根据角色名 + 性别确定音色，支持 SQLite 持久化保证跨运行一致。
/// </summary>
public class VoiceManager
{
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly SqlSugarClient? _db;
    private bool _tableReady;

    /// <summary>
    /// 创建 VoiceManager。
    /// </summary>
    /// <param name="db">可选的 SqlSugarClient，提供时启用 DB 持久化。</param>
    public VoiceManager(SqlSugarClient? db = null)
    {
        _db = db;
    }

    /// <summary>
    /// 根据角色名获取音色。
    /// 优先查 DB（如已配置）→ 内存缓存 → 计算分配 → 写入 DB。
    /// </summary>
    public string GetVoiceForCharacter(string characterName, string? gender = null)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return VoicePool.Narrator;

        var cacheKey = gender != null ? $"{characterName}|{gender}" : characterName;

        return _cache.GetOrAdd(cacheKey, _ =>
        {
            // 1. 查 DB
            EnsureTableReady();
            var dbVoice = LoadFromDb(characterName);
            if (!string.IsNullOrEmpty(dbVoice))
                return dbVoice;

            // 2. 计算分配
            var hash = GetStableHash(characterName);
            bool isFemale = !string.IsNullOrWhiteSpace(gender)
                ? gender.Contains("女")
                : hash % 2 == 0;

            var pool = isFemale ? VoicePool.Female : VoicePool.Male;
            var index = Math.Abs(hash) % pool.Length;
            var voice = pool[index];

            // 3. 写入 DB
            SaveToDb(characterName, gender, voice);

            return voice;
        });
    }

    /// <summary>获取旁白专用音色。</summary>
    public string GetNarratorVoice() => VoicePool.Narrator;

    /// <summary>获取性别无法识别时的 fallback 音色。</summary>
    public string GetFallbackVoice() => VoicePool.Male[0];

    /// <summary>
    /// 根据性别获取音色（固定选第一个，稳定可预测）。
    /// </summary>
    public string GetVoiceForGender(string? gender)
    {
        if (string.IsNullOrWhiteSpace(gender))
            return VoicePool.Narrator;

        var pool = gender.Contains("女") ? VoicePool.Female : VoicePool.Male;
        return pool[0];
    }

    /// <summary>已分配音色的角色数量（内存缓存）。</summary>
    public int AssignedCount => _cache.Count;

    /// <summary>
    /// 计算字符串的稳定哈希值（跨进程、跨平台一致，基于 SHA256）。
    /// </summary>
    internal static int GetStableHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToInt32(bytes, 0);
    }

    private void EnsureTableReady()
    {
        if (_db == null || _tableReady) return;
        _db.CodeFirst.SetStringDefaultLength(200).InitTables(typeof(CharacterVoiceMap));
        _tableReady = true;
    }

    private string? LoadFromDb(string characterName)
    {
        if (_db == null) return null;
        return _db.Queryable<CharacterVoiceMap>()
            .Where(m => m.CharacterName == characterName)
            .Select(m => m.Voice)
            .First();
    }

    private void SaveToDb(string characterName, string? gender, string voice)
    {
        if (_db == null) return;
        _db.Insertable(new CharacterVoiceMap
        {
            CharacterName = characterName,
            Gender = gender,
            Voice = voice,
            AssignedAt = DateTime.UtcNow
        }).ExecuteCommand();
    }
}
