using AtsEmployeeStats.Application.Statistics.Output;

namespace AtsEmployeeStats.Api.Presentation;

public sealed class NullableHttpResultPresenter<T> : IOutputBoundaryAdapter<T?>
{
    public IResult ViewModel { get; private set; } = Results.NoContent();

    public Task PresentAsync(T? response, CancellationToken cancellationToken)
    {
        ViewModel = response is null ? Results.NotFound() : Results.Ok(response);
        return Task.CompletedTask;
    }
}
