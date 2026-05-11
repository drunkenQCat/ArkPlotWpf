using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArkPlot.Novelizer;

public class BailianClient
{
    private readonly HttpClient _http;
    private readonly BailianConfig _config;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public BailianClient(HttpClient http, BailianConfig config)
    {
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri(config.BaseUrl.EndsWith('/') ? config.BaseUrl : config.BaseUrl + "/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.ApiKey);
        _http.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    }

    /// <summary>
    /// 调用百炼 Chat Completions API，返回 (reasoningContent, answerContent)
    /// </summary>
    public async Task<ChatResult> ChatAsync(string model, string systemPrompt, string userContent)
    {
        var requestBody = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            },
            max_tokens = _config.MaxTokens,
            extra_body = new { enable_thinking = _config.EnableThinking }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        int attempt = 0;

        while (true)
        {
            attempt++;
            response = await _http.PostAsync("chat/completions", content);

            if (response.IsSuccessStatusCode)
                break;

            if (attempt >= _config.MaxRetries)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new BailianException(
                    $"API 调用失败（{response.StatusCode}），已重试 {attempt} 次: {errorBody}");
            }

            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                Console.Error.WriteLine($"  [{model}] {response.StatusCode}，{delay.TotalSeconds}s 后重试 ({attempt}/{_config.MaxRetries})...");
                await Task.Delay(delay);
                continue;
            }

            var errBody = await response.Content.ReadAsStringAsync();
            throw new BailianException($"API 调用失败（{response.StatusCode}）: {errBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var message = choice.GetProperty("message");

        var reasoning = message.TryGetProperty("reasoning_content", out var r) ? r.GetString() ?? "" : "";
        var answer = message.TryGetProperty("content", out var a) ? a.GetString() ?? "" : "";

        var usage = doc.RootElement.TryGetProperty("usage", out var u)
            ? new TokenUsage(
                u.GetProperty("prompt_tokens").GetInt32(),
                u.GetProperty("completion_tokens").GetInt32(),
                u.GetProperty("total_tokens").GetInt32())
            : null;

        return new ChatResult(reasoning, answer, usage);
    }
}

public record ChatResult(string ReasoningContent, string AnswerContent, TokenUsage? Usage);

public record TokenUsage(int PromptTokens, int CompletionTokens, int TotalTokens);

public class BailianException : Exception
{
    public BailianException(string message) : base(message) { }
    public BailianException(string message, Exception inner) : base(message, inner) { }
}