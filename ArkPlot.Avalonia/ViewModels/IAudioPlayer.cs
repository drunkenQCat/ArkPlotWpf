using System;

namespace ArkPlot.Avalonia.ViewModels;

public interface IAudioPlayer : IDisposable
{
    void LoadFile(string path);
    void Play();
    void Pause();
    void Stop();
    void SeekToSeconds(double seconds);
    double GetLengthInSeconds();
    double GetCursorInSeconds();
    bool IsPlaying();
    event EventHandler? PlaybackEnded;
}
