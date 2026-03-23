using QMusic.Application.Interfaces;
using QMusic.Application.Services;
using QMusic.Domain.Entities;
using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;

namespace QMusic.Application.Tests;

public class SourceManagerServiceTests
{
    [Fact]
    public void AvailableSources_ReturnsAllRegisteredProviders()
    {
        var providers = new IMusicProvider[]
        {
            new FakeProvider(MusicSource.YouTubeMusic),
            new FakeProvider(MusicSource.Spotify)
        };
        var manager = new SourceManagerService(providers);

        Assert.Equal(2, manager.AvailableSources.Count);
        Assert.Contains(MusicSource.YouTubeMusic, manager.AvailableSources);
        Assert.Contains(MusicSource.Spotify, manager.AvailableSources);
    }

    [Fact]
    public void SetActiveSource_ChangesActiveProvider()
    {
        var providers = new IMusicProvider[]
        {
            new FakeProvider(MusicSource.YouTubeMusic),
            new FakeProvider(MusicSource.Spotify)
        };
        var manager = new SourceManagerService(providers);

        manager.SetActiveSource(MusicSource.Spotify);

        Assert.Equal(MusicSource.Spotify, manager.ActiveSource);
        Assert.Equal(MusicSource.Spotify, manager.GetActiveProvider()!.Source);
    }

    [Fact]
    public void SetActiveSource_WithInvalidSource_DoesNotChange()
    {
        var providers = new IMusicProvider[] { new FakeProvider(MusicSource.YouTubeMusic) };
        var manager = new SourceManagerService(providers);
        var original = manager.ActiveSource;

        manager.SetActiveSource(MusicSource.Spotify); // Not registered

        Assert.Equal(original, manager.ActiveSource);
    }

    /// <summary>
    /// Minimal fake for testing — this is exactly why we use interfaces.
    /// No real HTTP calls, no audio hardware, just pure logic testing.
    /// </summary>
    private sealed class FakeProvider(MusicSource source) : IMusicProvider
    {
        public MusicSource Source => source;
        public Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct) => Task.FromResult<IEnumerable<Track>>([]);
        public Task<Track?> GetTrackAsync(TrackId id, CancellationToken ct) => Task.FromResult<Track?>(null);
        public Task<Stream> GetAudioStreamAsync(TrackId id, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream());
        public Task<string?> GetAlbumArtUrlAsync(TrackId id, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);
    }
}
