using QMusic.Application.Interfaces;
using QMusic.Domain.Enums;

namespace QMusic.Application.Services;

/// <summary>
/// Manages which music provider is currently active and provides access to all registered providers.
/// This service is the single place that knows about all available providers — UI components
/// ask it "give me the current provider" rather than resolving providers themselves.
///
/// Why a dedicated service instead of just injecting IEnumerable&lt;IMusicProvider&gt; everywhere?
/// Because "which source is active" is app-level state that needs to be shared across components.
/// </summary>
public sealed class SourceManagerService
{
    private readonly Dictionary<MusicSource, IMusicProvider> _providers;

    public SourceManagerService(IEnumerable<IMusicProvider> providers, MusicSource defaultSource = MusicSource.YouTubeMusic)
    {
        _providers = providers.ToDictionary(p => p.Source);
        ActiveSource = _providers.ContainsKey(defaultSource)
            ? defaultSource
            : _providers.Keys.FirstOrDefault();
    }

    public MusicSource ActiveSource { get; private set; }

    public IMusicProvider? GetActiveProvider() =>
        _providers.GetValueOrDefault(ActiveSource);

    public IMusicProvider? GetProvider(MusicSource source) =>
        _providers.GetValueOrDefault(source);

    public IReadOnlyCollection<MusicSource> AvailableSources => _providers.Keys;

    public void SetActiveSource(MusicSource source)
    {
        if (_providers.ContainsKey(source))
            ActiveSource = source;
    }
}
