using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using QMusic.Domain.Enums;
using QMusic.Domain.ValueObjects;
using QMusic.Infrastructure.MusicProviders.YouTube;

namespace QMusic.Infrastructure.Tests;

public class YouTubeMusicProviderTests
{
    private static YouTubeMusicProvider CreateProvider(
        string? apiKey, Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var options = Options.Create(new YouTubeOptions
        {
            Enabled = true,
            ApiKey = apiKey,
            MaxResults = 5
        });

        var mockHandler = new MockHttpMessageHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/")
        };

        var factory = new StubHttpClientFactory(httpClient);
        return new YouTubeMusicProvider(factory, options);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // --- Sample JSON payloads ---

    private const string SearchResponseJson = """
        {
            "items": [
                {
                    "id": { "videoId": "abc123" },
                    "snippet": {
                        "title": "Test Song &amp; More",
                        "channelTitle": "Test Artist",
                        "thumbnails": {
                            "high": { "url": "https://img.youtube.com/vi/abc123/hq.jpg" }
                        }
                    }
                },
                {
                    "id": { "videoId": "def456" },
                    "snippet": {
                        "title": "Another Track",
                        "channelTitle": "Another Artist",
                        "thumbnails": {
                            "medium": { "url": "https://img.youtube.com/vi/def456/mq.jpg" }
                        }
                    }
                }
            ]
        }
        """;

    private const string VideosResponseJson = """
        {
            "items": [
                {
                    "id": "abc123",
                    "snippet": {
                        "title": "Test Song &amp; More",
                        "channelTitle": "Test Artist",
                        "thumbnails": {
                            "high": { "url": "https://img.youtube.com/vi/abc123/hq.jpg" }
                        }
                    },
                    "contentDetails": { "duration": "PT3M30S" }
                },
                {
                    "id": "def456",
                    "snippet": {
                        "title": "Another Track",
                        "channelTitle": "Another Artist",
                        "thumbnails": {
                            "medium": { "url": "https://img.youtube.com/vi/def456/mq.jpg" }
                        }
                    },
                    "contentDetails": { "duration": "PT4M15S" }
                }
            ]
        }
        """;

    private const string SingleVideoResponseJson = """
        {
            "items": [
                {
                    "id": "abc123",
                    "snippet": {
                        "title": "Test Song &amp; More",
                        "channelTitle": "Test Artist",
                        "thumbnails": {
                            "high": { "url": "https://img.youtube.com/vi/abc123/hq.jpg" }
                        }
                    },
                    "contentDetails": { "duration": "PT3M30S" }
                }
            ]
        }
        """;

    [Fact]
    public void Source_ReturnsYouTubeMusic()
    {
        var provider = CreateProvider("test-key", _ => JsonResponse("{}"));
        Assert.Equal(MusicSource.YouTubeMusic, provider.Source);
    }

    [Fact]
    public async Task SearchAsync_WithValidResponse_ReturnsMappedTracks()
    {
        var provider = CreateProvider("test-key", req =>
        {
            var url = req.RequestUri!.PathAndQuery;
            if (url.Contains("search"))
                return JsonResponse(SearchResponseJson);
            if (url.Contains("videos"))
                return JsonResponse(VideosResponseJson);
            return JsonResponse("{}", HttpStatusCode.NotFound);
        });

        var results = (await provider.SearchAsync("test")).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("abc123", results[0].Id.Value);
        Assert.Equal(MusicSource.YouTubeMusic, results[0].Source);
    }

