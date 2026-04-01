namespace QMusic.Application.DTOs;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextPageToken);
