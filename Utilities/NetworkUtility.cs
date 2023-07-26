using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ArkPlotWpf.Utilities;

public static class NetworkUtility
{
    public static async Task<string> GetAsync(string url)
    {
        // 发送一个request请求

        using var client = new HttpClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            if (response.ReasonPhrase != null)
                NotificationBlock.Instance.OnNetErrorHappen(new NetworkErrorEventArgs(response.ReasonPhrase));
            return "";
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[4096];
        var isMoreToRead = true;
        var memoryStream = new MemoryStream();
        do
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                isMoreToRead = false;
            }
            else
            {
                await memoryStream.WriteAsync(buffer.AsMemory(0, read));
            }
        } while (isMoreToRead);
        var fileContent = Encoding.UTF8.GetString(memoryStream.ToArray());
        return fileContent;
    }
    
    // 获取查询的json
    public static async Task<string> GetJsonContent(string plotsJsonRequestUrl)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage();
        request.RequestUri = new Uri(plotsJsonRequestUrl);
        request.Method = HttpMethod.Get;
        request.Headers.Add("Accept", "application/json");
        try
        {
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (HttpRequestException e)
        {
            NotificationBlock.Instance.OnNetErrorHappen(new NetworkErrorEventArgs(e.Message));
            return  "";
        }
    }
}