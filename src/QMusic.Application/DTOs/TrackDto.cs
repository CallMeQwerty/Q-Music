using QMusic.Domain.Enums;

namespace QMusic.Application.DTOs;

/// <summary>
/// A flattened, UI-friendly representation of a Track. DTOs exist so the UI layer
/// doesn't depend directly on domain entities — if the domain model evolves
/// (e.g., Track gets complex nested objects), the UI can stay stable.
/// </summary>
public sealed record TrackDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public string? Album { get; init; }
    public string? DurationFormatted { get; init; }
    public string? AlbumArtUrl { get; init; }
    public MusicSource Source { get; init; }
}
