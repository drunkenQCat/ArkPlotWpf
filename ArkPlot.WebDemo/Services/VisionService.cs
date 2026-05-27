using ArkPlot.Vision;

namespace ArkPlot.WebDemo.Services;

/// <summary>
/// 视觉描述服务：百炼优先，奥拉玛回退。
/// </summary>
public class VisionService : IAsyncDisposable
{
    private readonly ILogger<VisionService> _logger;
    private Func<string, Task<string>>? _describeByUrl;
    private IDisposable? _clientDisposable;
    private bool _initialized;
    private string _backendName = "未初始化";

    public string BackendName => _backendName;
    public bool IsAvailable => _describeByUrl != null;

    public VisionService(ILogger<VisionService> logger)
    {
        _logger = logger;
    }

    /// <summary>尝试初始化视觉客户端。</summary>
    public bool Initialize()
    {
        if (_initialized) return IsAvailable;
        _initialized = true;

        // 优先百炼
        var apiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? "";
        if (!string.IsNullOrEmpty(apiKey))
        {
            var config = new BailianVisionConfig
            {
                ApiKey = apiKey,
                Model = "qwen3-vl-flash",
                TimeoutSeconds = 60,
                SystemPrompt = "请用中文详细描述这张图片中的所有视觉元素，包括角色、场景、动作、服饰、背景等细节。直接输出描述内容，不要加任何前缀或总结性语句。",
                MaxTokens = 2048
            };
            var client = new BailianVisionClient(config, onLog: msg => _logger.LogInformation("[Vision] {Msg}", msg));
            _describeByUrl = async url => await client.DescribeImageUrlAsync(url);
            _clientDisposable = client;
            _backendName = "百炼 (qwen3-vl-flash)";
            return true;
        }

        // 回退奥拉玛
        try
        {
            var visionConfig = new VisionConfig
            {
                BaseUrl = "http://localhost:11434",
                Model = "qwen3-vl:8b",
                TimeoutSeconds = 600,
                SystemPrompt = "请用中文详细描述这张图片中的所有视觉元素，包括角色、场景、动作、服饰、背景等细节。直接输出描述内容，不要加任何前缀或总结性语句。",
                Temperature = 0.7f,
                MaxTokens = 2048
            };
            var ollamaClient = new OllamaVisionClient(visionConfig, onLog: msg => _logger.LogInformation("[Vision] {Msg}", msg));
            _describeByUrl = async url =>
            {
                var tempPath = Path.GetTempFileName();
                try
                {
                    using var http = new HttpClient();
                    http.Timeout = TimeSpan.FromMinutes(5);
                    var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    await using var fs = File.Create(tempPath);
                    await (await response.Content.ReadAsStreamAsync()).CopyToAsync(fs);
                    return await ollamaClient.DescribeImageAsync(tempPath);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            };
            _clientDisposable = ollamaClient;
            _backendName = "Ollama (qwen3-vl:8b)";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama 不可用");
            _backendName = "无可用后端";
            return false;
        }
    }

    /// <summary>描述指定 URL 的图片。</summary>
    public async Task<string> DescribeAsync(string imageUrl)
    {
        if (_describeByUrl == null)
            throw new InvalidOperationException("视觉服务未初始化或无可用后端");
        return await _describeByUrl(imageUrl);
    }

    public ValueTask DisposeAsync()
    {
        _clientDisposable?.Dispose();
        return ValueTask.CompletedTask;
    }
}
