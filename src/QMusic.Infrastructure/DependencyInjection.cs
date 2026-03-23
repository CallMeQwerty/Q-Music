using Microsoft.Extensions.DependencyInjection;
using QMusic.Application.Interfaces;
using QMusic.Application.Services;
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
/// The host (Desktop) just chains: services.AddInfrastructure().
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Music providers — registered as the interface so DI can resolve IEnumerable<IMusicProvider>
        services.AddSingleton<IMusicProvider, YouTubeMusicProvider>();
        services.AddSingleton<IMusicProvider, SpotifyMusicProvider>();

        // Playback engine — singleton because there's one audio output device
        services.AddSingleton<IPlaybackEngine, StubPlaybackEngine>();

        // Settings — singleton with a well-known file path
        services.AddSingleton<ISettingsService>(sp =>
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QMusic");
            Directory.CreateDirectory(appData);
            return new JsonSettingsService(Path.Combine(appData, "settings.json"));
        });

        // Application services
        services.AddSingleton<SourceManagerService>();
        services.AddSingleton<MusicPlayerService>();

        return services;
    }
}
