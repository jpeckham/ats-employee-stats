using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Domain.Saves;
using System.Collections.Concurrent;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class FileSaveSnapshotSource(string rootPath, TimeSpan? historyWindow = null) : ISaveSnapshotSource
{
    private readonly SiiSaveTextDecoder _decoder = new();

    public async Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        if (!Directory.Exists(rootPath))
        {
            progress?.Report(new SaveLoadProgress(
                SaveLoadStage.Completed,
                CompletedFiles: 0,
                TotalFiles: 0,
                CompletedUnits: 0,
                TotalUnits: 0,
                Message: "Save root was not found."));
            return [];
        }

        progress?.Report(new SaveLoadProgress(
            SaveLoadStage.DiscoveringFiles,
            CompletedFiles: 0,
            TotalFiles: 0,
            CompletedUnits: 0,
            TotalUnits: 0,
            Message: "Discovering game.sii files..."));

        var cutoffUtc = historyWindow is null ? (DateTime?)null : DateTime.UtcNow.Subtract(historyWindow.Value);
        var paths = Directory
            .EnumerateFiles(rootPath, "game.sii", SearchOption.AllDirectories)
            .Select(path => new { Path = path, LastWriteTimeUtc = File.GetLastWriteTimeUtc(path) })
            .Where(file => cutoffUtc is null || file.LastWriteTimeUtc >= cutoffUtc)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.Path)
            .ToList();
        var snapshots = new List<SaveSnapshot>();
        var completedFiles = 0;
        var completedUnits = 0;
        var estimatedTotalUnits = 0;

        progress?.Report(new SaveLoadProgress(
            SaveLoadStage.FilesDiscovered,
            CompletedFiles: 0,
            TotalFiles: paths.Count,
            CompletedUnits: 0,
            TotalUnits: 0,
            Message: $"Found {paths.Count:N0} save file{(paths.Count == 1 ? string.Empty : "s")}."));

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await TryReadSnapshotAsync(
                path,
                completedFiles,
                paths.Count,
                completedUnits,
                estimatedTotalUnits,
                progress,
                cancellationToken);
            completedFiles++;
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
                var unitCount = snapshot.Document.Units.Count;
                if (estimatedTotalUnits == 0 && unitCount > 0)
                {
                    estimatedTotalUnits = unitCount * paths.Count;
                }

                completedUnits += unitCount;
                progress?.Report(new SaveLoadProgress(
                    SaveLoadStage.FileLoaded,
                    CompletedFiles: completedFiles,
                    TotalFiles: paths.Count,
                    CompletedUnits: completedUnits,
                    TotalUnits: Math.Max(estimatedTotalUnits, completedUnits),
                    Message: $"Loaded {completedFiles:N0} of {paths.Count:N0} save files.",
                    CurrentFile: path,
                    CurrentFileCompletedUnits: unitCount,
                    CurrentFileTotalUnits: unitCount));
            }
            else
            {
                progress?.Report(new SaveLoadProgress(
                    SaveLoadStage.FileLoaded,
                    CompletedFiles: completedFiles,
                    TotalFiles: paths.Count,
                    CompletedUnits: completedUnits,
                    TotalUnits: estimatedTotalUnits,
                    Message: $"Skipped {completedFiles:N0} of {paths.Count:N0} save files.",
                    CurrentFile: path));
            }
        }

        progress?.Report(new SaveLoadProgress(
            SaveLoadStage.Completed,
            CompletedFiles: completedFiles,
            TotalFiles: paths.Count,
            CompletedUnits: completedUnits,
            TotalUnits: Math.Max(estimatedTotalUnits, completedUnits),
            Message: $"Loaded {completedFiles:N0} save file{(completedFiles == 1 ? string.Empty : "s")}."));

        return snapshots
            .OrderByDescending(snapshot => snapshot.LastWritten)
            .ToList();
    }

    private async Task<SaveSnapshot?> TryReadSnapshotAsync(
        string path,
        int completedFiles,
        int totalFiles,
        int completedUnits,
        int estimatedTotalUnits,
        IProgress<SaveLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await ReadTextWithRetriesAsync(path, cancellationToken);
            if (content is null || !LooksLikePlainSii(content))
            {
                return null;
            }

            var currentFileTotalUnits = SiiSaveParser.CountUnits(content);
            var parserProgress = new InlineProgress<int>(currentFileCompletedUnits =>
            {
                progress?.Report(new SaveLoadProgress(
                    SaveLoadStage.LoadingFiles,
                    CompletedFiles: completedFiles,
                    TotalFiles: totalFiles,
                    CompletedUnits: completedUnits + currentFileCompletedUnits,
                    TotalUnits: Math.Max(estimatedTotalUnits, completedUnits + currentFileTotalUnits),
                    Message: $"Parsing save file {completedFiles + 1:N0} of {totalFiles:N0}.",
                    CurrentFile: path,
                    CurrentFileCompletedUnits: currentFileCompletedUnits,
                    CurrentFileTotalUnits: currentFileTotalUnits));
            });
            var info = new FileInfo(path);
            return new SaveSnapshot(path, info.LastWriteTimeUtc, SiiSaveParser.Parse(content, parserProgress));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task<string?> ReadTextWithRetriesAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return await _decoder.DecodeAsync(path, cancellationToken);
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }
        }

        return null;
    }

    private static bool LooksLikePlainSii(string content) =>
        content.Contains("SiiNunit", StringComparison.OrdinalIgnoreCase);

    private sealed class InlineProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }
}
