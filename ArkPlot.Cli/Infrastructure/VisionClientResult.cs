namespace ArkPlot.Cli.Infrastructure;

/// <param name="DescribeByUrl">按 URL 获取图片描述的委托。</param>
/// <param name="Disposable">需要随 pipeline 一起释放的客户端资源。</param>
public record VisionClientResult(Func<string, Task<string>> DescribeByUrl, IDisposable Disposable);
