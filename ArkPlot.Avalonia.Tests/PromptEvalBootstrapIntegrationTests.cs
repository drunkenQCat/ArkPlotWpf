using ArkPlot.Avalonia.Services;
using Xunit;

namespace ArkPlot.Avalonia.Tests;

public class PromptEvalBootstrapIntegrationTests
{
    [Fact(Timeout = 3_600_000)]
    public async Task Bootstrap_CurrentPrompts_FromAvaloniaFlow()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PROMPT_EVAL_BOOTSTRAP"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine("Set PROMPT_EVAL_BOOTSTRAP=1 to run prompt-eval bootstrap.");
            return;
        }

        var root = FindProjectRoot();
        var avaloniaOutput = Path.Combine(root, "ArkPlot.Avalonia", "bin", "Debug", "net9.0");
        var outputRoot = Path.Combine(root, "ArkPlot.Novelizer.Tests", "bootstrap", "current_prompts");

        var runner = new PromptEvalBootstrapper();
        var result = await runner.RunAsync(new PromptEvalBootstrapOptions
        {
            SourceDbPath = Path.Combine(avaloniaOutput, "arkplot.db"),
            SettingsPath = Path.Combine(avaloniaOutput, "settings.json"),
            TagsJsonPath = Path.Combine(root, "ArkPlotWpf", "tags.json"),
            OutputRoot = outputRoot,
            ResetOutputRoot = true,
            ResetWorkingDb = true,
            EnablePicDesc = true,
            EnableNovelizer = true,
            RefreshPicDesc = true,
            ActIds = ["act13side", "act25side", "act33side", "act49side"],
            ChapterCount = 1
        });

        Assert.Equal(4, result.Acts.Count);
        Assert.All(result.Acts, act =>
        {
            Assert.True(File.Exists(act.MarkdownPath), $"Markdown not found: {act.MarkdownPath}");
            Assert.True(File.Exists(act.PicDescSnapshotPath), $"PicDesc snapshot not found: {act.PicDescSnapshotPath}");
            Assert.True(File.Exists(act.MetadataPath), $"Metadata not found: {act.MetadataPath}");
            Assert.NotEmpty(act.NovelPaths);
            Assert.All(act.NovelPaths, path => Assert.True(File.Exists(path), $"Novel not found: {path}"));
        });
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "ArkPlot.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Cannot find ArkPlot.sln");
    }
}
