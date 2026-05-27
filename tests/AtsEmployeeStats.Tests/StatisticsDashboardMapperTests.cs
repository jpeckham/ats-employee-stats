using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsDashboardMapperTests
{
    [Fact]
    public void ToDashboardDto_maps_statistics_and_computes_per_day_values()
    {
        var statistics = new AtsStatistics(
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            [
                new CompanyStatistics(
                    "desert-line",
                    "Desert Line",
                    new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
                    [
                        new GarageStatistic("garage.phoenix", "phoenix", 5000, 1, 1)
                    ],
                    [
                        new DriverStatistic("driver.alice", "Alice Ramirez", 3000, "garage.phoenix", "truck.alice")
                    ],
                    [
                        new TruckStatistic("truck.alice", "ATS-100", 725, "garage.phoenix", "driver.alice")
                    ],
                    [
                        new MissionStatistic("job.1", "driver.alice", "truck.alice", "trailer.1", "reefer", "medicine", "phoenix", "denver", 3000),
                        new MissionStatistic("job.2", "driver.alice", "truck.alice", "trailer.2", "flatbed", "steel", "denver", "phoenix", 1500)
                    ],
                    [
                        new TrailerTypeStatistic("reefer", 3000, 1)
                    ])
            ]);

        var dto = StatisticsDashboardMapper.ToDashboardDto(statistics, rangeDays: 7);

        var company = Assert.Single(dto.Companies);
        Assert.Equal("Desert Line", company.DisplayName);
        Assert.Equal(5000, company.Profit);
        Assert.Equal(714, Assert.Single(company.Garages).ProfitPerDay);
        var driver = Assert.Single(company.Drivers);
        Assert.Equal(429, driver.ProfitPerDay);
        Assert.Equal(2, driver.JobCount);
        Assert.Equal("ATS-100", Assert.Single(company.Trucks).DisplayName);
        Assert.Contains(company.Missions, mission => mission.Cargo == "medicine");
    }
}
