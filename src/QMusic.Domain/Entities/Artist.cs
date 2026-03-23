namespace QMusic.Domain.Entities;

public sealed record Artist
{
    public required string Name { get; init; }
    public string? ImageUrl { get; init; }
}
