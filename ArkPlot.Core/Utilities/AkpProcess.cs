using System.IO;
using System.Linq;
using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.TagProcessingComponents;
using ArkPlot.Core.Utilities.WorkFlow;
using Markdig;

namespace ArkPlot.Core.Utilities;

public abstract class AkpProcessor
{
    /// <summary>
    /// ��һ����鵼��Ϊ Markdown �ı���
    /// </summary>
    /// <param name="plotList">Ҫ�����ľ����б���</param>
    /// <returns>��ʾ������ Markdown ���ݵ��ַ�����</returns>
    public static string ExportPlots(List<PlotManager> plotList)
    {
        var md = new StringBuilder();
        foreach (var chapter in plotList)
        {
            var textList = chapter.CurrentPlot.TextVariants;
            var reconstructor = new MdReconstructor(textList);
            md.Append($"## {chapter.CurrentPlot.Title}\r\n\r\n");
            reconstructor.AppendResultToBuilder(md);
        }

        return md.ToString();
    }

    /// <summary>
    /// ��ָ���� Plot ������ Markdown �ļ�����ʽд�뵽ָ����·����
    /// </summary>
    /// <param name="path">Ҫд�� Markdown �ļ���·����</param>
    /// <param name="markdown">Ҫд��Ϊ Markdown �� Plot ����</param>
    public static void WriteMd(string path, Plot markdown)
    {
        var mdOutPath = path + "\\" + markdown.Title + ".md";
        File.WriteAllText(mdOutPath, markdown.Content.ToString());
    }

    /// <summary>
    /// �� Plot ����� HTML ����д���ļ���
    /// </summary>
    /// <param name="path">���� HTML �ļ���·����</param>
    /// <param name="markdown">���� markdown ���ݵ� Plot ����</param>
    public static void WriteHtml(string path, Plot markdown)
    {
        var htmlPath = path + "\\" + markdown.Title + ".html";
        var htmlContent = GetHtmlContent(markdown);
        var result = FormatHtmlBody(htmlContent, markdown.Title);
        File.WriteAllText(htmlPath, result);
    }

    /// <summary>
    /// �� html �ı��е������滻Ϊ������Ե�ַ��
    /// </summary>
    /// <param name="path">html �ļ�·����</param>
    /// <param name="markdown">Ҫת��ΪHTML��Plot����</param>
    public static void WriteHtmlWithLocalRes(string path, Plot markdown)
    {
        var htmlPath = path + "\\" + markdown.Title + ".html";
        var htmlContent = GetHtmlContent(markdown);
        var htmlWithLocalRes = htmlContent.Replace("https://", "");
        var result = FormatHtmlBody(htmlWithLocalRes, markdown.Title);
        File.WriteAllText(htmlPath, result);
    }

    /// <summary>
    /// �� Plot ���������ת��Ϊʹ�� Markdown �﷨�� HTML��
    /// </summary>
    /// <param name="markdown">���� Markdown ���ݵ� Plot ����</param>
    /// <returns>Markdown ���ݵ� HTML ��ʾ��</returns>
    private static string GetHtmlContent(Plot markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // Add most of all advanced extensions
            .Build();
        return Markdown.ToHtml(markdown.Content.ToString(), pipeline);
    }

    /// <summary>
    /// ��ʽ��HTML���ģ����ӱ�Ҫ��HTML��ǩ��ͷ����β����
    /// </summary>
    /// <param name="body">Ҫ��ʽ�����������ݡ�</param>
    /// <param name="title">HTML�ĵ��ı��⡣</param>
    /// <returns>��ʽ�����HTML���ݡ�</returns>
    private static string FormatHtmlBody(string body, string title)
    {
        body = $"<body>{body}</body>";
        title = $"<title>{title}</title>";
        // ��ȡͷ����β��
        var head = File.ReadAllText("assets/head.html");
        head = $"<head>{head}{title}</head>";
        var html = $"<html>{head}{body}</html>";
        html = "<!doctype html>" + html;
        var tail = File.ReadAllText("assets/tail.html");
        html += tail;
        return html;
    }

    public static void WriteTyp(string outputPath, AkpStoryLoader contentLoader)
    {
        List<PlotManager> plotList = contentLoader.ContentTable;
        var typFolder = outputPath;
        // ��ģ�帴�ƹ���
        string templateFolder = Path.Join(Directory.GetCurrentDirectory(), "typst-template");
        CopyDirectory(templateFolder, typFolder);

        int fileIndex = 1;
        foreach (var plot in plotList)
        {
            var result = "#import \"./template.typ\": arknights_sim, arknights_sim_2p\n";
            var content = string.Join("\n", plot.CurrentPlot.TextVariants.Select(x => x.TypText).ToList());
            result += content;
            var currentTyp = Path.Join(typFolder, $"{fileIndex}_{plot.CurrentPlot.Title}.typ");
            File.WriteAllText(currentTyp, result);
            fileIndex++;
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        // Ensure the source directory exists
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        // Create all directories in the destination
        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(sourceDir, destinationDir));
        }

        // Copy all files to the destination
        foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            string destFile = file.Replace(sourceDir, destinationDir);
            File.Copy(file, destFile, true);
        }
    }
}
