using ArkPlot.Tts.Engines;

namespace ArkPlot.Tts.Tests;

public class EdgeTtsEngineTests
{
    [Theory]
    [InlineData("unable to connect to server")]
    [InlineData("connection refused")]
    [InlineData("timeout exceeded")]
    [InlineData("connection reset by peer")]
    [InlineData("websocket error occurred")]
    public void IsTransientError_NetworkErrors_ReturnsTrue(string message)
    {
        var ex = new Exception(message);
        Assert.True(EdgeTtsEngine.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_HttpRequestException_ReturnsTrue()
    {
        var ex = new System.Net.Http.HttpRequestException("bad gateway");
        Assert.True(EdgeTtsEngine.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_SocketException_ReturnsTrue()
    {
        var ex = new System.Net.Sockets.SocketException();
        Assert.True(EdgeTtsEngine.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_TaskCanceledException_ReturnsTrue()
    {
        var ex = new TaskCanceledException();
        Assert.True(EdgeTtsEngine.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_UnrelatedException_ReturnsFalse()
    {
        var ex = new InvalidOperationException("some logic error");
        Assert.False(EdgeTtsEngine.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_ArgumentException_ReturnsFalse()
    {
        var ex = new ArgumentException("bad argument");
        Assert.False(EdgeTtsEngine.IsTransientError(ex));
    }
}
