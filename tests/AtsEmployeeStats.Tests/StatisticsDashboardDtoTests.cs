using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsDashboardDtoTests
{
    [Fact]
    public void Dashboard_statistics_dto_carries_drilldown_data_for_web_and_api()
    {
        var statistics = new DashboardStatisticsDto(
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            [
                new CompanyDto(
                    "desert-line",
                    "Desert Line",
                    6000,
                    [
                        new GarageDto("garage.phoenix", "phoenix", 5000, 357, 1, 1)
                    ],
                    [
                        new DriverDto("driver.alice", "Alice Ramirez", 3000, 429, "garage.phoenix", "truck.alice", 3)
                    ],
                    [
                        new TruckDto("truck.alice", "ATS-100", 725, "garage.phoenix", "driver.alice")
                    ],
                    [
                        new MissionDto("job.alice", "driver.alice", "truck.alice", "reefer", "medicine", "phoenix", "denver", 3000)
                    ],
                    [
                        new TrailerTypeDto("reefer", 3000, 1)
                    ])
            ]);

        var company = Assert.Single(statistics.Companies);
        Assert.Equal("Desert Line", company.DisplayName);
        Assert.Equal(6000, company.Profit);
        Assert.Equal("phoenix", Assert.Single(company.Garages).DisplayName);
        Assert.Equal("Alice Ramirez", Assert.Single(company.Drivers).DisplayName);
        Assert.Equal("ATS-100", Assert.Single(company.Trucks).DisplayName);
        Assert.Equal("medicine", Assert.Single(company.Missions).Cargo);
        Assert.Equal("reefer", Assert.Single(company.TrailerTypes).Id);
    }

    [Fact]
    public void Config_and_live_message_dtos_capture_api_runtime_state()
    {
        var config = new DashboardConfigDto("C:\\ATS", 14);
        var status = new DashboardStatusDto("Loaded", false);
        var progress = new DashboardProgressDto("Parsing saves", 2, 10, 50, 100);

        Assert.Equal("C:\\ATS", config.SaveRoot);
        Assert.Equal(14, config.HistoryDays);
        Assert.Equal("Loaded", status.Message);
        Assert.False(status.IsError);
        Assert.Equal(2, progress.CompletedFiles);
        Assert.Equal(100, progress.CurrentFileTotalUnits);
    }
}
