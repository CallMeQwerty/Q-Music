using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Options;
using QMusic.Application.DTOs;
using QMusic.Application.Interfaces;
using QMusic.Domain.Entities;
using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;
using QMusic.Infrastructure.Auth;
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
    private readonly GoogleOAuthService _authService;

    public YouTubeMusicProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<YouTubeOptions> options,
        GoogleOAuthService authService)
    {
        _httpClient = httpClientFactory.CreateClient("YouTube");
        _options = options.Value;
        _youtube = new YoutubeClient();
        _authService = authService;
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

    private Task<T?> GetAsync<T>(string url, CancellationToken ct)
        => GetAsync<T>(url, accessToken: null, ct);

    private async Task<T?> GetAsync<T>(string url, string? accessToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (accessToken is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, ct);
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

    public async Task<IEnumerable<PlaylistDto>> GetUserPlaylistsAsync(CancellationToken ct = default)
    {
        var token = await _authService.GetAccessTokenAsync(ct);
        if (token is null)
            return [];

        var playlists = new List<PlaylistDto>();
        string? pageToken = null;

        do
        {
            var url = $"playlists?part=snippet,contentDetails&mine=true&maxResults=50&key={_options.ApiKey}" +
                      (pageToken is not null ? $"&pageToken={pageToken}" : "");

            var response = await GetAsync<YouTubePlaylistListResponse>(url, token, ct);
            if (response?.Items is null)
                break;

            playlists.AddRange(response.Items.Select(item =>
            {
                var thumb = item.Snippet.Thumbnails.Medium
                            ?? item.Snippet.Thumbnails.Default;

                return new PlaylistDto
                {
                    Id = item.Id,
                    Title = WebUtility.HtmlDecode(item.Snippet.Title),
                    ThumbnailUrl = thumb?.Url,
                    PublishedAt = item.Snippet.PublishedAt,
                    TrackCount = item.ContentDetails.ItemCount
                };
            }));

            pageToken = response.NextPageToken;
        } while (pageToken is not null);

        // Prepend the "Liked Videos" system playlist (ID: LL) — it doesn't appear in playlists.list
        var likedUrl = $"playlists?part=snippet,contentDetails&id=LL&key={_options.ApiKey}";
        var likedResponse = await GetAsync<YouTubePlaylistListResponse>(likedUrl, token, ct);
        if (likedResponse?.Items is { Count: > 0 })
        {
            var liked = likedResponse.Items[0];
            playlists.Insert(0, new PlaylistDto
            {
                Id = "LL",
                Title = "\u2764 Liked Videos",
                ThumbnailUrl = null,
                PublishedAt = liked.Snippet.PublishedAt,
                TrackCount = liked.ContentDetails.ItemCount
            });
        }

        return playlists;
    }

    public async Task<IEnumerable<Track>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var token = await _authService.GetAccessTokenAsync(ct);
        if (token is null)
            return [];

        // Step 1: Get all video IDs from the playlist
        var videoIds = new List<string>();
        string? pageToken = null;

        do
        {
            var url = $"playlistItems?part=snippet&playlistId={Uri.EscapeDataString(playlistId)}" +
                      $"&maxResults=50&key={_options.ApiKey}" +
                      (pageToken is not null ? $"&pageToken={pageToken}" : "");

            var response = await GetAsync<YouTubePlaylistItemListResponse>(url, token, ct);
            if (response?.Items is null)
                break;

            videoIds.AddRange(response.Items
                .Select(i => i.Snippet.ResourceId.VideoId)
                .Where(id => !string.IsNullOrEmpty(id)));

            pageToken = response.NextPageToken;
        } while (pageToken is not null);

        if (videoIds.Count == 0)
            return [];

        // Step 2: Batch fetch video details (durations, full metadata) — 50 per call
        // Filter to Music category (categoryId: 10) to exclude non-music content
        var tracks = new List<Track>();
        foreach (var batch in videoIds.Chunk(50))
        {
            var videosUrl = $"videos?part=contentDetails,snippet&id={string.Join(',', batch)}&key={_options.ApiKey}";
            var videosResponse = await GetAsync<YouTubeVideoListResponse>(videosUrl, ct);
            if (videosResponse?.Items is not null)
                tracks.AddRange(videosResponse.Items
                    .Where(v => v.Snippet.CategoryId == "10")
                    .Select(MapToTrack));
        }

        return tracks;
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

        // Use mqdefault (320x180, 16:9 no letterbox) instead of hqdefault (480x360, often letterboxed)
        var albumArtUrl = $"https://i.ytimg.com/vi/{item.Id}/mqdefault.jpg";

        return new Track
        {
            Id = new TrackId(MusicSource.YouTubeMusic, item.Id),
            Title = WebUtility.HtmlDecode(item.Snippet.Title),
            Artist = WebUtility.HtmlDecode(item.Snippet.ChannelTitle),
            Album = null,
            Duration = duration,
            AlbumArtUrl = albumArtUrl
        };
    }
}
