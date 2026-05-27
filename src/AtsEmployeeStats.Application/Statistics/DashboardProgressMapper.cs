using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics;

public static class DashboardProgressMapper
{
    public static DashboardProgressDto ToDashboardProgressDto(SaveLoadProgress progress) =>
        new(
            progress.Message,
            progress.CompletedFiles,
            progress.TotalFiles,
            progress.CurrentFileCompletedUnits,
            progress.CurrentFileTotalUnits);
}
