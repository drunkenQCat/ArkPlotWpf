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
public record AppSettings(NovelizerSettings Novelizer, VisionSettings? Vision = null)
{
    private static readonly string FilePath = Path.Combine(
        AppContext.BaseDirectory,
        "settings.json"
    );

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
            _ => "",
        };
    }

    private static AppSettings CreateDefaults()
    {
        return new AppSettings(NovelizerSettings.CreateDefaults(), VisionSettings.CreateDefaults());
    }
}

/// <summary>
/// 小说化模块配置
/// </summary>
public record NovelizerSettings(
    string SystemPrompt,
    string SelectedProvider,
    string SelectedModel,
    Dictionary<string, string> ApiKeys
)
{
    public const string DefaultSystemPrompt = """
## 明日方舟剧情小说化转换协议

### 一、 叙事视角与文体调性

* **视角规范**：严格采用**第三人称有限视角**。叙述焦点应锚定于当前场境的核心角色，通过其感官、逻辑与职责边界推进叙事。
* **文体特征**：承袭《孤星》、《巴别塔》、《乌萨斯的孩子们》等剧作的冷峻、克制与思辨感。剔除网络文学的夸张修辞、影视解说的全知旁白，以及短视频式的断句节奏。
* **组织结构**：采用传统严肃文学的长段落结构。一个合格的叙事单元须将**环境异动、角色动作、观察结果、心理推演**有机融合，严禁无故出现连续的单句成段或刻意制造悬念的破折号断句。

### 二、 视听语言的叙事转化

* **场景（背景图）**：严禁概括性描述（如“满目疮痍”、“气氛死寂”）。必须将场景拆解为具象的叙事客体——例如通过“管线渗出的冷却液腐蚀了地板”、“终端屏幕跳动的异常波形”来交待环境的破败与危机。
* **动态（立绘/演出）**：禁止机械性复述角色的神态变化（如“眉头紧锁”、“露出微笑”）。角色外貌除首秀或服务于核心剧情外不予赘述。将立绘的细微调整转化为角色内在的思维波动或肢体战术动作。
* **声音（音乐/音效）**：严禁出现任何提及音乐或音效的字眼。
* *BGM变化* $\rightarrow$ 转化为叙事张力的松紧、对话密度的调整或环境压迫感的骤增。
* *效能音（爆破、警报、开门）* $\rightarrow$ 直接转化为物理层面的环境剧变或不可逆的事实发生。



### 三、 角色声音与行为逻辑

* **身份锚定**：角色台词与行动必须符合其泰拉世界的职业背景与社会阶层。
* *指挥官（如博士、凯尔希）*：全局审视，剥离主观情绪，计算战损与概率，聚焦于决策推进。
* *技术/医疗人员*：定量分析，关注生理体征数据、源石病扩散速率及设备参数。
* *一线干员*：战术占位、危险感知、路径规划，台词去长期化、指令化。


* **去模版化**：严禁滥用“顿了顿”、“轻轻叹气”、“抬起头看向对方”等无意义的通用机械动作。每一次交互必须带有明确的战术意图或心理动机。

### 四、 严重警告事项
* 不要忽略">"开头的文字，那些是需要保留的原文。没有这些文字将会使剧情残缺！

---

**【即刻执行】** 请提供你需要改写的《明日方舟》AVG剧情脚本。我将直接输出符合上述标准的小说正文，不包含任何前言、后记或解释性说明。

""";

    public static readonly string[] ProviderOptions = ["DeepSeek", "百炼"];
    public static readonly string[] ModelOptions = ["deepseek-v4-pro", "deepseek-v4-flash"];

    public static NovelizerSettings CreateDefaults()
    {
        return new NovelizerSettings(
            SystemPrompt: DefaultSystemPrompt,
            SelectedProvider: "DeepSeek",
            SelectedModel: "deepseek-v4-pro",
            ApiKeys: new Dictionary<string, string> { ["DeepSeek"] = "", ["百炼"] = "" }
        );
    }
}

/// <summary>
/// 图片描述（Vision）模块配置
/// </summary>
public record VisionSettings(bool IsPicDescEnabled)
{
    public static VisionSettings CreateDefaults()
    {
        return new VisionSettings(IsPicDescEnabled: false);
    }
}
