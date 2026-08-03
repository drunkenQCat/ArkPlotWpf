using Xunit;

namespace ArkPlot.Novelizer.Tests;

/// <summary>
/// 回归测试：验证 ChapterProcessor 在多轮模式下触发上下文压缩时，
/// 真正把压缩指令 CompressPrompt 作为最后一条 user 消息发给 LLM，
/// 而不是让模型顺着历史继续续写。
/// </summary>
public class ChapterProcessorCompressionTests
{
    [Fact]
    public async Task MultiTurn_CompressInterval_Reaches_CompressRequest_Includes_CompressPrompt()
    {
        // Arrange
        // 正文长度必须 > chunkSize，且每个原始段远小于 chunkSize，
        // 段间用 --- 分隔，ChapterChunker 才能拆出多个 chunk，进而在第 compressInterval 轮后触发压缩。
        var chunkSize = 50;
        var compressInterval = 2;
        var segments = Enumerable.Range(0, 50).Select(i => $"段落{i}：" + new string('文', 10));
        var body = string.Join("\n\n---\n\n", segments);

        var chapter = new Chapter(0, "测试章", body);
        var fakeClient = new HistoryRecordingBailianClient();
        var processor = new ChapterProcessor(
            fakeClient,
            systemPrompt: "你是小说生成助手",
            log: _ => { },
            logError: _ => { },
            maxConcurrency: 1,
            enableMultiTurn: true,
            chunkSize: chunkSize,
            compressInterval: compressInterval);

        // Act
        var results = await processor.ProcessAllAsync(new[] { chapter }, model: "fake-model");

        // Assert
        Assert.Single(results);
        Assert.True(results[0].IsSuccess);

        // 触发压缩：chunk 数量必须 > 2，否则不会触发
        var compressCalls = fakeClient.HistoryCalls.Where(c => c.Messages.LastOrDefault()?.Content.Contains("紧凑的情节摘要") == true).ToList();
        Assert.NotEmpty(compressCalls);

        var compressCall = compressCalls.First();
        var lastMessage = compressCall.Messages.Last();
        Assert.Equal("user", lastMessage.Role);
        Assert.Contains("压缩为一份紧凑的情节摘要", lastMessage.Content);

        // 压缩调用里必须包含此前已生成的小说化内容，而不是只有 system prompt
        var userMessagesBeforeCompress = compressCall.Messages.Where(m => m.Role == "user").ToList();
        Assert.True(userMessagesBeforeCompress.Count >= 2, "压缩请求应至少包含原始 user 请求和压缩指令");
    }

    [Fact]
    public async Task MultiTurn_After_Compress_History_Is_Reset_To_System_Plus_Summary()
    {
        // Arrange
        var chunkSize = 50;
        var compressInterval = 2;
        var segments = Enumerable.Range(0, 50).Select(i => $"段落{i}：" + new string('文', 10));
        var body = string.Join("\n\n---\n\n", segments);

        var chapter = new Chapter(0, "测试章", body);
        var fakeClient = new HistoryRecordingBailianClient();
        var processor = new ChapterProcessor(
            fakeClient,
            systemPrompt: "你是小说生成助手",
            log: _ => { },
            logError: _ => { },
            maxConcurrency: 1,
            enableMultiTurn: true,
            chunkSize: chunkSize,
            compressInterval: compressInterval);

        // Act
        var results = await processor.ProcessAllAsync(new[] { chapter }, model: "fake-model");

        // Assert
        Assert.True(results[0].IsSuccess);

        // 找到压缩后的下一轮小说化请求，确认其历史已经被重置为 system + summary
        var compressCalls = fakeClient.HistoryCalls.Where(c => c.Messages.LastOrDefault()?.Content.Contains("紧凑的情节摘要") == true).ToList();
        Assert.NotEmpty(compressCalls);

        // 压缩后下一次 ChatWithHistoryAsync 的 messages 数量应该很小（2：system + 新 user 请求），
        // 而不是携带完整历史。
        var compressIndex = fakeClient.HistoryCalls.IndexOf(compressCalls.First());
        Assert.True(compressIndex + 1 < fakeClient.HistoryCalls.Count, "压缩后应该还有后续小说化调用");

        var postCompressCall = fakeClient.HistoryCalls[compressIndex + 1];
        Assert.True(postCompressCall.Messages.Count <= 3, $"压缩后历史应被重置，实际 {postCompressCall.Messages.Count} 条");
        Assert.Contains(postCompressCall.Messages, m => m.Role == "system" && m.Content.Contains("此前已生成的小说情节摘要"));
    }

    /// <summary>
    /// 记录每次 ChatWithHistoryAsync 收到的消息，并返回固定摘要/续写结果。
    /// </summary>
    private class HistoryRecordingBailianClient : BailianClient
    {
        public HistoryRecordingBailianClient()
            : base(new HttpClient(), new ApiConfig { Provider = ApiProvider.Bailian, ApiKey = "fake" })
        {
        }

        public List<(string Model, IReadOnlyList<ChatMessage> Messages)> HistoryCalls { get; } = new();

        public override Task<ChatResult> ChatAsync(string model, string systemPrompt, string userContent)
        {
            return Task.FromResult(new ChatResult("", "单次小说化结果", null));
        }

        public override Task<ChatResult> ChatWithHistoryAsync(string model, IReadOnlyList<ChatMessage> messages)
        {
            HistoryCalls.Add((model, messages.ToList()));

            // 判断是压缩请求（最后一条 user 包含压缩指令）还是普通续写请求
            var lastUser = messages.LastOrDefault(m => m.Role == "user");
            var answer = lastUser?.Content.Contains("压缩") == true
                ? "压缩摘要：角色A做了X，角色B做了Y。"
                : $"续写结果-{HistoryCalls.Count}";

            return Task.FromResult(new ChatResult("", answer, null));
        }
    }
}
