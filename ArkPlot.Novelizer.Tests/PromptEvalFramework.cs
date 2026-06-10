using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArkPlot.Novelizer.Tests;

public static partial class PromptEvalPaths
{
    public static string ProjectRoot => FindProjectRoot();
    public static string TestRoot => Path.Combine(ProjectRoot, "ArkPlot.Novelizer.Tests");
    public static string BundlesDir => Path.Combine(TestRoot, "bundles");
    public static string SamplesDir => Path.Combine(TestRoot, "samples");
    public static string FactsDir => Path.Combine(TestRoot, "facts");
    public static string OutputDir => Path.Combine(TestRoot, "output");
    public static string RunsDir => Path.Combine(TestRoot, "runs");
    public static string VisionSnapshotsDir => Path.Combine(TestRoot, "vision_output_snapshots");
    public static string SampleIndexPath => Path.Combine(SamplesDir, "index.json");
    public static string AvaloniaDbPath => Path.Combine(ProjectRoot, "ArkPlot.Avalonia", "bin", "Debug", "net9.0", "arkplot.db");
    public static IReadOnlyList<string> TagsJsonCandidates =>
    [
        Path.Combine(ProjectRoot, "ArkPlotWpf", "tags.json"),
        Path.Combine(ProjectRoot, "ArkPlot.Avalonia", "tags.json"),
        Path.Combine(ProjectRoot, "ArkPlot.Cli", "tags.json")
    ];

    public static string TagsJsonPath =>
        TagsJsonCandidates.FirstOrDefault(File.Exists)
        ?? TagsJsonCandidates[0];

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BundlesDir);
        Directory.CreateDirectory(SamplesDir);
        Directory.CreateDirectory(FactsDir);
        Directory.CreateDirectory(OutputDir);
        Directory.CreateDirectory(RunsDir);
        Directory.CreateDirectory(VisionSnapshotsDir);
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

public sealed record PromptBundleManifest(
    string BundleId,
    string DisplayName,
    string Kind,
    string Summary,
    string NovelizerModelHint,
    string VisionModelHint,
    string ParentBundleId,
    string ChangedDimension);

public sealed record PromptBundle(
    PromptBundleManifest Manifest,
    string NovelizerSystemPrompt,
    string VisionSystemPrompt,
    string VisionUserBackgroundPrompt,
    string VisionUserPortraitPrompt,
    string BundleDir);

public static class PromptBundleLoader
{
    public static PromptBundle Load(string bundleDir)
    {
        var manifestPath = Path.Combine(bundleDir, "bundle.json");
        var manifest = JsonSerializer.Deserialize<PromptBundleManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidOperationException($"Cannot parse bundle manifest: {manifestPath}");

        return new PromptBundle(
            manifest,
            File.ReadAllText(Path.Combine(bundleDir, "novelizer_system.md")),
            File.ReadAllText(Path.Combine(bundleDir, "vision_system.md")),
            File.ReadAllText(Path.Combine(bundleDir, "vision_user_background.md")),
            File.ReadAllText(Path.Combine(bundleDir, "vision_user_portrait.md")),
            bundleDir);
    }

    public static IReadOnlyList<PromptBundle> LoadAll()
    {
        if (!Directory.Exists(PromptEvalPaths.BundlesDir))
            return [];

        return Directory.GetDirectories(PromptEvalPaths.BundlesDir)
            .OrderBy(p => p)
            .Select(Load)
            .ToList();
    }
}

public sealed record EvalSampleSpec(
    string SampleId,
    string ActId,
    string ExpectedStoryCode,
    string ActivityName);

public sealed record EvalSampleManifest(
    string SampleId,
    string ActId,
    string ActivityName,
    string StoryCode,
    string StoryName,
    string? AvgTag,
    long PlotId,
    string SamplePath,
    string FactsPath,
    int CharacterCount);

public static partial class EvalSampleCatalog
{
    public static readonly IReadOnlyList<EvalSampleSpec> DefaultSpecs =
    [
        new("act13side_first_chapter", "act13side", "NL-ST-1", "长夜临光"),
        new("act25side_first_chapter", "act25side", "CW-ST-1", "孤星"),
        new("act33side_first_chapter", "act33side", "BB-ST-1", "巴别塔"),
        new("act49side_first_chapter", "act49side", "TA-ST-1", "辞岁行"),
    ];

    public static string BuildLoaderChapterTitle(ArkPlot.Core.Model.StoryChapter chapter)
    {
        var variation = chapter.StoryId.Contains("variation", StringComparison.OrdinalIgnoreCase)
            ? ExtractVariationRegex().Match(chapter.StoryId).Groups[1].Value
            : "";
        return $"{chapter.StoryCode} {chapter.StoryName} {chapter.AvgTag}{variation}".Trim();
    }

    public static string GetSamplePath(EvalSampleSpec spec, string storyCode) =>
        Path.Combine(PromptEvalPaths.SamplesDir, $"{spec.ActId}_{storyCode}.md");

    public static string GetFactsPath(EvalSampleSpec spec, string storyCode) =>
        Path.Combine(PromptEvalPaths.FactsDir, $"{spec.ActId}_{storyCode}.yml");

    [GeneratedRegex(@"variation(\d+)")]
    private static partial Regex ExtractVariationRegex();
}

