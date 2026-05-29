using SqlSugar;

namespace ArkPlot.Core.Model;

/// <summary>
/// 数据源同步状态，记录每次从 GitHub 拉取的 commit SHA。
/// 用于判断远程数据是否有更新。
/// </summary>
[SugarTable("SyncState")]
public class SyncState
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    /// <summary>
    /// 语言标识：zh_CN / en / ja / ko
    /// </summary>
    [SugarColumn(Length = 10, IsNullable = false)]
    public string Lang { get; set; } = string.Empty;

    /// <summary>
    /// 仓库路径，如 "Kengxxiao/ArknightsGameData"
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Repo { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次成功同步的 commit SHA
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? LastCommitSha { get; set; }

    /// <summary>
    /// 最近同步时间
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// PRTS 数据上次同步时的 commit SHA（与 LastCommitSha 对比来判断是否需要重下）
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? PrtsSyncedAtSha { get; set; }
}
