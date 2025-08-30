using System;
using System.Net.Http;

namespace ArkPlot.Core.Services;

public class NotificationBlock
{
    private static readonly Lazy<NotificationBlock> InstanceLazy = new(() => new NotificationBlock());

    public static NotificationBlock Instance => InstanceLazy.Value;

    public event EventHandler<string>? CommonEventHandler;
    public event EventHandler<ChapterLoadedEventArgs>? ChapterLoaded;
    public event EventHandler<LineNoMatchEventArgs>? LineNoMatch;
    public event EventHandler<NetworkErrorEventArgs>? NetErrorHappen;

    /// <summary>
    /// 触发一个事件，并传递指定的消息。消息会显示在界面上。
    /// </summary>
    /// <param name="message">要传递给事件处理程序的消息。</param>
    public void RaiseCommonEvent(string message)
    {
        CommonEventHandler?.Invoke(this, message);
    }

    internal void OnChapterLoaded(ChapterLoadedEventArgs chapterLoadedEventArgs)
    {
        ChapterLoaded?.Invoke(this, chapterLoadedEventArgs);
    }

    internal void OnNoMatchTag(LineNoMatchEventArgs lineNoMatchEventArgs)
    {
        LineNoMatch?.Invoke(this, lineNoMatchEventArgs);
    }

    internal void OnNetErrorHappen(NetworkErrorEventArgs networkErrorEventArgs)
    {
        NetErrorHappen?.Invoke(this, networkErrorEventArgs);
    }
}

public class NetworkErrorEventArgs : EventArgs
{
    public NetworkErrorEventArgs(string? message)
    {
        Message = message;
    }

    public NetworkErrorEventArgs(HttpResponseMessage response)
    {
        Message = response.ReasonPhrase + $"\n请求内容：{response.RequestMessage}";
    }

    public string? Message { get; }
}

public class LineNoMatchEventArgs : EventArgs

{
    public LineNoMatchEventArgs(string line, string tag)
    {
        Line = line;
        Tag = tag;
    }

    public string Line { get; }
    public string Tag { get; }
}

public class ChapterLoadedEventArgs : EventArgs
{
    public ChapterLoadedEventArgs(string title)
    {
        Title = title;
    }

    public string Title { get; }
}