namespace AtsEmployeeStats.Application.Statistics.Output;

public interface IOutputBoundaryAdapter<in TResponse>
{
    Task PresentAsync(TResponse response, CancellationToken cancellationToken);
}
