namespace QMusic.Application.Interfaces;

/// <summary>
/// Handles persisting and retrieving app settings. The interface is intentionally simple —
/// get a value by key, set a value by key. The implementation decides *where* settings live
/// (JSON file, registry, database). For v1, it's a local JSON file.
///
/// Using generics here so callers can request typed values without manual casting.
/// </summary>
public interface ISettingsService
{
    T? GetValue<T>(string key);
    void SetValue<T>(string key, T value);
    Task SaveAsync(CancellationToken ct = default);
    Task LoadAsync(CancellationToken ct = default);
}
