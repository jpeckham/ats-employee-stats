using System.Text.Json;
using AtsEmployeeStats.Application.Saves;
using Microsoft.Data.Sqlite;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class SqliteGameSourceSettingsStore(string databasePath) : IGameSourceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static SqliteGameSourceSettingsStore CreateDefault()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsEmployeeStats");
        return new SqliteGameSourceSettingsStore(Path.Combine(dataDirectory, "ats-employee-stats.db"));
    }

    public async Task<GameSourceSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        var completed = false;
        await using (var metadata = connection.CreateCommand())
        {
            metadata.CommandText = "select wizard_completed from game_source_settings_metadata where id = 1";
            var value = await metadata.ExecuteScalarAsync(cancellationToken);
            completed = value is long number && number != 0;
        }

        var sources = new List<GameSourceConfiguration>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select game, enabled, install_path, profile_path, save_path, save_paths_json
            from game_source_settings
            order by game
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var game = Enum.Parse<GameType>(reader.GetString(0));
            var savePaths = DeserializeSavePaths(ReadNullableString(reader, 5));
            sources.Add(new GameSourceConfiguration(
                game,
                reader.GetInt64(1) != 0,
                ReadNullableString(reader, 2),
                ReadNullableString(reader, 3),
                ReadNullableString(reader, 4),
                savePaths));
        }

        return new GameSourceSettings(sources, completed);
    }

    public async Task SaveAsync(GameSourceSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var connection = await OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = (SqliteTransaction)transaction;
            delete.CommandText = "delete from game_source_settings";
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var source in settings.Sources)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                insert into game_source_settings (
                    game, enabled, install_path, profile_path, save_path, save_paths_json
                ) values (
                    $game, $enabled, $install_path, $profile_path, $save_path, $save_paths_json
                )
                """;
            Add(insert, "$game", source.Game.ToString());
            Add(insert, "$enabled", source.Enabled ? 1 : 0);
            Add(insert, "$install_path", source.InstallPath);
            Add(insert, "$profile_path", source.ProfilePath);
            Add(insert, "$save_path", source.EffectiveSavePaths.FirstOrDefault() ?? source.SavePath);
            Add(insert, "$save_paths_json", JsonSerializer.Serialize(source.EffectiveSavePaths, JsonOptions));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var metadata = connection.CreateCommand())
        {
            metadata.Transaction = (SqliteTransaction)transaction;
            metadata.CommandText = """
                insert into game_source_settings_metadata (id, wizard_completed)
                values (1, $wizard_completed)
                on conflict(id) do update set wizard_completed = excluded.wizard_completed
                """;
            Add(metadata, "$wizard_completed", settings.WizardCompleted ? 1 : 0);
            await metadata.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists game_source_settings_metadata (
                id integer primary key check (id = 1),
                wizard_completed integer not null
            );

            create table if not exists game_source_settings (
                game text not null primary key,
                enabled integer not null,
                install_path text,
                profile_path text,
                save_path text,
                save_paths_json text
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyList<string> DeserializeSavePaths(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? [];
    }

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static void Add(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
}
