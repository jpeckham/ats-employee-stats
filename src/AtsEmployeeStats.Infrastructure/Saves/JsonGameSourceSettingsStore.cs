using System.Text.Json;
using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class JsonGameSourceSettingsStore(string path) : IGameSourceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static JsonGameSourceSettingsStore CreateDefault()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsEmployeeStats");
        return new JsonGameSourceSettingsStore(Path.Combine(dataDirectory, "game-sources.json"));
    }

    public async Task<GameSourceSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return new GameSourceSettings([]);

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<GameSourceSettings>(
            stream,
            JsonOptions,
            cancellationToken) ?? new GameSourceSettings([]);
    }

    public async Task SaveAsync(GameSourceSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }
}
