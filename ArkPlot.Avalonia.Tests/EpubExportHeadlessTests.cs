using ArkPlot.Avalonia.ViewModels;
using ArkPlot.Novelizer;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// EPUB 导出 + IsInitialized 生命周期 headless 测试。
/// 绕过所有生成步骤（下载、解析、导出），直接验证 EPUB 输出和 UI 状态恢复。
///
/// Bug 描述：EPUB 导出后 IsInitialized 卡在 false，导致按钮大片灰掉不能点。
/// 怀疑根因：LoadMd finally 块使用 Dispatcher.UIThread.Post 异步恢复 IsInitialized，
/// 在某些场景下 Post 的 action 未被及时处理。
/// </summary>
public class EpubExportHeadlessTests : IDisposable
{
    private readonly string _tempDir;

    public EpubExportHeadlessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ArkPlot_EpubTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static async Task<bool> HasPandoc() => await PandocService.IsPandocAvailableAsync();

    // ──────────────────────────────────────────────
    //  1. 直接 EPUB 输出（绕过所有生成步骤）
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PandocService_DirectEpubOutput_GeneratesValidFile()
    {
        if (!await HasPandoc()) return; // pandoc 未安装，跳过

        var mdPath = Path.Combine(_tempDir, "孤星_novel_test.md");
        await File.WriteAllTextAsync(mdPath, """
            # 孤星

            ## 第一章

            这是测试内容。

            > 博士，你好。

            ## 第二章

            更多内容在这里。
            """);

        var epubPath = await PandocService.GenerateEpubAsync(mdPath, "孤星 测试");

        Assert.NotNull(epubPath);
        Assert.True(File.Exists(epubPath), $"EPUB 文件未生成: {epubPath}");
        Assert.EndsWith(".epub", epubPath);
        Assert.True(new FileInfo(epubPath).Length > 0, "EPUB 文件大小为 0");
    }

    [Fact]
    public async Task PandocService_MultipleNovelMd_GeneratesAllEpubs()
    {
        if (!await HasPandoc()) return; // pandoc 未安装，跳过

        var titles = new[] { "孤星_novel_ch1", "孤星_novel_ch2", "孤星_novel_ch3" };
        foreach (var title in titles)
        {
            var mdPath = Path.Combine(_tempDir, $"{title}.md");
            await File.WriteAllTextAsync(mdPath, $"# {title}\n\n测试内容 for {title}");
        }

        // 模拟 NovelizerPipeline.GenerateEpubsForNovelsAsync 的逻辑
        var novelMdFiles = Directory.GetFiles(_tempDir, "*_novel_*.md");
        Assert.Equal(3, novelMdFiles.Length);

        foreach (var mdPath in novelMdFiles)
        {
            var title = Path.GetFileNameWithoutExtension(mdPath);
            await PandocService.GenerateEpubAsync(mdPath, title);
        }

        var epubFiles = Directory.GetFiles(_tempDir, "*.epub");
        Assert.Equal(3, epubFiles.Length);
        Assert.All(epubFiles, f => Assert.True(new FileInfo(f).Length > 0));
    }

    // ──────────────────────────────────────────────
    //  2. IsInitialized 生命周期（核心 bug 诊断）
    //     使用 [AvaloniaFact] 确保 Dispatcher 已初始化
    // ──────────────────────────────────────────────

