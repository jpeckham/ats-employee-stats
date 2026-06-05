namespace AtsEmployeeStats.Wpf.Threading;

public interface IBackgroundRunner
{
    Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default);

    Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default);
}
