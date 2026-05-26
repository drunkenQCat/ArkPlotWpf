using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ArkPlot.Avalonia.Models;

/// <summary>
/// 应用程序统一配置。
/// 读写程序运行目录的 settings.json，不存在时自动生成默认值。
/// 结构可扩展，未来可新增 section。
/// </summary>
public record AppSettings(NovelizerSettings Novelizer)
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 加载配置。如果 settings.json 不存在，自动生成默认配置并保存。
    /// </summary>
    public static AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = CreateDefaults();
            defaults.Save();
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializeOptions);
            return settings ?? CreateDefaults();
        }
        catch
        {
            // 配置文件损坏则重新生成
            var defaults = CreateDefaults();
            defaults.Save();
            return defaults;
        }
    }

    /// <summary>
    /// 保存配置到 settings.json
    /// </summary>
    public void Save()
    {
        var json = JsonSerializer.Serialize(this, SerializeOptions);
        File.WriteAllText(FilePath, json);
    }

    /// <summary>
    /// 获取指定平台的 API Key：settings.json 优先 → 回退环境变量
    /// </summary>
    public string GetApiKey(string providerName)
    {
        // 1. 优先读取 settings.json 中手动配置的值
        if (Novelizer.ApiKeys.TryGetValue(providerName, out var key) && !string.IsNullOrEmpty(key))
            return key;

        // 2. 回退到环境变量
        return providerName switch
        {
            "DeepSeek" => Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "",
            "百炼" => Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? "",
            _ => ""
        };
    }

    private static AppSettings CreateDefaults()
    {
        return new AppSettings(NovelizerSettings.CreateDefaults());
    }
}

/// <summary>
/// 小说化模块配置
/// </summary>
public record NovelizerSettings(
    string SystemPrompt,
    string SelectedProvider,
    string SelectedModel,
    Dictionary<string, string> ApiKeys)
{
    public const string DefaultSystemPrompt = """
        你是一位精通明日方舟世界观的资深小说家。
        请将输入的剧情脚本转化为连贯、流畅的小说叙述。
        文本要求：
        - 保持游戏原文的角色对话核心内容不变
        - 将舞台指示（立绘变化、背景切换、音乐提示、音效等）自然地融入叙事
        - 对话之间补充恰当的衔接描写（动作、心理、环境）
        - 语气符合明日方舟冷峻、克制的文学风格
        - 用第三人称叙述
        - 直接输出小说正文，不要前缀说明、不要后缀总结
        格式要求：
        - 输出时不许带任何# （标题）,无论几级都不需要
        - 对于`音乐`，你可以结合全文，构建出相应的气氛。音乐永远不会出现在剧情里。
        """;

    public static readonly string[] ProviderOptions = ["DeepSeek", "百炼"];
    public static readonly string[] ModelOptions = ["deepseek-v4-pro", "deepseek-v4-flash"];

    public static NovelizerSettings CreateDefaults()
    {
        return new NovelizerSettings(
            SystemPrompt: DefaultSystemPrompt,
            SelectedProvider: "DeepSeek",
            SelectedModel: "deepseek-v4-pro",
            ApiKeys: new Dictionary<string, string>
            {
                ["DeepSeek"] = "",
                ["百炼"] = ""
            }
        );
    }
}
