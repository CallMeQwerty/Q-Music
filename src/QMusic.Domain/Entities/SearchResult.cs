using QMusic.Domain.Enums;

namespace QMusic.Domain.Entities;

/// <summary>
/// Wraps a list of tracks returned from a search, along with metadata
/// about which source produced the results.
/// </summary>
public sealed record SearchResult
{
    public required MusicSource Source { get; init; }
    public required string Query { get; init; }
    public IReadOnlyList<Track> Tracks { get; init; } = [];
}
