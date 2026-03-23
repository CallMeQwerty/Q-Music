using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;

namespace QMusic.Domain.Entities;

/// <summary>
/// The core entity of the app — represents a single playable track.
/// Uses a record for immutability: once we get track metadata from a provider,
/// it doesn't change during the lifetime of the object.
/// </summary>
public sealed record Track
{
    public required TrackId Id { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public string? Album { get; init; }
    public TimeSpan Duration { get; init; }
    public string? AlbumArtUrl { get; init; }
    public MusicSource Source => Id.Source;
}
