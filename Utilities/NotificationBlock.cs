using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArkPlotWpf.Utilities;

public class NotificationBlock
{
    private static readonly Lazy<NotificationBlock> _instance =
        new Lazy<NotificationBlock>(() => new NotificationBlock());

    public static NotificationBlock Instance => _instance.Value;

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

    internal void OnLineNoMatch(LineNoMatchEventArgs lineNoMatchEventArgs)
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

public class  ChapterLoadedEventArgs : EventArgs
{
    public ChapterLoadedEventArgs(string title)
    {
        Title = title;
    }

    public string Title { get; }
}

public class NotificationOut : INotifyPropertyChanged, INotifyPropertyChanging
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;
    private string _color = null!;
    private string _content = null!;


    public string Color
    {
        get => _color;
        set
        {
            _color = value;
            OnPropertyChanged();
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            _content = value;
            OnPropertyChanged();
        }
    }

    public NotificationOut(string content, string color)
    {
        Color = color;
        Content = content;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null!)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

