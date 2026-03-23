using QMusic.Application.Interfaces;
using QMusic.Domain.Entities;
using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;

namespace QMusic.Infrastructure.MusicProviders.YouTube;

/// <summary>
/// Stub implementation for YouTube Music. In a future session, this will use yt-dlp
/// for audio extraction and possibly the YouTube Data API for search.
/// For now, it returns dummy data so the architecture is testable end-to-end.
/// </summary>
public sealed class YouTubeMusicProvider : IMusicProvider
{
    public MusicSource Source => MusicSource.YouTubeMusic;

    public Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct = default)
    {
        // Stub: return a few fake tracks so the UI has something to show
        var tracks = new[]
        {
            new Track
            {
                Id = new TrackId(MusicSource.YouTubeMusic, "dQw4w9WgXcQ"),
                Title = $"Search result for: {query}",
                Artist = "Stub Artist",
                Album = "Stub Album",
                Duration = TimeSpan.FromMinutes(3.5),
                AlbumArtUrl = null
            }
        };
        return Task.FromResult<IEnumerable<Track>>(tracks);
    }

    public Task<Track?> GetTrackAsync(TrackId id, CancellationToken ct = default)
    {
        var track = new Track
        {
            Id = id,
            Title = "Stub Track",
            Artist = "Stub Artist",
            Album = "Stub Album",
            Duration = TimeSpan.FromMinutes(3.5),
            AlbumArtUrl = null
        };
        return Task.FromResult<Track?>(track);
    }

    public Task<Stream> GetAudioStreamAsync(TrackId id, CancellationToken ct = default)
    {
        // Return an empty stream — real implementation will pipe audio from yt-dlp
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public Task<string?> GetAlbumArtUrlAsync(TrackId id, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }
}
