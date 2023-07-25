using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArkPlotWpf.Utilities;
public class MyEventClass
{
    private static readonly Lazy<MyEventClass> _instance = new Lazy<MyEventClass>(() => new MyEventClass());

    public static MyEventClass Instance => _instance.Value;

    public event EventHandler<string> MyEvent;

    public void RaiseMyEvent(string message)
    {
        MyEvent?.Invoke(this, message);
    }
}


public class ConsoleOut : INotifyPropertyChanged
{
    private string _color = null!;
    private string _content = null!;

    public event PropertyChangedEventHandler? PropertyChanged = null!;

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

    public ConsoleOut(string content, string color)
    {
        Color = color;
        Content = content;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null!)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

