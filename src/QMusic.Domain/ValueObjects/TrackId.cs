namespace QMusic.Domain.ValueObjects;

/// <summary>
/// A source-agnostic track identifier. Wraps the provider's native ID (e.g., a YouTube video ID)
/// together with the source it came from, so we can always route back to the correct provider.
///
/// This is a value object — two TrackIds are equal if they have the same Source and Value,
/// regardless of which object instance they are. Using a record gives us this for free.
/// </summary>
public sealed record TrackId(Enums.MusicSource Source, string Value)
{
    public override string ToString() => $"{Source}:{Value}";
}
