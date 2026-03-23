using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using QMusic.Application.Interfaces;
using QMusic.Infrastructure.Playback;
using PlaybackState = QMusic.Application.Interfaces.PlaybackState;

namespace QMusic.Infrastructure.Tests;

/// <summary>
/// Integration tests for NAudioPlaybackEngine.
/// These require Windows Media Foundation and an audio output device (or at least
/// the WaveOut subsystem). Tagged as Integration so they can be filtered in CI.
/// </summary>
[Trait("Category", "Integration")]
public class NAudioPlaybackEngineTests
{
    /// <summary>
    /// Generates a short WAV audio stream in memory using NAudio's SignalGenerator.
    /// No test fixture files needed.
    /// </summary>
    private static MemoryStream CreateTestAudioStream(double durationSeconds = 1.0)
    {
        var ms = new MemoryStream();
        var signal = new SignalGenerator(44100, 1)
        {
            Frequency = 440,
            Type = SignalGeneratorType.Sin
        };
        var trimmed = signal.Take(TimeSpan.FromSeconds(durationSeconds));
        WaveFileWriter.WriteWavFileToStream(ms, trimmed.ToWaveProvider16());
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task PlayAsync_WithValidStream_SetsStatePlaying()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream = CreateTestAudioStream();

        await engine.PlayAsync(stream);

        Assert.Equal(PlaybackState.Playing, engine.State);
    }

    [Fact]
    public async Task PlayAsync_WithValidStream_SetsDuration()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream = CreateTestAudioStream();

        await engine.PlayAsync(stream);

        Assert.True(engine.TotalDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task Pause_SetsStatePaused()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream = CreateTestAudioStream();
        await engine.PlayAsync(stream);

        engine.Pause();

        Assert.Equal(PlaybackState.Paused, engine.State);
    }

    [Fact]
    public async Task Resume_AfterPause_SetsStatePlaying()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream = CreateTestAudioStream();
        await engine.PlayAsync(stream);
        engine.Pause();

        engine.Resume();

        Assert.Equal(PlaybackState.Playing, engine.State);
    }

    [Fact]
    public async Task Stop_SetsStateStopped()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream = CreateTestAudioStream();
        await engine.PlayAsync(stream);

        engine.Stop();

        Assert.Equal(PlaybackState.Stopped, engine.State);
    }

    [Fact]
    public void Volume_ClampsBetween0And100()
    {
        using var engine = new NAudioPlaybackEngine();

        engine.Volume = 150;
        Assert.Equal(100, engine.Volume);

        engine.Volume = -10;
        Assert.Equal(0, engine.Volume);

        engine.Volume = 50;
        Assert.Equal(50, engine.Volume);
    }

    [Fact]
    public async Task StateChanged_FiresOnPlayPauseStop()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream = CreateTestAudioStream();
        var states = new List<PlaybackState>();
        engine.StateChanged += (_, s) => states.Add(s);

        await engine.PlayAsync(stream);
        engine.Pause();
        engine.Stop();

        Assert.Contains(PlaybackState.Playing, states);
        Assert.Contains(PlaybackState.Paused, states);
        Assert.Contains(PlaybackState.Stopped, states);
    }

    [Fact]
    public async Task PositionChanged_FiresDuringPlayback()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream = CreateTestAudioStream(2.0);
        var positionReceived = new TaskCompletionSource<TimeSpan>();
        engine.PositionChanged += (_, pos) =>
        {
            if (pos > TimeSpan.Zero)
                positionReceived.TrySetResult(pos);
        };

        await engine.PlayAsync(stream);

        // Wait up to 2 seconds for a non-zero position event
        var completed = await Task.WhenAny(positionReceived.Task, Task.Delay(2000));
        engine.Stop();

        Assert.Equal(positionReceived.Task, completed);
        var receivedPosition = await positionReceived.Task;
        Assert.True(receivedPosition > TimeSpan.Zero);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var engine = new NAudioPlaybackEngine();

        engine.Dispose();
        engine.Dispose(); // Should not throw
    }

    [Fact]
    public async Task PlayAsync_ReplacesCurrentTrack()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream1 = CreateTestAudioStream();
        using var stream2 = CreateTestAudioStream();

        await engine.PlayAsync(stream1);
        await engine.PlayAsync(stream2); // Should not throw

        Assert.Equal(PlaybackState.Playing, engine.State);
    }

    [Fact]
    public async Task Seek_OnSeekableStream_UpdatesPosition()
    {
        using var engine = new NAudioPlaybackEngine();
        using var stream = CreateTestAudioStream(2.0);
        await engine.PlayAsync(stream);

        var target = TimeSpan.FromSeconds(0.5);
        engine.Seek(target);

        // Position should be approximately at the target (within 100ms tolerance)
        var pos = engine.CurrentPosition;
        Assert.True(pos >= target - TimeSpan.FromMilliseconds(100),
            $"Expected position >= {target - TimeSpan.FromMilliseconds(100)}, got {pos}");
    }
}
