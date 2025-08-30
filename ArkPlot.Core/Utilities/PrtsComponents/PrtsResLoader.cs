using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ArkPlot.Core.Services;
using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;

namespace ArkPlot.Core.Utilities.PrtsComponents;

public class PrtsResLoader
{
    // 下载 assets 里面的所�?assets。要求他们放�?output 文件夹下
    // 保存的时候要按照链接,按文件夹保存。比如说一个链接是 https://example.com/1.png,当前活动名是“阴云火花”，那么就要保存�?output/阴云火花/example.com/1.png
    public static async Task DownloadAssets(string storyName, PreloadSet assets)
    {
        var httpClient = new HttpClient();

        foreach (var asset in assets)
        {
            var url = asset.Value;
            var fullPath = GetLocalPathFromUrl(storyName, url);
            var directoryPath = Path.GetDirectoryName(fullPath);
            EnsureDirectoryExists(directoryPath!);
            if (!File.Exists(fullPath)) await DownloadFileAsync(httpClient, url, fullPath);
        }
    }

    private static string GetLocalPathFromUrl(string storyName, string url)
    {
        var uri = new Uri(url);
        var localPath = Path.Join(uri.Host, uri.AbsolutePath.TrimStart('/'));
        return Path.Join("output", storyName, localPath);
    }


    public static string GetRelativePathFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        var uri = new Uri(url);
        var localPath = Path.Combine(uri.Host, uri.AbsolutePath.TrimStart('/'));
        return localPath;
    }

    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) _ = Directory.CreateDirectory(directoryPath);
    }

    private static async Task DownloadFileAsync(HttpClient httpClient, string url, string fullPath)
    {
        var notice = NotificationBlock.Instance;
        try
        {
            var content = await httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(fullPath, content);
            notice.RaiseCommonEvent($"Downloaded: {url} to {fullPath}");
        }
        catch (HttpRequestException httpEx)
        {
            // 处理网络请求相关的异�?
            notice.OnNetErrorHappen(new NetworkErrorEventArgs(
                $"An error occurred while downloading {url}. Error: {httpEx.Message}"
            ));
        }
        catch (IOException ioEx)
        {
            // 处理文件写入相关的异�?
            notice.OnNetErrorHappen(new NetworkErrorEventArgs(
                $"An error occurred while writing to {fullPath}. Error: {ioEx.Message}"
            ));
        }
        catch (Exception ex)
        {
            // 处理其他可能发生的异�?
            notice.OnNetErrorHappen(new NetworkErrorEventArgs(
                $"An unexpected error occurred. Error: {ex.Message}"
            ));
        }
    }
}
