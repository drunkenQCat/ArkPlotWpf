using System;

namespace ArkPlotWpf.Utilities;

public class NotificationBlock
{
    private static readonly Lazy<NotificationBlock> InstanceLazy =
        new Lazy<NotificationBlock>(() => new NotificationBlock());

    public static NotificationBlock Instance => InstanceLazy.Value;

    public event EventHandler<string>? CommonEventHandler;
    public event EventHandler<ChapterLoadedEventArgs>? ChapterLoaded;
    public event EventHandler<LineNoMatchEventArgs>? LineNoMatch;
    public event EventHandler<NetworkErrorEventArgs>? NetErrorHappen;

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
    public NetworkErrorEventArgs(string message)
    {
        Message = message;
    }

    public string Message { get; }
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
