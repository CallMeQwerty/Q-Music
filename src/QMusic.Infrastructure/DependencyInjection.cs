using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QMusic.Application.Interfaces;
using QMusic.Application.Services;
using QMusic.Infrastructure.Auth;
using QMusic.Infrastructure.MusicProviders.Spotify;
using QMusic.Infrastructure.MusicProviders.YouTube;
using QMusic.Infrastructure.Playback;
using QMusic.Infrastructure.Settings;

namespace QMusic.Infrastructure;

/// <summary>
/// Centralizes all Infrastructure service registrations. The Desktop project calls
/// this single method instead of knowing about every concrete type.
///
/// Why extension methods on IServiceCollection? It's the standard .NET pattern —
/// each layer exposes an "AddXxx" method that registers its own services.
/// The host (Desktop) just chains: services.AddInfrastructure(configuration).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // YouTube configuration — binds "QMusic:Providers:YouTubeMusic" section to YouTubeOptions
        services.Configure<YouTubeOptions>(
            configuration.GetSection(YouTubeOptions.SectionPath));

        // Named HttpClient for YouTube API — IHttpClientFactory manages handler lifetimes
        services.AddHttpClient("YouTube", client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
        });

        // Music providers — registered as the interface so DI can resolve IEnumerable<IMusicProvider>
        services.AddSingleton<IMusicProvider, YouTubeMusicProvider>();
        services.AddSingleton<IMusicProvider, SpotifyMusicProvider>();

        // Playback engine — singleton because there's one audio output device
        services.AddSingleton<IPlaybackEngine, NAudioPlaybackEngine>();

        // Settings — singleton with a well-known file path
        services.AddSingleton<ISettingsService>(sp =>
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QMusic");
            Directory.CreateDirectory(appData);
            return new JsonSettingsService(Path.Combine(appData, "settings.json"));
        });

        // Authentication — Google OAuth for YouTube user-level access (playlists, library)
        // Register concrete type so YouTubeMusicProvider can inject it directly,
        // and forward the interface so UI components use the abstraction.
        services.AddSingleton<GoogleOAuthService>();
        services.AddSingleton<IAuthenticationService>(sp => sp.GetRequiredService<GoogleOAuthService>());

        // Application services — SourceManagerService gets the configured default source
        services.AddSingleton(sp =>
        {
            var providers = sp.GetServices<IMusicProvider>();
            var defaultSourceStr = configuration["QMusic:DefaultSource"] ?? "YouTubeMusic";
            Enum.TryParse<QMusic.Domain.Enums.MusicSource>(defaultSourceStr, out var defaultSource);
            return new SourceManagerService(providers, defaultSource);
        });
        services.AddSingleton<MusicPlayerService>();

        return services;
    }
}
