using QMusic.Domain.Entities;
using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;

namespace QMusic.Application.Interfaces;

/// <summary>
/// THE core abstraction of QMusic. Every music source (YouTube Music, Spotify, etc.)
/// implements this single interface. This is what makes the app extensible — to add a new
/// source, you implement this interface and register it in DI. Nothing else changes.
///
/// Design decisions:
/// - Returns domain entities (Track), not DTOs — the provider is responsible for mapping
///   source-specific data into our unified domain model.
/// - Every method takes CancellationToken — streaming services are inherently async and
///   can be slow or unreliable. Callers need the ability to cancel.
/// - GetAudioStreamAsync returns a Stream, not a URL — some providers may need to pipe
///   audio through a local process (like yt-dlp), so a raw stream is more flexible.
/// - IsAvailableAsync lets the UI gracefully handle providers that are down or unconfigured.
/// </summary>
public interface IMusicProvider
{
    MusicSource Source { get; }
    Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct = default);
    Task<Track?> GetTrackAsync(TrackId id, CancellationToken ct = default);
    Task<Stream> GetAudioStreamAsync(TrackId id, CancellationToken ct = default);
    Task<string?> GetAlbumArtUrlAsync(TrackId id, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
