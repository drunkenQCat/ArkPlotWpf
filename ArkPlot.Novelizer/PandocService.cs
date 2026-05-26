using System.Diagnostics;

namespace ArkPlot.Novelizer;

/// <summary>
/// Pandoc 服务：负责检测 pandoc 可用性并生成 epub
/// </summary>
public static class PandocService
{
    /// <summary>
    /// 检测系统是否安装了 pandoc
    /// </summary>
    public static async Task<bool> IsPandocAvailableAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pandoc",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 使用 pandoc 将 Markdown 文件异步转换为 epub 格式
    /// </summary>
    /// <param name="mdFilePath">Markdown 文件路径</param>
    /// <param name="title">epub 标题</param>
    /// <returns>生成的 epub 文件路径，失败或 pandoc 不可用时返回 null</returns>
    public static async Task<string?> GenerateEpubAsync(string mdFilePath, string title)
    {
        if (!await IsPandocAvailableAsync())
            return null;

        var epubPath = Path.ChangeExtension(mdFilePath, ".epub");

        try
        {
            var arguments = $"\"{mdFilePath}\" --toc --shift-heading-level-by=-1 --toc-depth=2 --metadata title=\"{title}\" -o \"{epubPath}\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pandoc",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(epubPath))
            {
                return epubPath;
            }

            var stderr = await process.StandardError.ReadToEndAsync();
            Debug.WriteLine($"pandoc 生成 epub 失败: {stderr}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"pandoc 生成 epub 异常: {ex.Message}");
            return null;
        }
    }
}
