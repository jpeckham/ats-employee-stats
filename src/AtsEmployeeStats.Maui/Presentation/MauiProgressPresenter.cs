using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;

namespace AtsEmployeeStats.Maui.Presentation;

internal sealed class MauiProgressPresenter(IDashboardPresentationTarget target)
    : IProgressOutputBoundaryAdapter
{
    public Task PresentProgressAsync(SaveLoadProgress progress, CancellationToken cancellationToken)
    {
        var currentFileRatio = progress.CurrentFileTotalUnits <= 0
            ? 0
            : Math.Clamp(progress.CurrentFileCompletedUnits / (double)progress.CurrentFileTotalUnits, 0, 1);
        var overallRatio = progress.TotalFiles <= 0
            ? progress.Stage == SaveLoadStage.Completed ? 1 : 0
            : Math.Clamp((progress.CompletedFiles + currentFileRatio) / progress.TotalFiles, 0, 1);

        target.ShowProgress(new DashboardProgressPresentation(
            progress.Message,
            overallRatio,
            currentFileRatio,
            $"Save files: {progress.CompletedFiles:N0} of {progress.TotalFiles:N0}",
            progress.CurrentFileTotalUnits <= 0
                ? "Current save: waiting for parser"
                : $"Current save: {progress.CurrentFileCompletedUnits:N0} of {progress.CurrentFileTotalUnits:N0} units"));
        return Task.CompletedTask;
    }

    public IProgress<SaveLoadProgress> AsProgress(CancellationToken cancellationToken) =>
        new Progress<SaveLoadProgress>(progress =>
        {
            _ = PresentProgressAsync(progress, cancellationToken);
        });
}
