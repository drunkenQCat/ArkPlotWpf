using ArkPlot.Cli.Pipeline;

namespace ArkPlot.Cli;

public class Program
{
    private static async Task Main(string[] args)
    {
        var tagsJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tags.json");
        if (!File.Exists(tagsJson))
        {
            Console.WriteLine($"❌ 找不到 tags.json，预期路径：{tagsJson}");
            Console.WriteLine("   请确保 ArkPlot.Avalonia/tags.json 已复制到 CLI 输出目录。");
            return;
        }

        try
        {
            var pipeline = new CliPipeline(tagsJson);
            await pipeline.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 发生错误：{ex.Message}");
            Console.WriteLine($"详细：{ex}");
        }
    }
}
