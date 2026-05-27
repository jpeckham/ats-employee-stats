using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class DashboardProgressMapperTests
{
    [Fact]
    public void ToDashboardProgressDto_maps_file_and_current_save_unit_progress()
    {
        var progress = new SaveLoadProgress(
            SaveLoadStage.LoadingFiles,
            CompletedFiles: 3,
            TotalFiles: 12,
            CompletedUnits: 150,
            TotalUnits: 500,
            Message: "Loading save 3 of 12.",
            CurrentFileCompletedUnits: 42,
            CurrentFileTotalUnits: 100);

        var dto = DashboardProgressMapper.ToDashboardProgressDto(progress);

        Assert.Equal("Loading save 3 of 12.", dto.Message);
        Assert.Equal(3, dto.CompletedFiles);
        Assert.Equal(12, dto.TotalFiles);
        Assert.Equal(42, dto.CurrentFileCompletedUnits);
        Assert.Equal(100, dto.CurrentFileTotalUnits);
    }
}
