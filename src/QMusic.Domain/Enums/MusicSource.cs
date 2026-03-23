namespace QMusic.Domain.Enums;

/// <summary>
/// Identifies which streaming service a track originates from.
/// Each value maps to exactly one IMusicProvider implementation.
/// </summary>
public enum MusicSource
{
    YouTubeMusic,
    Spotify
}
