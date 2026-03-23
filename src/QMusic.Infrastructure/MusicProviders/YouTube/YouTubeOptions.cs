namespace QMusic.Infrastructure.MusicProviders.YouTube;

/// <summary>
/// Strongly-typed configuration for the YouTube Music provider.
/// Bound from appsettings via the Options pattern at the path "QMusic:Providers:YouTubeMusic".
///
/// Why Options pattern instead of injecting IConfiguration directly?
/// The provider shouldn't know about the configuration system — it just needs its own settings.
/// This also makes testing easier: pass Options.Create(new YouTubeOptions { ... }).
/// </summary>
public sealed class YouTubeOptions
{
    public const string SectionPath = "QMusic:Providers:YouTubeMusic";

    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public int MaxResults { get; set; } = 20;
}
