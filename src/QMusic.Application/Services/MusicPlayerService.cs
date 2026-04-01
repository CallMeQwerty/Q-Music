using QMusic.Application.DTOs;
using QMusic.Application.Interfaces;
using QMusic.Domain.Entities;
using QMusic.Domain.ValueObjects;

namespace QMusic.Application.Services;

/// <summary>
/// The main orchestrator — coordinates between the active music provider and the playback engine.
/// UI components talk to this service instead of directly using providers or the playback engine.
///
/// This is Application-layer logic because it doesn't implement any infrastructure concerns
/// (no HTTP calls, no audio hardware) — it just coordinates the abstractions.
/// </summary>
public sealed class MusicPlayerService
{
    private readonly SourceManagerService _sourceManager;
    private readonly IPlaybackEngine _playbackEngine;
    private Track? _currentTrack;

    public MusicPlayerService(SourceManagerService sourceManager, IPlaybackEngine playbackEngine)
    {
        _sourceManager = sourceManager;
        _playbackEngine = playbackEngine;
    }

    public Track? CurrentTrack => _currentTrack;
    public PlaybackState State => _playbackEngine.State;
    public TimeSpan CurrentPosition => _playbackEngine.CurrentPosition;
    public TimeSpan TotalDuration => _playbackEngine.TotalDuration;
    public int Volume
    {
        get => _playbackEngine.Volume;
        set => _playbackEngine.Volume = value;
    }

    public async Task<IEnumerable<TrackDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var provider = _sourceManager.GetActiveProvider();
        if (provider is null)
            return [];

        var tracks = await provider.SearchAsync(query, ct);
        return tracks.Select(MapToDto);
    }

    public async Task PlayTrackAsync(TrackId trackId, CancellationToken ct = default)
    {
        var provider = _sourceManager.GetProvider(trackId.Source);
        if (provider is null) return;

        _currentTrack = await provider.GetTrackAsync(trackId, ct);
        var stream = await provider.GetAudioStreamAsync(trackId, ct);
        await _playbackEngine.PlayAsync(stream, ct);
    }

    public async Task<IEnumerable<PlaylistDto>> GetUserPlaylistsAsync(CancellationToken ct = default)
    {
        var provider = _sourceManager.GetActiveProvider();
        if (provider is null)
            return [];

        return await provider.GetUserPlaylistsAsync(ct);
    }

    public async Task<IEnumerable<TrackDto>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var provider = _sourceManager.GetActiveProvider();
        if (provider is null)
            return [];

        var tracks = await provider.GetPlaylistTracksAsync(playlistId, ct);
        return tracks.Select(MapToDto);
    }

    public void Pause() => _playbackEngine.Pause();
    public void Resume() => _playbackEngine.Resume();
    public void Stop() => _playbackEngine.Stop();
    public void Seek(TimeSpan position) => _playbackEngine.Seek(position);

    /// <summary>
    /// Relays engine events so UI components subscribe through the service
    /// (the service is the single touchpoint for the UI, not the engine directly).
    /// </summary>
    public event EventHandler<PlaybackState>? StateChanged
    {
        add => _playbackEngine.StateChanged += value;
        remove => _playbackEngine.StateChanged -= value;
    }

    public event EventHandler<TimeSpan>? PositionChanged
    {
        add => _playbackEngine.PositionChanged += value;
        remove => _playbackEngine.PositionChanged -= value;
    }

    private static TrackDto MapToDto(Track track) => new()
    {
        Id = track.Id.ToString(),
        Title = track.Title,
        Artist = track.Artist,
        Album = track.Album,
        DurationFormatted = track.Duration.ToString(@"m\:ss"),
        AlbumArtUrl = track.AlbumArtUrl,
        Source = track.Source
    };
}
