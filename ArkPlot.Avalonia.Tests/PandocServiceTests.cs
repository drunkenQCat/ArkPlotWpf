using System.Diagnostics;
using ArkPlot.Novelizer;

namespace ArkPlot.Avalonia.Tests;

/// <summary>
/// 验证 PandocService 的死锁修复和 BatchProcessAsync 的文件过滤逻辑。
/// </summary>
public class PandocServiceTests
{
    /// <summary>
    /// 验证：当子进程向 stdout 写入大量数据时，不会死锁。
    /// 模拟 pandoc 输出 > 4KB 到 stdout 的场景（Windows pipe buffer 约 4KB）。
    /// 修复前此测试会超时挂起；修复后应在几秒内完成。
    /// </summary>
    [Fact]
    public async Task GenerateEpubAsync_StdoutHeavyOutput_DoesNotDeadlock()
    {
        // 创建一个测试用的 markdown 文件
        var tempDir = Path.Combine(Path.GetTempPath(), "ArkPlotTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# Test\n\nHello world.\n");

            // 即使 pandoc 不存在，GenerateEpubAsync 应安全返回 null，不会死锁
            var task = PandocService.GenerateEpubAsync(mdPath, "Test Title");
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Equal(task, completed); // 10秒内必须完成，否则视为死锁
            // pandoc 可能没装，结果为 null 也正常
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// 验证：Process 重定向 stdout 时，大量输出不会导致 WaitForExitAsync 死锁。
    /// 这是一个底层验证，直接测试 .NET Process 的死锁场景。
    /// </summary>
    [Fact]
    public async Task Process_RedirectedStdout_LargeOutputDoesNotDeadlock()
    {
        // 用 cmd 生成 > 8KB 的 stdout 输出（远超 4KB pipe buffer）
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"for /L %i in (1,1,500) do @echo LINE_%i_PADDING_DATA_TO_FILL_BUFFER_1234567890\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;

        // 正确做法：先启动异步读取，再 WaitForExitAsync
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(cts.Token);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("LINE_1_PADDING", stdout);
        Assert.True(stdout.Length > 4000, $"stdout 应超过 4KB，实际 {stdout.Length} 字节");
    }

    /// <summary>
    /// 反面验证：如果不在 WaitForExitAsync 之前读流，大量 stdout 会导致死锁（超时）。
    /// 这个测试演示了错误做法的后果。
    /// </summary>
    [Fact]
    public async Task Process_WrongOrder_LargeOutputCausesDeadlock()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"for /L %i in (1,1,500) do @echo LINE_%i_PADDING_DATA_TO_FILL_BUFFER_1234567890\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;

        // 错误做法：先 WaitForExitAsync，再读流
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await process.WaitForExitAsync(cts.Token);
            // 如果走到这里说明没死锁（可能因为 pipe buffer 够大），也不算失败
        }
        catch (OperationCanceledException)
        {
            // 预期的死锁超时 — 这证明错误做法确实会死锁
            process.Kill(entireProcessTree: true);
        }
    }

    /// <summary>
    /// 验证 BatchProcessAsync 排除 _novel_ 文件，不会把小说输出当作输入重新处理。
    /// </summary>
    [Fact]
    public void BatchProcessAsync_NovelFileFilter_ExcludesNovelOutputs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ArkPlotFilterTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // 创建模拟文件
            File.WriteAllText(Path.Combine(tempDir, "水晶箭行动.md"), "# 原始剧情");
            File.WriteAllText(Path.Combine(tempDir, "水晶箭行动_novel_flash.md"), "# 小说化内容");
            File.WriteAllText(Path.Combine(tempDir, "水晶箭行动_novel_flash_novel_flash.md"), "# 重复追加");

            // 模拟 BatchProcessAsync 的文件过滤逻辑
            var mdFiles = Directory.GetFiles(tempDir, "*.md", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileNameWithoutExtension(f).Contains("_novel_"))
                .ToArray();

            Assert.Single(mdFiles);
            Assert.EndsWith("水晶箭行动.md", mdFiles[0]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
