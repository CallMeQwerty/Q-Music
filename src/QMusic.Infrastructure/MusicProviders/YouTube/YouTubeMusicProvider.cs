using System.Net;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Options;
using QMusic.Application.Interfaces;
using QMusic.Domain.Entities;
using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

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
/// Audio streaming uses YoutubeExplode to extract audio-only streams from YouTube videos.
/// YoutubeExplode resolves the stream manifest (no API quota cost) and provides direct
/// access to audio data. We prefer AAC/MP4 streams because NAudio's StreamMediaFoundationReader
/// handles AAC natively via Windows Media Foundation. Opus/WebM would require extra codecs.
/// </summary>
public sealed class YouTubeMusicProvider : IMusicProvider
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeOptions _options;
    private readonly YoutubeClient _youtube;

    public YouTubeMusicProvider(IHttpClientFactory httpClientFactory, IOptions<YouTubeOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("YouTube");
        _options = options.Value;
        _youtube = new YoutubeClient();
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

    public async Task<Stream> GetAudioStreamAsync(TrackId id, CancellationToken ct = default)
    {
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(id.Value, ct);

        // Prefer audio-only streams in MP4/AAC container — Media Foundation decodes these natively.
        // Fall back to any audio-only stream if no MP4 is available.
        var streamInfo = manifest.GetAudioOnlyStreams()
            .Where(s => s.Container == Container.Mp4)
            .GetWithHighestBitrate()
            ?? manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        if (streamInfo is null)
            throw new InvalidOperationException($"No audio streams available for video '{id.Value}'.");

        // YoutubeExplode returns a non-seekable network stream, but NAudio's
        // StreamMediaFoundationReader requires a seekable stream. Copy to MemoryStream.
        var memoryStream = new MemoryStream();
        await _youtube.Videos.Streams.CopyToAsync(streamInfo, memoryStream, cancellationToken: ct);
        memoryStream.Position = 0;

        return memoryStream;
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
