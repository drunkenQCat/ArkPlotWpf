using System.Text.Json;

namespace ArkPlot.Tts;

/// <summary>
/// 从 JSON 配置文件加载角色性别覆盖。
/// 用于解决 PicDescription 性别推断不可靠的问题。
///
/// JSON 格式：
/// {
///   "角色名": { "gender": "女" },
///   "另一角色": { "gender": "男" }
/// }
/// </summary>
public class GenderOverrideProvider
{
    private readonly Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 创建空的覆盖配置（无任何覆盖）。
    /// </summary>
    public GenderOverrideProvider() { }

    /// <summary>
    /// 从 JSON 文件加载覆盖配置。文件不存在时静默忽略。
    /// </summary>
    /// <param name="jsonPath">character_overrides.json 的路径。</param>
    public GenderOverrideProvider(string jsonPath)
    {
        if (!string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath))
        {
            LoadFromFile(jsonPath);
        }
    }

    /// <summary>
    /// 从内存字典创建（测试用）。
    /// </summary>
    internal GenderOverrideProvider(Dictionary<string, string> overrides)
    {
        foreach (var kvp in overrides)
            _overrides[kvp.Key] = kvp.Value;
    }

    /// <summary>
    /// 获取角色的性别覆盖值。
    /// </summary>
    /// <param name="characterName">角色名。</param>
    /// <returns>"男"或"女"，无覆盖时返回 null。</returns>
    public string? GetOverride(string? characterName)
    {
        if (string.IsNullOrEmpty(characterName))
            return null;

        return _overrides.GetValueOrDefault(characterName);
    }

    /// <summary>已加载的覆盖条目数量。</summary>
    public int Count => _overrides.Count;

    private void LoadFromFile(string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var entries = JsonSerializer.Deserialize<Dictionary<string, GenderEntry>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (entries == null) return;

            foreach (var kvp in entries)
            {
                if (!string.IsNullOrEmpty(kvp.Value.Gender))
                    _overrides[kvp.Key] = kvp.Value.Gender;
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️ character_overrides.json 解析失败: {ex.Message}");
        }
    }

    private class GenderEntry
    {
        public string? Gender { get; set; }
    }
}
