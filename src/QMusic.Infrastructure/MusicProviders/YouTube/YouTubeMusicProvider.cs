using System.Net;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Options;
using QMusic.Application.Interfaces;
using QMusic.Domain.Entities;
using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;

namespace QMusic.Infrastructure.MusicProviders.YouTube;

/// <summary>
/// YouTube Music provider using YouTube Data API v3.
///
/// Search flow (two API calls per search):
/// 1. search.list — finds videos matching the query, filtered to Music category.
///    Returns video IDs, titles, channel names, thumbnails. Does NOT return duration.
/// 2. videos.list — fetches duration and full metadata for the found video IDs.
///    Batches up to 50 IDs in one call (1 quota unit regardless of batch size).
///
/// Quota: search costs 100 units, videos.list costs 1 unit. Free tier = 10,000 units/day.
/// So roughly ~99 searches per day with this two-call pattern.
///
/// Audio streaming is not yet implemented — that will use a separate approach (YoutubeExplode or yt-dlp).
/// </summary>
public sealed class YouTubeMusicProvider : IMusicProvider
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeOptions _options;

    public YouTubeMusicProvider(IHttpClientFactory httpClientFactory, IOptions<YouTubeOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("YouTube");
        _options = options.Value;
    }

    public MusicSource Source => MusicSource.YouTubeMusic;

    public async Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return [];

        // Step 1: Search for videos in the Music category
        var searchUrl = $"search?part=snippet&type=video&videoCategoryId=10" +
                        $"&q={Uri.EscapeDataString(query)}" +
                        $"&maxResults={_options.MaxResults}" +
                        $"&key={_options.ApiKey}";

        var searchResponse = await GetAsync<YouTubeSearchResponse>(searchUrl, ct);
        if (searchResponse?.Items is not { Count: > 0 })
            return [];

        var videoIds = searchResponse.Items
            .Where(i => !string.IsNullOrEmpty(i.Id.VideoId))
            .Select(i => i.Id.VideoId)
            .ToList();

        if (videoIds.Count == 0)
            return [];

        // Step 2: Get durations and full metadata via videos.list
        var videosUrl = $"videos?part=contentDetails,snippet" +
                        $"&id={string.Join(',', videoIds)}" +
                        $"&key={_options.ApiKey}";

        var videosResponse = await GetAsync<YouTubeVideoListResponse>(videosUrl, ct);
        if (videosResponse?.Items is null)
            return [];

        return videosResponse.Items.Select(MapToTrack).ToList();
    }

    public async Task<Track?> GetTrackAsync(TrackId id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return null;

        var url = $"videos?part=contentDetails,snippet&id={id.Value}&key={_options.ApiKey}";
        var response = await GetAsync<YouTubeVideoListResponse>(url, ct);

        return response?.Items is { Count: > 0 }
            ? MapToTrack(response.Items[0])
            : null;
    }

    public Task<Stream> GetAudioStreamAsync(TrackId id, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Audio streaming is not yet implemented. This will be added in a future task.");
    }

    public async Task<string?> GetAlbumArtUrlAsync(TrackId id, CancellationToken ct = default)
    {
        var track = await GetTrackAsync(id, ct);
        return track?.AlbumArtUrl;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(_options.ApiKey));
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return default;

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct);
        }
        catch (HttpRequestException)
        {
            return default;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static Track MapToTrack(YouTubeVideoItem item)
    {
        var duration = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(item.ContentDetails.Duration))
        {
            try
            {
                duration = XmlConvert.ToTimeSpan(item.ContentDetails.Duration);
            }
            catch (FormatException)
            {
                // Malformed duration string — leave as zero
            }
        }

        var thumbnail = item.Snippet.Thumbnails.High
                        ?? item.Snippet.Thumbnails.Medium
                        ?? item.Snippet.Thumbnails.Default;

        return new Track
        {
            Id = new TrackId(MusicSource.YouTubeMusic, item.Id),
            Title = WebUtility.HtmlDecode(item.Snippet.Title),
            Artist = WebUtility.HtmlDecode(item.Snippet.ChannelTitle),
            Album = null,
            Duration = duration,
            AlbumArtUrl = thumbnail?.Url
        };
    }
}
