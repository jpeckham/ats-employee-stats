using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Application.Statistics.Output;

public interface IProgressOutputBoundaryAdapter
{
    Task PresentProgressAsync(SaveLoadProgress progress, CancellationToken cancellationToken);
}
