using ArkPlotWpf.Model;
using Markdig;
using System.IO;

namespace ArkPlotWpf.Utilities;

internal abstract class AkpProcessor
{
    public static string ExportPlots(List<Plot> plotList, string jsonPath)
    {
        var md = new StringBuilder();
        var parser = new AkpParser(jsonPath);
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
        string htmlPath = path + "\\" + markdown.Title + ".html";
        string htmlContent = GetHtmlContent(htmlPath, markdown);
        string result = FormatHtmlBody(htmlContent, markdown.Title);
        File.WriteAllText(htmlPath, result);
    }

    public static void WriteHtmlWithLocalRes(string path, Plot markdown)
    {
        string htmlPath = path + "\\" + markdown.Title + ".html";
        string htmlContent = GetHtmlContent(htmlPath, markdown);
        string htmlWithLocalRes = htmlContent.Replace("https://", "");
        var result = FormatHtmlBody(htmlWithLocalRes, markdown.Title);
        File.WriteAllText(htmlPath, result);
    }

    private static string GetHtmlContent(string path, Plot markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
                          .UseAdvancedExtensions()
                          .Build();
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // Add most of all advanced extensions
            .Build();
        return Markdown.ToHtml(markdown.Content.ToString(), pipeline);
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
