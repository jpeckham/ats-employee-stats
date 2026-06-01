using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;

namespace AtsEmployeeStats.Application.Statistics.Queries;

internal static class ProgressOutputAdapter
{
    public static IProgress<SaveLoadProgress>? ToProgress(
        IProgressOutputBoundaryAdapter? output,
        CancellationToken cancellationToken) =>
        output is null
            ? null
            : new Progress<SaveLoadProgress>(progress =>
            {
                _ = output.PresentProgressAsync(progress, cancellationToken);
            });
}
