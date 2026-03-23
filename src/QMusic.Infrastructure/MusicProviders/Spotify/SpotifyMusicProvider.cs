using QMusic.Application.Interfaces;
using QMusic.Domain.Entities;
using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;

namespace QMusic.Infrastructure.MusicProviders.Spotify;

/// <summary>
/// Placeholder for Spotify integration (v2).
/// Will use SpotifyAPI.Web (v7.4.2) — requires Spotify Premium.
/// Currently returns unavailable so the UI knows not to offer it.
/// </summary>
public sealed class SpotifyMusicProvider : IMusicProvider
{
    public MusicSource Source => MusicSource.Spotify;

    public Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IEnumerable<Track>>([]);

    public Task<Track?> GetTrackAsync(TrackId id, CancellationToken ct = default)
        => Task.FromResult<Track?>(null);

    public Task<Stream> GetAudioStreamAsync(TrackId id, CancellationToken ct = default)
        => throw new NotSupportedException("Spotify provider is not yet implemented.");

    public Task<string?> GetAlbumArtUrlAsync(TrackId id, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(false);
}
