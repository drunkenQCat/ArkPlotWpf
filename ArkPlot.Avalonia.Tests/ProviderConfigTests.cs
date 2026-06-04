using ArkPlot.Avalonia.Models;
using Xunit;

namespace ArkPlot.Avalonia.Tests;

public class ProviderConfigTests
{
    [Fact]
    public void ProviderConfig_RecordEquality()
    {
        var a = new ProviderConfig("test", "https://api.test.com", "key123", ["model-a", "model-b"]);
        var b = new ProviderConfig("test", "https://api.test.com", "key123", ["model-a", "model-b"]);
        // record 的 string[] 属性使用引用比较，需逐字段验证
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.BaseUrl, b.BaseUrl);
        Assert.Equal(a.ApiKey, b.ApiKey);
        Assert.Equal(a.Models, b.Models); // xunit Assert.Equal 对数组做元素级比较
    }

    [Fact]
    public void ProviderConfig_WithExpression()
    {
        var original = new ProviderConfig("test", "https://api.test.com", "key123", ["model-a"]);
        var modified = original with { Name = "modified", ApiKey = "new-key" };
        Assert.Equal("modified", modified.Name);
        Assert.Equal("new-key", modified.ApiKey);
        Assert.Equal("https://api.test.com", modified.BaseUrl);
        Assert.Equal(["model-a"], modified.Models);
    }
}
