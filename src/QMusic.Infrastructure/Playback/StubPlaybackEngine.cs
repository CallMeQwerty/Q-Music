using QMusic.Application.Interfaces;

namespace QMusic.Infrastructure.Playback;

/// <summary>
/// Stub playback engine for scaffolding. Tracks state transitions without producing audio.
/// Will be replaced by NAudioPlaybackEngine in a future session.
/// </summary>
public sealed class StubPlaybackEngine : IPlaybackEngine
{
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;
    public TimeSpan CurrentPosition { get; private set; }
    public TimeSpan TotalDuration { get; private set; }
    public int Volume { get; set; } = 80;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;

    public Task PlayAsync(Stream audioStream, CancellationToken ct = default)
    {
        TotalDuration = TimeSpan.FromMinutes(3.5); // Fake duration
        CurrentPosition = TimeSpan.Zero;
        State = PlaybackState.Playing;
        StateChanged?.Invoke(this, State);
        return Task.CompletedTask;
    }

    public void Pause()
    {
        State = PlaybackState.Paused;
        StateChanged?.Invoke(this, State);
    }

    public void Resume()
    {
        State = PlaybackState.Playing;
        StateChanged?.Invoke(this, State);
    }

    public void Stop()
    {
        State = PlaybackState.Stopped;
        CurrentPosition = TimeSpan.Zero;
        StateChanged?.Invoke(this, State);
    }

    public void Seek(TimeSpan position)
    {
        CurrentPosition = position;
        PositionChanged?.Invoke(this, position);
    }

    public void Dispose()
    {
        // Nothing to dispose in the stub
    }
}
