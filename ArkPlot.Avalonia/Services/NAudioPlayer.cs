using System;
using NAudio.Wave;

namespace ArkPlot.Avalonia.Services;

public sealed class NAudioPlayer : ViewModels.IAudioPlayer
{
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioReader;

    public event EventHandler? PlaybackEnded;

    public void LoadFile(string path)
    {
        Stop();
        Cleanup();

        _audioReader = new AudioFileReader(path);
        _wavePlayer = new WaveOutEvent();
        _wavePlayer.PlaybackStopped += OnPlaybackStopped;
        _wavePlayer.Init(_audioReader);
    }

    public void Play()
    {
        if (_wavePlayer?.PlaybackState == PlaybackState.Paused)
            _wavePlayer.Play();
        else if (_wavePlayer?.PlaybackState != PlaybackState.Playing)
        {
            _wavePlayer?.Play();
        }
    }

    public void Pause() => _wavePlayer?.Pause();

    public void Stop()
    {
        _wavePlayer?.Stop();
        if (_audioReader != null)
            _audioReader.Position = 0;
    }

    public void SeekToSeconds(double seconds)
    {
        if (_audioReader == null) return;
        _audioReader.CurrentTime = TimeSpan.FromSeconds(seconds);
    }

    public double GetLengthInSeconds() => _audioReader?.TotalTime.TotalSeconds ?? 0;
    public double GetCursorInSeconds() => _audioReader?.CurrentTime.TotalSeconds ?? 0;

    public bool IsPlaying()
    {
        if (_wavePlayer == null) return false;
        return _wavePlayer.PlaybackState == PlaybackState.Playing;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void Cleanup()
    {
        if (_wavePlayer != null)
        {
            _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
            _wavePlayer.Dispose();
        }
        _wavePlayer = null;
        _audioReader?.Dispose();
        _audioReader = null;
    }

    public void Dispose()
    {
        Stop();
        Cleanup();
    }
}
