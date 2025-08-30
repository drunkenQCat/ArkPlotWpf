using System.IO;
using Typst;

namespace ArkPlotWpf.Utilities.TypstComponents;

public class TypstRenderer
{
    private readonly string _chapterName;
    private readonly string _typstCode;

    // 构造函数直接接收 TypstTranslator 对象
    public TypstRenderer(TypstTranslator translator)
    {
        _chapterName = translator.ChapterName;
        _typstCode = translator.TypCode;
    }

    // 这个方法现在返回一个图片字节数组的列表
    // 每一项代表一页渲染出的图片
    public List<byte[]> RenderToPngs()
    {
        // 直接使用 Typst.Net 库
        // 无需创建临时文件，也无需调用外部进程
        using var compiler = new TypstCompiler(_typstCode);
        var (pages, warnings) = compiler.Compile(format: "png", ppi: 72.0f);

        // 可以选择在这里处理警告信息
        foreach (var warning in warnings)
        {
            // 例如：System.Diagnostics.Debug.WriteLine($"Typst warning: {warning.Message}");
        }

        return pages;
    }

    // 提供一个辅助方法来保存渲染结果
    public void SavePngsToDirectory(string outputDirectory)
    {
        var pngs = RenderToPngs();

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        for (int i = 0; i < pngs.Count; i++)
        {
            string outputPath = Path.Combine(outputDirectory, $"pic{i + 1}.png");
            File.WriteAllBytes(outputPath, pngs[i]);
        }
    }
}