    [Fact]
    public async Task SearchAsync_DecodesHtmlEntitiesInTitle()
    {
        var provider = CreateProvider("test-key", req =>
        {
            var url = req.RequestUri!.PathAndQuery;
            if (url.Contains("search")) return JsonResponse(SearchResponseJson);
            if (url.Contains("videos")) return JsonResponse(VideosResponseJson);
            return JsonResponse("{}");
        });

        var results = (await provider.SearchAsync("test")).ToList();

        // "&amp;" in JSON should be decoded to "&"
        Assert.Equal("Test Song & More", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_ParsesIso8601Duration()
    {
        var provider = CreateProvider("test-key", req =>
        {
            var url = req.RequestUri!.PathAndQuery;
            if (url.Contains("search")) return JsonResponse(SearchResponseJson);
            if (url.Contains("videos")) return JsonResponse(VideosResponseJson);
            return JsonResponse("{}");
        });

        var results = (await provider.SearchAsync("test")).ToList();

        Assert.Equal(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(30), results[0].Duration);
        Assert.Equal(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(15), results[1].Duration);
    }

    [Fact]
    public async Task SearchAsync_ReturnsThumbnailUrl()
    {
        var provider = CreateProvider("test-key", req =>
        {
            var url = req.RequestUri!.PathAndQuery;
            if (url.Contains("search")) return JsonResponse(SearchResponseJson);
            if (url.Contains("videos")) return JsonResponse(VideosResponseJson);
            return JsonResponse("{}");
        });

        var results = (await provider.SearchAsync("test")).ToList();

        Assert.Equal("https://img.youtube.com/vi/abc123/hq.jpg", results[0].AlbumArtUrl);
        // Second track only has "medium" thumbnail
        Assert.Equal("https://img.youtube.com/vi/def456/mq.jpg", results[1].AlbumArtUrl);
    }

    [Fact]
    public async Task SearchAsync_WithNoApiKey_ReturnsEmpty()
    {
        var provider = CreateProvider(null, _ => throw new InvalidOperationException("Should not call API"));

        var results = await provider.SearchAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyApiKey_ReturnsEmpty()
    {
        var provider = CreateProvider("", _ => throw new InvalidOperationException("Should not call API"));

        var results = await provider.SearchAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_WithApiError_ReturnsEmpty()
    {
        var provider = CreateProvider("test-key", _ =>
            JsonResponse("""{"error": {"code": 403}}""", HttpStatusCode.Forbidden));

        var results = await provider.SearchAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetTrackAsync_WithValidId_ReturnsTrack()
    {
        var provider = CreateProvider("test-key", _ => JsonResponse(SingleVideoResponseJson));

        var track = await provider.GetTrackAsync(new TrackId(MusicSource.YouTubeMusic, "abc123"));

        Assert.NotNull(track);
        Assert.Equal("abc123", track.Id.Value);
        Assert.Equal("Test Song & More", track.Title);
        Assert.Equal(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(30), track.Duration);
    }

    [Fact]
    public async Task GetTrackAsync_WithEmptyResponse_ReturnsNull()
    {
        var provider = CreateProvider("test-key", _ =>
            JsonResponse("""{"items": []}"""));

        var track = await provider.GetTrackAsync(new TrackId(MusicSource.YouTubeMusic, "nonexistent"));

        Assert.Null(track);
    }

    [Fact]
    public async Task GetTrackAsync_WithNoApiKey_ReturnsNull()
    {
        var provider = CreateProvider(null, _ => throw new InvalidOperationException("Should not call API"));

        var track = await provider.GetTrackAsync(new TrackId(MusicSource.YouTubeMusic, "abc123"));

        Assert.Null(track);
    }

    [Fact]
    public async Task GetAlbumArtUrlAsync_ReturnsThumbnailUrl()
    {
        var provider = CreateProvider("test-key", _ => JsonResponse(SingleVideoResponseJson));

        var url = await provider.GetAlbumArtUrlAsync(new TrackId(MusicSource.YouTubeMusic, "abc123"));

        Assert.Equal("https://img.youtube.com/vi/abc123/hq.jpg", url);
    }

    [Fact]
    public async Task IsAvailableAsync_WithApiKey_ReturnsTrue()
    {
        var provider = CreateProvider("test-key", _ => JsonResponse("{}"));

        Assert.True(await provider.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_WithoutApiKey_ReturnsFalse()
    {
        var provider = CreateProvider(null, _ => JsonResponse("{}"));

        Assert.False(await provider.IsAvailableAsync());
    }

    // Note: GetAudioStreamAsync uses YoutubeExplode which makes real HTTP calls to YouTube.
    // Integration tests for audio streaming are not included here — they would require
    // network access and a valid video ID. The method is verified via manual end-to-end testing.

    // --- Test helpers ---

    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
