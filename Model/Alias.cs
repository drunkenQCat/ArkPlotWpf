using System.Linq;

namespace ArkPlotWpf.Model;

/// <summary>
/// 表示一个字符串字典的类，继承自 <see cref="Dictionary{TKey, TValue}"/>。
/// </summary>
public class StringDict : Dictionary<string, string>
{
    /// <summary>
    /// 初始化 <see cref="StringDict"/> 类的新实例。
    /// </summary>
    public StringDict()
    {
    }

    private StringDict(Dictionary<string, string> dictionary) : base(dictionary)
    {
    }

    /// <summary>
    /// 从键值对集合创建一个 <see cref="StringDict"/> 实例。
    /// </summary>
    /// <param name="kvpList">键值对集合。</param>
    /// <returns>一个新的 <see cref="StringDict"/> 实例。</returns>
    public static StringDict FromEnumerable(IEnumerable<KeyValuePair<string, string>> kvpList)
    {
        return new StringDict(kvpList.ToDictionary(pair => pair.Key, pair => pair.Value));
    }
}