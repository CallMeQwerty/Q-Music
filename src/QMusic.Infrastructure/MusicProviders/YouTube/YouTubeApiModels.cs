using System.Text.Json.Serialization;

namespace QMusic.Infrastructure.MusicProviders.YouTube;

/// <summary>
/// Internal DTOs for deserializing YouTube Data API v3 JSON responses.
/// These are infrastructure implementation details — not exposed outside this layer.
/// </summary>

// ===== Search endpoint response =====

internal sealed class YouTubeSearchResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeSearchItem> Items { get; set; } = [];
}

internal sealed class YouTubeSearchItem
{
    [JsonPropertyName("id")]
    public YouTubeSearchItemId Id { get; set; } = new();

    [JsonPropertyName("snippet")]
    public YouTubeSnippet Snippet { get; set; } = new();
}

internal sealed class YouTubeSearchItemId
{
    [JsonPropertyName("videoId")]
    public string VideoId { get; set; } = string.Empty;
}

// ===== Videos.list endpoint response =====

internal sealed class YouTubeVideoListResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeVideoItem> Items { get; set; } = [];
}

internal sealed class YouTubeVideoItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("snippet")]
    public YouTubeSnippet Snippet { get; set; } = new();

    [JsonPropertyName("contentDetails")]
    public YouTubeContentDetails ContentDetails { get; set; } = new();
}

internal sealed class YouTubeContentDetails
{
    /// <summary>
    /// ISO 8601 duration string, e.g. "PT4M30S" for 4 minutes 30 seconds.
    /// Parsed via XmlConvert.ToTimeSpan().
    /// </summary>
    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;
}

// ===== Shared types =====

internal sealed class YouTubeSnippet
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("channelTitle")]
    public string ChannelTitle { get; set; } = string.Empty;

    [JsonPropertyName("thumbnails")]
    public YouTubeThumbnails Thumbnails { get; set; } = new();
}

internal sealed class YouTubeThumbnails
{
    [JsonPropertyName("high")]
    public YouTubeThumbnail? High { get; set; }

    [JsonPropertyName("medium")]
    public YouTubeThumbnail? Medium { get; set; }

    [JsonPropertyName("default")]
    public YouTubeThumbnail? Default { get; set; }
}

internal sealed class YouTubeThumbnail
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

// ===== Playlists.list endpoint response =====

internal sealed class YouTubePlaylistListResponse
{
    [JsonPropertyName("items")]
    public List<YouTubePlaylistItem> Items { get; set; } = [];

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

internal sealed class YouTubePlaylistItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("snippet")]
    public YouTubeSnippet Snippet { get; set; } = new();

    [JsonPropertyName("contentDetails")]
    public YouTubePlaylistContentDetails ContentDetails { get; set; } = new();
}

internal sealed class YouTubePlaylistContentDetails
{
    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }
}

// ===== PlaylistItems.list endpoint response =====

internal sealed class YouTubePlaylistItemListResponse
{
    [JsonPropertyName("items")]
    public List<YouTubePlaylistItemEntry> Items { get; set; } = [];

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

internal sealed class YouTubePlaylistItemEntry
{
    [JsonPropertyName("snippet")]
    public YouTubePlaylistItemSnippet Snippet { get; set; } = new();
}

internal sealed class YouTubePlaylistItemSnippet
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public YouTubeResourceId ResourceId { get; set; } = new();
}

internal sealed class YouTubeResourceId
{
    [JsonPropertyName("videoId")]
    public string VideoId { get; set; } = string.Empty;
}
