using Newtonsoft.Json.Linq;

namespace ArkPlot.Core.Model;

/// <summary>
/// 明日方舟每一次活动相关的信息�?
/// </summary>
public class ActInfo
{
    /// <summary>
    /// 活动的语言�?
    /// </summary>
    public readonly string Lang;

    /// <summary>
    /// 活动的名称。会依照所选语言而变化�?
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// 活动的下拉菜单选项。同时也是这次活动包含的所有章节�?
    /// </summary>
    public readonly JToken Tokens;

    /// <summary>
    /// 活动的类型。有活动、故事集、主�?个类别�?
    /// </summary>
    public string ActType;

    /// <summary>
    /// 初始化一�?ActInfo 类的新实例�?
    /// </summary>
    /// <param name="lang">活动的语言�?/param>
    /// <param name="actType">活动的类型。有活动、故事集、主�?个类别�?/param>
    /// <param name="name">活动的名称。应当与活动的语言相对应�?/param>
    /// <param name="tokens">活动的下拉菜单选项。同时也是这次活动包含的所有章节�?/param>
    public ActInfo(string lang, string actType, string name, JToken tokens)
    {
        Lang = lang;
        ActType = actType;
        Name = name;
        Tokens = tokens;
    }
}
