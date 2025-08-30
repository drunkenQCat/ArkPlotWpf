using System.Diagnostics;
using System.IO;

namespace ArkPlot.Core.Utilities.TypstComponents;

// 这个类是用来�?Typst 代码渲染为图片的�?
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
        // 在构造函数中�?typst 代码写入 output 文件夹�?
        File.WriteAllText(TypPath, trans.TypCode);
    }

    // TODO:根据当前输出文件结构，修改渲染图片路�?
    private string TypPath => $".\\output\\{chapterName}.typ";
    public string GetPngByIndex(int index) => $".\\output\\{chapterName}_Seq\\" + $"pic{index}.typ";

    // 这个方法用来渲染 typst 代码为图片�?
    private void ExportPngSequence()
    {
        string ExportPngPath(string chapter) => $".\\output\\{chapter}_Seq\\" + "pic{n}.typ";

        string ExportCommand() => "typst c -f png --ppi 72 "
                                             + $"'{TypPath}' "
                                             + $"'{ExportPngPath(chapterName)}'";
        using var process = new Process();
        ProcessStartInfo startInfo = new()
        {
            FileName = @"powershell.exe",
            Arguments = ExportCommand()
        };
        Process.Start(startInfo);
        process.WaitForExit();
    }
    public void Render()
    {
        // 设置命令行程序的名称或路�?
        var command = "typst";

        // 设置命令行参�?
        var args = $"c -f png --ppi 72 '{TypPath}' \"pic{{n}}.png\"";

        // 创建一个新的进�?
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true, // 允许读取输出
            UseShellExecute = false, // 不使用系统外壳启动进�?
            CreateNoWindow = true // 不创建窗�?
        };

        using var process = Process.Start(startInfo);
        // 读取命令的输�?
        Debug.Assert(process != null, nameof(process) + " != null");
        var result = process.StandardOutput.ReadToEnd();
        Debug.Print(result);
        process.WaitForExit(); // 等待进程结束
    }
}