    [Fact]
    public void IsInitialized_DefaultValue_IsFalse()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.IsInitialized);
    }

    /// <summary>
    /// 精确复现 LoadMd 的 finally 块行为：
    /// PrepareLoading() 设 false → pipeline 完成 → Dispatcher.UIThread.Post(() => IsInitialized = true)
    ///
    /// 如果此测试失败，说明 headless Dispatcher 不会自动处理 Post 的 action，
    /// 这就是生产环境中按钮一直灰掉的根因。
    /// </summary>
    [AvaloniaFact]
    public void IsInitialized_DispatcherPost_RecoversInHeadless()
    {
        var vm = new MainWindowViewModel();

        // ── PrepareLoading() ──
        vm.IsInitialized = false;
        Assert.False(vm.IsInitialized);

        // ── LoadMd finally 块的精确复现 ──
        Dispatcher.UIThread.Post(() => vm.IsInitialized = true);

        // Post 是异步投递，需要强制 Dispatcher 处理队列
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsInitialized,
            "Dispatcher.UIThread.Post + RunJobs 未能恢复 IsInitialized — " +
            "这就是 EPUB 导出后按钮一直灰掉的根因！");
    }

    /// <summary>
    /// 验证 Post + RunJobs 只触发一次 PropertyChanged
    /// </summary>
    [AvaloniaFact]
    public void IsInitialized_PostThenRunJobs_OnlyTriggersOnce()
    {
        var vm = new MainWindowViewModel();
        vm.IsInitialized = false;

        int triggerCount = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsInitialized) && vm.IsInitialized)
                triggerCount++;
        };

        Dispatcher.UIThread.Post(() => vm.IsInitialized = true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsInitialized);
        Assert.Equal(1, triggerCount);
    }

    // ──────────────────────────────────────────────
    //  3. 异常路径（pipeline 失败时 finally 是否仍能恢复）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 模拟 LoadMd 中 pipeline 抛异常的场景。
    /// 外层 catch 捕获异常，内层 finally 的 Post 仍能恢复 IsInitialized。
    /// </summary>
    [AvaloniaFact]
    public void IsInitialized_ExceptionCaught_FinallyRecovers()
    {
        var vm = new MainWindowViewModel();
        Exception? caught = null;

        try
        {
            try
            {
                vm.IsInitialized = false;
                throw new InvalidOperationException("模拟 ExportDocuments 异常");
            }
            finally
            {
                // 精确复现 LoadMd finally 块
                Dispatcher.UIThread.Post(() => vm.IsInitialized = true);
            }
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        // 强制 Dispatcher 处理队列
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(caught);
        Assert.Equal("模拟 ExportDocuments 异常", caught.Message);
        Assert.True(vm.IsInitialized,
            "异常路径下 IsInitialized 未恢复 — finally 块存在但 Post 未执行");
    }

    // ──────────────────────────────────────────────
    //  4. 完整 LoadMd 生命周期模拟（含 async pipeline + CompleteLoading）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 最接近生产场景的测试：模拟 LoadMd 的完整 async 流程，
    /// 包含多个 await 步骤和 CompleteLoading（用 RunJobs 模拟 MessageBox.ShowAsync）
    /// </summary>
    [AvaloniaFact]
    public async Task IsInitialized_FullLoadMdSimulation_RecoversAfterCompleteLoading()
    {
        var vm = new MainWindowViewModel();
        vm.IsInitialized = true; // 初始状态：已加载完毕

        try
        {
            // ── PrepareLoading() ──
            vm.IsInitialized = false;
            Assert.False(vm.IsInitialized);

            // ── GetAllChapters (模拟 async) ──
            await Task.Delay(20);

            // ── PreloadResources (模拟 async) ──
            await Task.Delay(20);

            // ── StartParseDocuments (模拟 async) ──
            await Task.Delay(20);

            // ── ExportDocuments (模拟 async) ──
            await Task.Delay(20);

            // ── RunNovelizerIfEnabled → BatchProcessAsync → GenerateEpubsForNovelsAsync ──
            if (await PandocService.IsPandocAvailableAsync())
            {
                var mdPath = Path.Combine(_tempDir, "孤星_novel_sim.md");
                await File.WriteAllTextAsync(mdPath, "# 孤星\n\n模拟小说内容");
                await PandocService.GenerateEpubAsync(mdPath, "孤星");
            }

            // ── CompleteLoading (模拟 MessageBox.ShowAsync) ──
            // 生产环境中这是一个 await messageBox.ShowAsync()
            // 在 headless 中直接跳过（模拟用户点击 OK 后返回）
        }
        finally
        {
            // 精确复现 LoadMd finally 块
            Dispatcher.UIThread.Post(() => vm.IsInitialized = true);
        }

        // 强制 Dispatcher 处理队列
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsInitialized,
            "完整 LoadMd 模拟后 IsInitialized 未恢复 — " +
            "Post 在 CompleteLoading 之后未被处理！");
    }

    // ──────────────────────────────────────────────
    //  5. 对比测试：不同 Dispatcher 方法的可靠性
    // ──────────────────────────────────────────────

    /// <summary>
    /// 对比 Post+RunJobs vs Post+InvokeAsync vs InvokeAsync vs 直接赋值 的可靠性。
    /// 如果 Post 不可靠但其他方式可靠，应将 LoadMd finally 块改为可靠方式。
    /// </summary>
    [AvaloniaFact]
    public async Task DispatcherMethods_Comparison_Reliability()
    {
        // ── 方案 A：当前代码 — Dispatcher.UIThread.Post + RunJobs ──
        var vmA = new MainWindowViewModel();
        vmA.IsInitialized = false;
        Dispatcher.UIThread.Post(() => vmA.IsInitialized = true);
        Dispatcher.UIThread.RunJobs();
        var postWithRunJobs = vmA.IsInitialized;

        // ── 方案 B：Post + InvokeAsync 等待 ──
        var vmB = new MainWindowViewModel();
        vmB.IsInitialized = false;
        Dispatcher.UIThread.Post(() => vmB.IsInitialized = true);
        await Dispatcher.UIThread.InvokeAsync(() => { });
        var postWithInvoke = vmB.IsInitialized;

        // ── 方案 C：改用 InvokeAsync ──
        var vmC = new MainWindowViewModel();
        vmC.IsInitialized = false;
        await Dispatcher.UIThread.InvokeAsync(() => vmC.IsInitialized = true);
        var invokeResult = vmC.IsInitialized;

        // ── 方案 D：直接赋值（如果在 UI 线程）──
        var vmD = new MainWindowViewModel();
        vmD.IsInitialized = false;
        vmD.IsInitialized = true;
        var directResult = vmD.IsInitialized;

        // 记录各方案结果
        Console.WriteLine($"Post + RunJobs:    IsInitialized = {postWithRunJobs}");
        Console.WriteLine($"Post + InvokeAsync: IsInitialized = {postWithInvoke}");
        Console.WriteLine($"InvokeAsync:        IsInitialized = {invokeResult}");
        Console.WriteLine($"Direct:             IsInitialized = {directResult}");

        // 直接赋值和 InvokeAsync 应该都可靠
        Assert.True(directResult, "直接赋值应始终可靠");
        Assert.True(invokeResult, "InvokeAsync 应可靠");
        Assert.True(postWithRunJobs, "Post + RunJobs 应可靠");
        Assert.True(postWithInvoke, "Post + InvokeAsync 应可靠");
    }
}
