using Xunit;

namespace ArkPlot.Novelizer.Tests;

/// <summary>
/// 共享数据库测试集合：所有直接连 arkplot.db 的测试类必须加入此集合，
/// 避免并行访问静态 DbFactory 单例导致连接冲突。
/// </summary>
[CollectionDefinition("SharedDb")]
public class SharedDbCollectionDefinition { }