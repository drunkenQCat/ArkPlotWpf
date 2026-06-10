using System.Text.Json;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities;
using ArkPlot.Core.Utilities.WorkFlow;
using Xunit;

namespace ArkPlot.Novelizer.Tests;

public class PromptEvalBootstrapTests
{
    [Fact]
    public void BaselineBundle_IsLoadable()
    {
        PromptEvalPaths.EnsureDirectories();
        var bundles = PromptBundleLoader.LoadAll();

        Assert.NotEmpty(bundles);
        var baseline = bundles.FirstOrDefault(b => b.Manifest.BundleId == "00_baseline");
        Assert.NotNull(baseline);
        Assert.False(string.IsNullOrWhiteSpace(baseline!.NovelizerSystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(baseline.VisionSystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(baseline.VisionUserBackgroundPrompt));
        Assert.False(string.IsNullOrWhiteSpace(baseline.VisionUserPortraitPrompt));
    }

    [Fact]
    public async Task BootstrapEvaluationSamplesFromDb()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PROMPT_EVAL_BOOTSTRAP"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine("Set PROMPT_EVAL_BOOTSTRAP=1 to export evaluation samples from DB.");
            return;
        }

        PromptEvalPaths.EnsureDirectories();
        Assert.True(File.Exists(PromptEvalPaths.TagsJsonPath), $"tags.json not found: {PromptEvalPaths.TagsJsonPath}");

        DbFactory.ConfigureForTesting($"Data Source={PromptEvalPaths.AvaloniaDbPath}");
        _ = DbFactory.GetClient();
        var sync = new StorySyncService();
        var acts = await EnsureZhCnStoryIndexAsync(sync);
        var manifest = new List<EvalSampleManifest>();

        foreach (var spec in EvalSampleCatalog.DefaultSpecs)
        {
            var act = acts.FirstOrDefault(a => a.ActId == spec.ActId)
                ?? throw new InvalidOperationException($"Act not found in DB: {spec.ActId}");
            var chapters = sync.GetChaptersByActId(act.Id);
            var chapter = chapters
                .OrderBy(c => c.StorySort)
                .ThenBy(c => c.Id)
                .FirstOrDefault()
                ?? throw new InvalidOperationException($"Act has no chapter: {spec.ActId}");

            Assert.Equal(spec.ExpectedStoryCode, chapter.StoryCode);

            var loader = new AkpStoryLoader(act, chapters);
            var chapterTitle = EvalSampleCatalog.BuildLoaderChapterTitle(chapter);
            await loader.GetAllChapters(new[] { chapterTitle });
            await loader.ParseAllDocuments(PromptEvalPaths.TagsJsonPath);

            var plotManager = loader.ContentTable.FirstOrDefault()
                ?? throw new InvalidOperationException($"No content loaded for {spec.ActId} / {chapterTitle}");
            var entries = plotManager.CurrentPlot.TextVariants;
            var exportedMarkdown = AkpProcessor.ExportPlots(loader.ContentTable);
            var sampleMarkdown = "# " + act.Name + "\n\n" + exportedMarkdown;

            var samplePath = EvalSampleCatalog.GetSamplePath(spec, chapter.StoryCode);
            var factsPath = EvalSampleCatalog.GetFactsPath(spec, chapter.StoryCode);
            await File.WriteAllTextAsync(samplePath, sampleMarkdown);
            await File.WriteAllTextAsync(factsPath, BuildFactsPlaceholder(spec, chapter, plotManager.CurrentPlot.Id, entries.Count));

            manifest.Add(new EvalSampleManifest(
                spec.SampleId,
                spec.ActId,
                spec.ActivityName,
                chapter.StoryCode,
                chapter.StoryName,
                chapter.AvgTag,
                plotManager.CurrentPlot.Id,
                Path.GetRelativePath(PromptEvalPaths.TestRoot, samplePath),
                Path.GetRelativePath(PromptEvalPaths.TestRoot, factsPath),
                entries.Count));

            Console.WriteLine($"[EXPORTED] {spec.ActId} -> {Path.GetFileName(samplePath)} ({entries.Count} entries, {sampleMarkdown.Length} chars)");
        }

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(PromptEvalPaths.SampleIndexPath, json);
        Console.WriteLine($"[DONE] Sample index: {PromptEvalPaths.SampleIndexPath}");
    }

    private static string BuildFactsPlaceholder(EvalSampleSpec spec, StoryChapter chapter, long plotId, int entryCount)
    {
        return $"""
sample_id: {spec.SampleId}
act_id: {spec.ActId}
activity_name: {spec.ActivityName}
story_code: {chapter.StoryCode}
story_name: {chapter.StoryName}
avg_tag: {chapter.AvgTag ?? ""}
plot_id: {plotId}
entry_count: {entryCount}

# TODO: 由后续 agent 补全以下字段
required_characters:
  - TODO

required_scenes:
  - TODO

required_events:
  - TODO

forbidden_failures:
  - 混淆角色标签
  - 将多场景压缩为摘要
  - 漏掉核心背景切换
""";
    }

    private static async Task<List<Act>> EnsureZhCnStoryIndexAsync(StorySyncService sync)
    {
        var acts = sync.GetActsFromDb("zh_CN");
        if (acts.Count > 0)
            return acts;

        Console.WriteLine("[BOOTSTRAP] 本地 DB 缺少 zh_CN 活动索引，开始同步 story_review_table.json ...");
        await sync.DownloadAndSaveAsync("zh_CN");
        acts = sync.GetActsFromDb("zh_CN");

        if (acts.Count == 0)
            throw new InvalidOperationException("DB 初始化后仍未获得任何 zh_CN 活动索引。");

        return acts;
    }
}
