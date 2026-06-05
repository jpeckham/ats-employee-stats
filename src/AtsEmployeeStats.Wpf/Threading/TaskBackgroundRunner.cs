namespace AtsEmployeeStats.Wpf.Threading;

public sealed class TaskBackgroundRunner : IBackgroundRunner
{
    public Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default) =>
        Task.Run(work, cancellationToken);

    public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default) =>
        Task.Run(work, cancellationToken);
}
