using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;
using QMusic.Infrastructure.MusicProviders.YouTube;

namespace QMusic.Infrastructure.Tests;

public class YouTubeMusicProviderTests
{
    [Fact]
    public void Source_ReturnsYouTubeMusic()
    {
        var provider = new YouTubeMusicProvider();

        Assert.Equal(MusicSource.YouTubeMusic, provider.Source);
    }

    [Fact]
    public async Task SearchAsync_ReturnsStubResults()
    {
        var provider = new YouTubeMusicProvider();

        var results = await provider.SearchAsync("test query");

        Assert.NotEmpty(results);
        Assert.All(results, t => Assert.Equal(MusicSource.YouTubeMusic, t.Source));
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue()
    {
        var provider = new YouTubeMusicProvider();

        Assert.True(await provider.IsAvailableAsync());
    }

    [Fact]
    public async Task GetTrackAsync_ReturnsTrackWithCorrectId()
    {
        var provider = new YouTubeMusicProvider();
        var trackId = new TrackId(MusicSource.YouTubeMusic, "test123");

        var track = await provider.GetTrackAsync(trackId);

        Assert.NotNull(track);
        Assert.Equal(trackId, track.Id);
    }
}
