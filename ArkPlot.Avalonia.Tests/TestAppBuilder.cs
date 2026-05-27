using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(ArkPlot.Avalonia.Tests.TestAppBuilder))]

namespace ArkPlot.Avalonia.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>
/// 轻量测试用 App，不加载任何主题以避免字体问题。
/// </summary>
public class HeadlessTestApp : Application
{
    public override void Initialize()
    {
        DataTemplates.Add(new ViewLocator());
    }
}
