using ArkPlot.Arknights;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Novelizer;
using SqlSugar;
using Xunit;

namespace ArkPlot.Novelizer.Tests;

/// <summary>
/// TTS 分节器逻辑测试：不调用真实 LLM，只验证 Bg 变化点检测、切片定位、JSON 解析与插入应用。
/// </summary>
public class SectionSplitterTests : IDisposable
{
    private readonly SqlSugarClient _db;
    private long _plotId;

    private readonly string _dbPath;

    public SectionSplitterTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"arkplot_section_splitter_{Guid.NewGuid()}.db");
        _db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                SerializeService = new SystemTextJsonSerializer()
            }
        });
        InitSchema();
    }

    public void Dispose()
    {
        _db.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }

    private void InitSchema()
    {
        _db.CodeFirst.InitTables(typeof(Plot), typeof(FormattedTextEntry));
    }

    private long SeedPlot(string title = "CW-ST-1")
    {
        var plot = new Plot { Title = title };
        return _db.Insertable(plot).ExecuteReturnBigIdentity();
    }

    private void SeedEntries(params (int index, string bg, string dialog, string mdText)[] rows)
    {
        var entries = rows.Select(r => new FormattedTextEntry
        {
            PlotId = _plotId,
            Index = r.index,
            Bg = r.bg,
            Dialog = r.dialog,
            MdText = r.mdText,
            Type = "dialog"
        }).ToList();

        _db.Insertable(entries).ExecuteCommand();
    }

    [Fact]
    public void FindBgTransitions_FiltersBlackBg()
    {
        _plotId = SeedPlot();
        SeedEntries(
            (0, "bg_office", "你好", "**A**：你好"),
            (1, "bg_black", "", ""),
            (2, "bg_prison", "怎么回事", "**A**：怎么回事")
        );

        var splitter = new SectionSplitter(new FakeBailianClient("[]"), _db);
        var result = splitter.SplitAsync("some novel", _plotId).Result;

        // bg_black 被过滤，无变化点，直接返回原文
        Assert.Equal("some novel", result);
    }

    [Fact]
    public void LocateAndApplyInsertion_InsertsAtAnchor()
    {
        _plotId = SeedPlot();
        SeedEntries(
            (0, "bg_office", "这是第一句话", "**A**：这是第一句话"),
            (1, "bg_prison", "场景已经变了", "**A**：场景已经变了")
        );

        var novel = "这是第一句话，情节在继续。场景已经变了，故事继续发展。";
        var json = "[{\"anchor\":\"场景已经变了\",\"insert_before\":\"第二节 监狱\\n\\n——另一边，\"}]";
        var splitter = new SectionSplitter(new FakeBailianClient(json), _db);

        var result = splitter.SplitAsync(novel, _plotId).Result;

        Assert.Contains("第二节 监狱", result);
        Assert.Contains("——另一边，", result);
        Assert.Contains("场景已经变了", result);
    }

    [Fact]
    public void LocateAndApplyInsertion_FallsBackToPunctuationFreeAnchor()
    {
        _plotId = SeedPlot();
        SeedEntries(
            (0, "bg_office", "第一句", "**A**：第一句"),
            (1, "bg_prison", "变，场景", "**A**：变，场景")
        );

        var novel = "第一句。变场景。";
        var json = "[{\"anchor\":\"变，场景\",\"insert_before\":\"第二节 转折\\n\\n——三天后，\"}]";
        var splitter = new SectionSplitter(new FakeBailianClient(json), _db);

        var result = splitter.SplitAsync(novel, _plotId).Result;

        Assert.Contains("第二节 转折", result);
        Assert.Contains("——三天后，", result);
    }

    [Fact]
    public void ApplyInsertion_SkipsInvalidJson()
    {
        _plotId = SeedPlot();
        SeedEntries(
            (0, "bg_office", "开始", "**A**：开始"),
            (1, "bg_prison", "切换", "**A**：切换")
        );

        var novel = "开始。切换。";
        var splitter = new SectionSplitter(new FakeBailianClient("not json"), _db);

        var result = splitter.SplitAsync(novel, _plotId).Result;

        Assert.Equal(novel, result);
    }

    [Fact]
    public void ApplyInsertion_SkipsMissingAnchor()
    {
        _plotId = SeedPlot();
        SeedEntries(
            (0, "bg_office", "开始", "**A**：开始"),
            (1, "bg_prison", "切换", "**A**：切换")
        );

        var novel = "开始。切换。";
        var json = "[{\"anchor\":\"不存在的文本\",\"insert_before\":\"第二节 不存在\\n\\n——切换，\"}]";
        var splitter = new SectionSplitter(new FakeBailianClient(json), _db);

        var result = splitter.SplitAsync(novel, _plotId).Result;

        Assert.Equal(novel, result);
    }

    [Fact]
    public void ApplyInsertion_HandlesMarkdownCodeBlock()
    {
        _plotId = SeedPlot();
        SeedEntries(
            (0, "bg_office", "开始", "**A**：开始"),
            (1, "bg_prison", "切换", "**A**：切换")
        );

        var novel = "开始。切换。";
        var json = "```json\n[{\"anchor\":\"切换\",\"insert_before\":\"第二节 代码块\\n\\n——与此同时，\"}]\n```";
        var splitter = new SectionSplitter(new FakeBailianClient(json), _db);

        var result = splitter.SplitAsync(novel, _plotId).Result;

        Assert.Contains("第二节 代码块", result);
    }

    [Fact]
    public void SplitAsync_ReturnsOriginalText_WhenNoBgChanges()
    {
        _plotId = SeedPlot();
        SeedEntries(
            (0, "bg_office", "开始", "**A**：开始"),
            (1, "bg_office", "继续", "**A**：继续")
        );

        var novel = "开始继续。";
        var splitter = new SectionSplitter(new FakeBailianClient("[]"), _db);

        var result = splitter.SplitAsync(novel, _plotId).Result;

        Assert.Equal(novel, result);
    }

    [Fact]
    public void SplitAsync_KeepsOrder_WhenMultipleInsertions()
    {
        _plotId = SeedPlot();
        SeedEntries(
            (0, "bg_a", "开端", "**A**：开端"),
            (1, "bg_b", "中段", "**A**：中段"),
            (2, "bg_c", "结尾", "**A**：结尾")
        );

        var novel = "开端。中段。结尾。";
        var json = "[" +
            "{\"anchor\":\"中段\",\"insert_before\":\"第二节 中段\\n\\n——三天后，\"}," +
            "{\"anchor\":\"结尾\",\"insert_before\":\"第三节 结尾\\n\\n——另一边，\"}" +
            "]";
        var splitter = new SectionSplitter(new FakeBailianClient(json), _db);

        var result = splitter.SplitAsync(novel, _plotId).Result;

        var midIndex = result.IndexOf("第二节 中段", StringComparison.Ordinal);
        var endIndex = result.IndexOf("第三节 结尾", StringComparison.Ordinal);
        Assert.True(midIndex < endIndex);
    }

    private class FakeBailianClient : BailianClient
    {
        private readonly string _answer;

        public FakeBailianClient(string answer)
            : base(new HttpClient(), new ApiConfig { Provider = ApiProvider.Bailian, ApiKey = "fake" })
        {
            _answer = answer;
        }

        public override Task<ChatResult> ChatAsync(string model, string systemPrompt, string userContent)
        {
            return Task.FromResult(new ChatResult("", _answer, null));
        }
    }
}
