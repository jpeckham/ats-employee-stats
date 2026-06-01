using AtsEmployeeStats.Application.Statistics.Output;

namespace AtsEmployeeStats.Api.Presentation;

public sealed class HttpResultPresenter<T> : IOutputBoundaryAdapter<T>
{
    public IResult ViewModel { get; private set; } = Results.NoContent();

    public Task PresentAsync(T response, CancellationToken cancellationToken)
    {
        ViewModel = Results.Ok(response);
        return Task.CompletedTask;
    }
}
