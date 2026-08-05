namespace ArkPlot.Novelizer;

/// <summary>
/// 不调用真实 API 的 Mock 客户端，用于开发调试时跑通小说化管线。
/// 覆盖 ChatAsync / ChatWithHistoryAsync 返回预置占位文本，不产生任何网络请求。
/// </summary>
public class MockBailianClient : BailianClient
{
    private readonly Action<string>? _onLog;

    public MockBailianClient(HttpClient http, ApiConfig config, Action<string>? onLog = null)
        : base(http, config, onLog)
    {
        _onLog = onLog;
    }

    public override Task<ChatResult> ChatAsync(string model, string systemPrompt, string userContent)
    {
        _onLog?.Invoke(
            $"[Mock] 跳过真实 API。model={model}, 输入长度={userContent.Length}"
        );
        return Task.FromResult(
            new ChatResult("", BuildMockContent(userContent), new TokenUsage(0, 0, 0)));
    }

    public override Task<ChatResult> ChatWithHistoryAsync(
        string model,
        IReadOnlyList<ChatMessage> messages)
    {
        var user = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        _onLog?.Invoke(
            $"[Mock] 跳过真实 API。model={model}, 消息数={messages.Count}"
        );
        return Task.FromResult(
            new ChatResult("", BuildMockContent(user), new TokenUsage(0, 0, 0)));
    }

    private static string BuildMockContent(string input)
    {
        var head = input.Length <= 300 ? input : input[..300] + "...";
        return $"""
（Mock 输出，未调用真实 API）

这是小说化管线的占位结果，用于验证流程是否跑通。

输入预览：
{head}
""";
    }
}