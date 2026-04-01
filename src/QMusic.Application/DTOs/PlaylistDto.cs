namespace QMusic.Application.DTOs;

public sealed record PlaylistDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? ThumbnailUrl { get; init; }
    public int TrackCount { get; init; }
    public DateTime? PublishedAt { get; init; }
}
