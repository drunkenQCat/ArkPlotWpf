using System;
using System.Linq;

namespace ArkPlotWpf.Model;

/// <summary>
/// 表示一个字符串字典的类，继承自 <see cref="Dictionary{TKey, TValue}"/>。
/// </summary>
public class StringDict : OrderedDictionary<string, string>
{
    public event Action? OnChanged;

    /// <summary>
    /// 初始化 <see cref="StringDict"/> 类的新实例。
    /// </summary>
    public StringDict()
    {
    }

    public StringDict(IDictionary<string, string> dictionary) : base(dictionary)
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
    // 重写索引器
    public new string this[string key]
    {
        get => base[key];
        set
        {
            base[key] = value;
            OnChanged?.Invoke();
        }
    }

    // 重写 Add
    public new void Add(string key, string value)
    {
        base.Add(key, value);
        OnChanged?.Invoke();
    }

    // 重写 Remove
    public new bool Remove(string key)
    {
        var result = base.Remove(key);
        if (result)
            OnChanged?.Invoke();
        return result;
    }

    // 其他修改方法也建议重写并触发 OnChanged（比如 Clear、Insert 等）
    public new void Clear()
    {
        base.Clear();
        OnChanged?.Invoke();
    }
}