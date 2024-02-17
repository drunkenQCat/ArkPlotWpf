using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;

namespace ArkPlotWpf.Utilities.PrtsComponents;

public class PrtsResLoader
{
    // 下载 assets 里面的所有 assets。要求他们放到 output 文件夹下的一个文件夹叫做 preload。
    // 保存的时候要按照链接,按文件夹保存。比如说一个链接是 https://example.com/1.png, 那么就要保存到 output/preload/example.com/1.png
    public static async Task DownloadAssets(PreloadSet assets)
    {
        var httpClient = new HttpClient();

        foreach (var asset in assets)
        {
            var url = asset.Value;
            var fullPath = GetLocalPathFromUrl(url);
            var directoryPath = Path.GetDirectoryName(fullPath);
            EnsureDirectoryExists(directoryPath!);
            await DownloadFileAsync(httpClient, url, fullPath);
        }
    }

    public static string GetLocalPathFromUrl(string url)
    {
        var uri = new Uri(url);
        var localPath = Path.Combine(uri.Host, uri.AbsolutePath.TrimStart('/'));
        return localPath;
    }

    public static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _ = Directory.CreateDirectory(directoryPath);
        }
    }

    public static async Task DownloadFileAsync(HttpClient httpClient, string url, string fullPath)
    {
        var content = await httpClient.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(fullPath, content);
        Console.WriteLine($"Downloaded: {url} to {fullPath}");
    }

}