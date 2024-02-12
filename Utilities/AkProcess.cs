using ArkPlotWpf.Model;
using Markdig;
using System.IO;

namespace ArkPlotWpf.Utilities;

internal abstract class AkProcessor
{
    public static string ExportPlots(List<Plot> plotList, string jsonPath)
    {
        var md = new StringBuilder();
        var parser = new AkParser(jsonPath);
        foreach (var chapter in plotList)
        {
            md.AppendLine($"## {chapter.Title}");
            parser.ConvertToMarkdown(chapter.Content);
            md.Append(chapter.Content);
        }
        return md.ToString();
    }

    public static void WriteMd(string path, Plot markdown)
    {
        var mdOutPath = path + "\\" + markdown.Title + ".md";
        File.WriteAllText(mdOutPath, markdown.Content.ToString());
    }

    public static void WriteHtml(string path, Plot markdown)
    {
        var htmlPath = path + "\\" + markdown.Title + ".html";
        var pipeline = new MarkdownPipelineBuilder()
                          .UseAdvancedExtensions()
                          .Build();
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // Add most of all advanced extensions
            .Build();
        var htmlBody = Markdown.ToHtml(markdown.Content.ToString(), pipeline);
        string htmlContent = FormatHtmlBody(htmlBody, markdown.Title);
        File.WriteAllText(htmlPath, htmlContent);

    }
    static string FormatHtmlBody(string body, string title)
    {
        body = $"<body>{body}</body>";
        var head = File.ReadAllText("assets/head.html");
        title = $"<title>{title}</title>";
        head = $"<head>{head}{title}</head>";
        var html = $"<html>{head}{body}</html>";
        html = "<!doctype html>" + html;
        var tail = File.ReadAllText("assets/tail.html");
        html += tail;
        return html;
    }
}
