using NAudio.Wave;
using QMusic.Application.Interfaces;
using PlaybackState = QMusic.Application.Interfaces.PlaybackState;

namespace QMusic.Infrastructure.Playback;

/// <summary>
/// Real audio playback engine using NAudio.
///
/// Key types:
/// - WaveOutEvent: audio output device that uses an internal callback thread.
///   Unlike WaveOut, it doesn't need a Win32 message pump — works in WPF/Blazor.
/// - StreamMediaFoundationReader: decodes MP3/AAC/WMA/etc. from a raw Stream
///   via Windows Media Foundation. Matches our interface where providers hand over streams.
///
/// Thread safety: all mutable state is guarded by _lock. Events are fired outside
/// the lock to prevent deadlocks if subscribers call back into the engine.
/// </summary>
public sealed class NAudioPlaybackEngine : IPlaybackEngine
{
    private readonly object _lock = new();
    private WaveOutEvent? _outputDevice;
    private StreamMediaFoundationReader? _reader;
    private Timer? _positionTimer;
    private PlaybackState _state = PlaybackState.Stopped;
    private int _volume = 80;
    private bool _disposed;

    public PlaybackState State
    {
        get { lock (_lock) return _state; }
    }

    public TimeSpan CurrentPosition
    {
        get
        {
            lock (_lock)
                return _reader?.CurrentTime ?? TimeSpan.Zero;
        }
    }

    public TimeSpan TotalDuration
    {
        get
        {
            lock (_lock)
                return _reader?.TotalTime ?? TimeSpan.Zero;
        }
    }

    public int Volume
    {
        get { lock (_lock) return _volume; }
        set
        {
            lock (_lock)
            {
                _volume = Math.Clamp(value, 0, 100);
                if (_outputDevice is not null)
                    _outputDevice.Volume = _volume / 100f;
            }
        }
    }

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;

    public Task PlayAsync(Stream audioStream, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            CleanupCurrentTrack();

            _reader = new StreamMediaFoundationReader(audioStream);
            _outputDevice = new WaveOutEvent();
            _outputDevice.Volume = _volume / 100f;
            _outputDevice.PlaybackStopped += OnPlaybackStopped;
            _outputDevice.Init(_reader);
            _outputDevice.Play();

            _state = PlaybackState.Playing;
            StartPositionTimer();
        }

        StateChanged?.Invoke(this, PlaybackState.Playing);
        return Task.CompletedTask;
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_state != PlaybackState.Playing) return;
            _outputDevice?.Pause();
            _state = PlaybackState.Paused;
            StopPositionTimer();
        }

        StateChanged?.Invoke(this, PlaybackState.Paused);
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_state != PlaybackState.Paused) return;
            _outputDevice?.Play();
            _state = PlaybackState.Playing;
            StartPositionTimer();
        }

        StateChanged?.Invoke(this, PlaybackState.Playing);
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_state == PlaybackState.Stopped) return;
            _outputDevice?.Stop();
            _state = PlaybackState.Stopped;
            StopPositionTimer();
        }

        StateChanged?.Invoke(this, PlaybackState.Stopped);
    }

    public void Seek(TimeSpan position)
    {
        lock (_lock)
        {
            if (_reader is null) return;
            try
            {
                _reader.CurrentTime = position;
            }
            catch (NotSupportedException)
            {
                // Stream is not seekable (e.g. a network stream) — silently ignore
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            CleanupCurrentTrack();
        }
    }

    /// <summary>
    /// Called by NAudio when playback reaches end-of-stream or encounters an error.
    /// </summary>
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            StopPositionTimer();
            _state = PlaybackState.Stopped;
        }

        StateChanged?.Invoke(this, PlaybackState.Stopped);
    }

    private void StartPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = new Timer(OnPositionTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(250));
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    private void OnPositionTick(object? state)
    {
        TimeSpan position;
        lock (_lock)
        {
            if (_state != PlaybackState.Playing || _reader is null)
                return;
            position = _reader.CurrentTime;
        }

        PositionChanged?.Invoke(this, position);
    }

    /// <summary>
    /// Stops and disposes the current track's resources. Must be called inside _lock.
    /// </summary>
    private void CleanupCurrentTrack()
    {
        StopPositionTimer();

        if (_outputDevice is not null)
        {
            _outputDevice.PlaybackStopped -= OnPlaybackStopped;
            _outputDevice.Stop();
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        if (_reader is not null)
        {
            _reader.Dispose();
            _reader = null;
        }
    }
}
