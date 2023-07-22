using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArkPlotWpf.Model;

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

