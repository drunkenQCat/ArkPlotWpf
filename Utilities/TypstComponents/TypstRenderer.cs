using System.Diagnostics;
using System.IO;

namespace ArkPlotWpf.Utilities.TypstComponents;
// 这个类是用来将 Typst 代码渲染为图片的。
public class TypstRenderer
{
    readonly string chapterName;
    private string typPath => $".\\output\\{chapterName}.typ";

    TypstRenderer(string name, string code)
    {
        chapterName = name;
        File.WriteAllText(typPath, code);
    }
    
    TypstRenderer(TypstTranslator trans)
    {
        chapterName = trans.ChapterName;
        // 在构造函数中将 typst 代码写入 output 文件夹。
        File.WriteAllText(typPath, trans.TypCode);
    }

    // 这个方法用来渲染 typst 代码为图片。
    public void Render()
    {
        // 设置命令行程序的名称或路径
        string command = "typst";

        // 设置命令行参数
        string args = $"c -f png --ppi 72 '{typPath}' \"pic{{n}}.png\"";

        // 创建一个新的进程
        var startInfo = new ProcessStartInfo()
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true, // 允许读取输出
            UseShellExecute = false, // 不使用系统外壳启动进程
            CreateNoWindow = true, // 不创建窗口
        };

        using Process? process = Process.Start(startInfo);
        // 读取命令的输出
        Debug.Assert(process != null, nameof(process) + " != null");
        string result = process.StandardOutput.ReadToEnd();
        Debug.Print(result);
        process.WaitForExit(); // 等待进程结束
    }
}