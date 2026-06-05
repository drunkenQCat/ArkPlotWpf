using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArkPlot.Vision;

/// <summary>
/// Ollama 视觉模型客户端，支持图片描述任务
/// </summary>
public class OllamaVisionClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly VisionConfig _config;
    private readonly Action<string>? _onLog;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaVisionClient(VisionConfig? config = null, Action<string>? onLog = null)
    {
        _config = config ?? new VisionConfig();
        _onLog = onLog;
        _http = new HttpClient
        {
            BaseAddress = new Uri(_config.BaseUrl.EndsWith('/') ? _config.BaseUrl : _config.BaseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds)
        };
    }

    /// <summary>
    /// 允许外部注入 HttpClient（用于依赖注入场景）
    /// </summary>
    public OllamaVisionClient(HttpClient http, VisionConfig config, Action<string>? onLog = null)
    {
        _http = http;
        _config = config;
        _onLog = onLog;
        _http.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    }

    private void Log(string msg)
    {
        Console.WriteLine(msg);
        _onLog?.Invoke(msg);
    }

    private void LogError(string msg)
    {
        Console.Error.WriteLine(msg);
        _onLog?.Invoke(msg);
    }

    /// <summary>
    /// 描述本地图片文件
    /// </summary>
    /// <param name="imagePath">图片文件路径</param>
    /// <param name="userPrompt">可选的用户提示词，覆盖默认系统提示</param>
    /// <returns>图片描述文本</returns>
    public async Task<string> DescribeImageAsync(string imagePath, string? userPrompt = null)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"图片文件不存在: {imagePath}");

        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var base64Image = Convert.ToBase64String(imageBytes);

        return await DescribeImageBase64Async(base64Image, userPrompt);
    }

    /// <summary>
    /// 描述 Base64 编码的图片
    /// </summary>
    /// <param name="base64Image">Base64 编码的图片数据</param>
    /// <param name="userPrompt">可选的用户提示词</param>
    /// <returns>图片描述文本</returns>
    public async Task<string> DescribeImageBase64Async(string base64Image, string? userPrompt = null)
    {
        var prompt = userPrompt ?? _config.SystemPrompt;

        // 参照 Ollama 官方示例：单条 user 消息，images 和 content 在同一个 message 内
        var requestBody = new
        {
            model = _config.Model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = prompt,
                    images = new[] { base64Image }
                }
            },
            stream = false,
            options = new
            {
                temperature = _config.Temperature,
                num_predict = _config.MaxTokens
            }
        };

        return await SendRequestAsync(requestBody);
    }

    private async Task<string> SendRequestAsync(object requestBody)
    {
        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        Log($"[Vision] 开始调用 Ollama，model={_config.Model}");

        HttpResponseMessage response;
        int attempt = 0;

        while (true)
        {
            attempt++;
            Log($"[Vision] 第 {attempt} 次请求尝试...");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                response = await _http.PostAsync("api/chat", content);
                sw.Stop();
                Log($"[Vision] 收到响应 status={response.StatusCode}，耗时 {sw.Elapsed.TotalSeconds:F1}s");
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                var msg = $"[Vision] 请求超时！耗时 {sw.Elapsed.TotalSeconds:F1}s（Timeout={_config.TimeoutSeconds}s）";
                LogError(msg);
                throw new VisionException($"Ollama 请求超时（{sw.Elapsed.TotalSeconds:F0}s）", new TimeoutException());
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogError($"[Vision] 网络异常({ex.GetType().Name}): {ex.Message}");
                throw new VisionException($"Ollama 网络异常: {ex.Message}", ex);
            }

            if (response.IsSuccessStatusCode)
                break;

            if (attempt >= _config.MaxRetries)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                LogError($"[Vision] 重试耗尽。status={response.StatusCode}, body={errorBody.Truncate(200)}");
                throw new VisionException($"Ollama 调用失败（{response.StatusCode}），已重试 {attempt} 次: {errorBody.Truncate(500)}");
            }

            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                LogError($"[Vision] {response.StatusCode}，{delay.TotalSeconds}s 后重试 ({attempt}/{_config.MaxRetries})...");
                await Task.Delay(delay);
                continue;
            }

            var errBody = await response.Content.ReadAsStringAsync();
            LogError($"[Vision] 不可重试错误。status={response.StatusCode}, body={errBody.Truncate(200)}");
            throw new VisionException($"Ollama 调用失败（{response.StatusCode}）: {errBody.Truncate(500)}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        Log($"[Vision] 响应 {responseJson.Length} 字节，开始解析");

        using var doc = JsonDocument.Parse(responseJson);
        var message = doc.RootElement.GetProperty("message");
        var description = message.GetProperty("content").GetString() ?? "";

        if (string.IsNullOrWhiteSpace(description))
            throw new VisionException("Ollama 返回了空响应");

        Log($"[Vision] 描述完成，{description.Length} 字符");
        return description.Trim();
    }

    /// <summary>
    /// 批量描述图片
    /// </summary>
    /// <param name="imagePaths">图片路径列表</param>
    /// <param name="userPrompt">可选的统一提示词</param>
    /// <returns>每张图片的描述结果（按输入顺序）</returns>
    public async Task<IReadOnlyList<(string Path, string Description)>> DescribeImagesAsync(
        IEnumerable<string> imagePaths, string? userPrompt = null)
    {
        var results = new List<(string Path, string Description)>();

        foreach (var path in imagePaths)
        {
            try
            {
                var desc = await DescribeImageAsync(path, userPrompt);
                results.Add((path, desc));
            }
            catch (Exception ex)
            {
                LogError($"[Vision] 处理 {path} 失败: {ex.Message}");
                results.Add((path, $"[ERROR] {ex.Message}"));
            }
        }

        return results;
    }

    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 视觉模型异常
/// </summary>
public class VisionException : Exception
{
    public VisionException(string message) : base(message) { }
    public VisionException(string message, Exception inner) : base(message, inner) { }
}

internal static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";
}
