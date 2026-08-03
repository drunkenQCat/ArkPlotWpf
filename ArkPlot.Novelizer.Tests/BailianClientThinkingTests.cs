using System.Net;
using System.Text;
using System.Text.Json;
using ArkPlot.Novelizer;
using Xunit;

namespace ArkPlot.Novelizer.Tests;

/// <summary>
/// 验证 DeepSeek/百炼 下 thinking 字段的发送逻辑。
/// 分节（Pass 2）等轻量任务在 DeepSeek 下必须关闭思考，否则 token 暴涨。
/// </summary>
public class BailianClientThinkingTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastRequestBody = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}]}",
                    Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private static ApiConfig Config(ApiProvider provider, bool enableThinking) => new()
    {
        Provider = provider,
        ApiKey = "test-key",
        BaseUrl = "https://api.example.com",
        Models = ["deepseek-v4-flash"],
        EnableThinking = enableThinking,
        MaxTokens = 1000
    };

    private static async Task<string> CaptureBody(ApiProvider provider, bool enableThinking)
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var client = new BailianClient(http, Config(provider, enableThinking));
        await client.ChatAsync("deepseek-v4-flash", "system", "user");
        return handler.LastRequestBody!;
    }

    private static JsonElement Root(string body)
        => JsonDocument.Parse(body).RootElement;

    [Fact]
    public async Task DeepSeek_EnableThinkingTrue_SendsThinkingFields()
    {
        var body = await CaptureBody(ApiProvider.DeepSeek, enableThinking: true);
        var root = Root(body);

        Assert.True(root.TryGetProperty("reasoning_effort", out _), "应包含 reasoning_effort");
        Assert.True(root.TryGetProperty("extra_body", out var extra), "应包含 extra_body");
        Assert.Equal("enabled", extra.GetProperty("thinking").GetProperty("type").GetString());
    }

    [Fact]
    public async Task DeepSeek_EnableThinkingFalse_SkipsThinkingFields()
    {
        var body = await CaptureBody(ApiProvider.DeepSeek, enableThinking: false);
        var root = Root(body);

        Assert.False(root.TryGetProperty("reasoning_effort", out _), "不应包含 reasoning_effort");
        Assert.False(root.TryGetProperty("extra_body", out _), "不应包含 extra_body");
    }

    [Fact]
    public async Task Bailian_AlwaysSendsEnableThinkingFlag()
    {
        var body = await CaptureBody(ApiProvider.Bailian, enableThinking: false);
        var root = Root(body);

        Assert.True(root.TryGetProperty("enable_thinking", out var flag), "百炼应包含 enable_thinking");
        Assert.False(flag.GetBoolean());
    }
}