namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class SaveFileWatcher : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _debounceTimer;

    public SaveFileWatcher(string rootPath, Func<Task> onChanged)
    {
        _debounceTimer = new Timer(
            _ => _ = Task.Run(onChanged),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        if (!Directory.Exists(rootPath))
        {
            return;
        }

        _watcher = new FileSystemWatcher(rootPath, "game.sii")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Changed += (_, _) => Debounce();
        _watcher.Created += (_, _) => Debounce();
        _watcher.Renamed += (_, _) => Debounce();
        _watcher.EnableRaisingEvents = true;
    }

    private void Debounce() =>
        _debounceTimer.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer.Dispose();
    }
}
