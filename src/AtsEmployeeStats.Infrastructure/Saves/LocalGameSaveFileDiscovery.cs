using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class LocalGameSaveFileDiscovery : IGameSaveFileDiscovery
{
    private readonly IGameSaveDiscovery _rootDiscovery;
    private readonly Func<string, string, IEnumerable<string>> _enumerateFiles;
    private readonly Func<string, bool> _fileExists;

    public LocalGameSaveFileDiscovery(IGameSaveDiscovery rootDiscovery)
        : this(
            rootDiscovery,
            (path, pattern) => Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories),
            File.Exists)
    {
    }

    public LocalGameSaveFileDiscovery(
        IGameSaveDiscovery rootDiscovery,
        Func<string, string, IEnumerable<string>> enumerateFiles,
        Func<string, bool> fileExists)
    {
        _rootDiscovery = rootDiscovery;
        _enumerateFiles = enumerateFiles;
        _fileExists = fileExists;
    }

    public async Task<IReadOnlyList<GameSaveFile>> FindCandidateSaveFilesAsync(
        GameSaveKind game,
        CancellationToken cancellationToken)
    {
        var roots = await _rootDiscovery.FindCandidateRootsAsync(game, cancellationToken);
        var files = new List<GameSaveFile>();

        foreach (var root in roots.Where(root => root.Exists))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var path in _enumerateFiles(root.Path, "game.sii"))
            {
                files.Add(new GameSaveFile(game, path, _fileExists(path)));
            }
        }

        return files;
    }
}
