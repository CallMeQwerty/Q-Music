namespace QMusic.Domain.Entities;

public sealed record Playlist
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<Track> Tracks { get; init; } = [];
}
