namespace QMusic.Application.DTOs;

public sealed record UserProfile
{
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? ProfilePictureUrl { get; init; }
}
