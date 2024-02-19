using System.Diagnostics;
using System.IO;

namespace ArkPlotWpf.Utilities.TypstComponents;

// 这个类是用来将 Typst 代码渲染为图片的。
public class TypstRenderer
{
    private readonly string chapterName;

    private TypstRenderer(string name, string code)
    {
        chapterName = name;
        File.WriteAllText(TypPath, code);
    }

    private TypstRenderer(TypstTranslator trans)
    {
        chapterName = trans.ChapterName;
        // 在构造函数中将 typst 代码写入 output 文件夹。
        File.WriteAllText(TypPath, trans.TypCode);
    }

    private string TypPath => $".\\output\\{chapterName}.typ";

    // 这个方法用来渲染 typst 代码为图片。
    public void Render()
    {
        // 设置命令行程序的名称或路径
        var command = "typst";

        // 设置命令行参数
        var args = $"c -f png --ppi 72 '{TypPath}' \"pic{{n}}.png\"";

        // 创建一个新的进程
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true, // 允许读取输出
            UseShellExecute = false, // 不使用系统外壳启动进程
            CreateNoWindow = true // 不创建窗口
        };

        using var process = Process.Start(startInfo);
        // 读取命令的输出
        Debug.Assert(process != null, nameof(process) + " != null");
        var result = process.StandardOutput.ReadToEnd();
        Debug.Print(result);
        process.WaitForExit(); // 等待进程结束
    }
}