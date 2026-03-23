namespace QMusic.Domain.Entities;

public sealed record Album
{
    public required string Name { get; init; }
    public required string Artist { get; init; }
    public string? CoverArtUrl { get; init; }
    public int? Year { get; init; }
    public IReadOnlyList<Track> Tracks { get; init; } = [];
}
