using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;

namespace QMusic.Domain.Tests;

public class TrackIdTests
{
    [Fact]
    public void TrackIds_WithSameSourceAndValue_AreEqual()
    {
        // Value objects should be compared by value, not reference.
        // Using a record gives us this automatically.
        var id1 = new TrackId(MusicSource.YouTubeMusic, "abc123");
        var id2 = new TrackId(MusicSource.YouTubeMusic, "abc123");

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void TrackIds_WithDifferentSources_AreNotEqual()
    {
        var ytId = new TrackId(MusicSource.YouTubeMusic, "abc123");
        var spotifyId = new TrackId(MusicSource.Spotify, "abc123");

        Assert.NotEqual(ytId, spotifyId);
    }

    [Fact]
    public void ToString_ReturnsSourceColonValue()
    {
        var id = new TrackId(MusicSource.YouTubeMusic, "dQw4w9WgXcQ");

        Assert.Equal("YouTubeMusic:dQw4w9WgXcQ", id.ToString());
    }
}
