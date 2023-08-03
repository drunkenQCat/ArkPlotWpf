using System.IO;
using System.Text;
using System.Threading.Tasks;
using ArkPlotWpf.Model;
using Markdig;

namespace ArkPlotWpf.Utilities
{
    internal abstract class AkProcessor
    {
        public static string ExportPlots(List<Plot> plotList, string jsonPath)
        {
            var md = string.Empty;
            foreach (var chapter in plotList)
            {
                var chpName = "## " + chapter.Title;
                var chpMd = new AkParser(chapter.Content, jsonPath);
                md += chpName + "\r\n" + chpMd.MarkDown;
            }
            return md;
        }

        public static void WriteMd(string path, Plot markdown)
        {
            var mdOutPath = path + "\\" + markdown.Title + ".md";
            using var file = File.OpenWrite(mdOutPath);
            var info = new UTF8Encoding(true).GetBytes(markdown.Content);
            file.Write(info, 0, info.Length);
        }

        public static void WriteHtml(string path, Plot markdown)
        {
            var htmlPath = path + "\\" + markdown.Title + ".html";
            var htmlBody = Markdown.ToHtml(markdown.Content);
            string htmlContent = FormatHtmlBody(htmlBody, markdown.Title);
            var html = new UTF8Encoding(true).GetBytes(htmlContent);
            using var htmlFile = File.OpenWrite(htmlPath);
            htmlFile.Write(html, 0, html.Length);
        }

        private static string FormatHtmlBody(string body, string title)
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

        public static async Task MainProc(ActInfo info, string jsPah, string outputPath)
        {
            var content = new AkGetter(info);
            var activeTitle = info.Tokens["name"]?.ToString();
            //大工程，把所有的章节都下载下来
            await content.GetAllChapters();
            var allContent = content.ContentTable;
            // 处理每一章，最后导出
            var exportMd = ExportPlots(allContent, jsPah);
            var mdWithTitle = "# " + activeTitle + "\r\n\r\n" + exportMd;

            var markdown = new Plot(activeTitle!, mdWithTitle);
            AkProcessor.WriteMd(outputPath, markdown);
            AkProcessor.WriteHtml(outputPath, markdown);
        }
    }
}
