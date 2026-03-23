namespace QMusic.Application.Interfaces;

/// <summary>
/// Abstracts the audio playback hardware. The app tells this interface "play this stream"
/// and it handles the actual audio output. This separation means:
/// - We can swap NAudio for another library without touching business logic.
/// - We can create a mock for testing without needing audio hardware.
/// - The playback engine doesn't know or care where the audio stream came from.
/// </summary>
public interface IPlaybackEngine : IDisposable
{
    PlaybackState State { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan TotalDuration { get; }
    int Volume { get; set; }

    Task PlayAsync(Stream audioStream, CancellationToken ct = default);
    void Pause();
    void Resume();
    void Stop();
    void Seek(TimeSpan position);

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
}

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}
