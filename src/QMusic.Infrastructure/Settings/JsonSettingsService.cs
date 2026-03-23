using System.Text.Json;
using System.Text.Json.Nodes;
using QMusic.Application.Interfaces;

namespace QMusic.Infrastructure.Settings;

/// <summary>
/// Persists settings as a JSON file on disk. Uses System.Text.Json's JsonNode API
/// for dynamic key-value access — no need to define a rigid settings class upfront.
/// Settings are kept in memory as a mutable JsonObject and flushed to disk on SaveAsync.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly string _filePath;
    private JsonObject _settings = new();

    public JsonSettingsService(string filePath)
    {
        _filePath = filePath;
    }

    public T? GetValue<T>(string key)
    {
        if (_settings.TryGetPropertyValue(key, out var node) && node is not null)
            return node.Deserialize<T>();
        return default;
    }

    public void SetValue<T>(string key, T value)
    {
        _settings[key] = JsonSerializer.SerializeToNode(value);
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var json = _settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return;

        var json = await File.ReadAllTextAsync(_filePath, ct);
        _settings = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
    }
}
