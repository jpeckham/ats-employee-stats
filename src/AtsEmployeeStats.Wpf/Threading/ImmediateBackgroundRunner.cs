namespace AtsEmployeeStats.Wpf.Threading;

public sealed class ImmediateBackgroundRunner : IBackgroundRunner
{
    public Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(work());
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await work();
    }
}
