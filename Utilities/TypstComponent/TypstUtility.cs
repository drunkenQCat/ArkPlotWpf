using System.Diagnostics;

public class Program
{
    public static void Main(string chapterName)
    {
        var templatePath = @".\typst-template\template.typ";
        var outputPath = $".\\output\\{chapterName}";
        // 设置命令行程序的名称或路径
        string command = "typst";

        // 设置命令行参数
        string args = $"c -f png --ppi 72 '{templatePath}' \"pic{{n}}.png\"";

        // 创建一个新的进程
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true, // 允许读取输出
            UseShellExecute = false, // 不使用系统外壳启动进程
            CreateNoWindow = true, // 不创建窗口
        };

        using (Process process = Process.Start(startInfo))
        {
            // 读取命令的输出
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit(); // 等待进程结束
        }
    }
}
