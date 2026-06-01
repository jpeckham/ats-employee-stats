using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class DiagnoseUnderperformersUseCaseTests
{
    [Fact]
    public async Task DiagnoseAsync_returns_active_entities_ranked_by_low_profit_per_day()
    {
        var useCase = CreateUseCase();

        var diagnoses = await useCase.DiagnoseAsync("desert-line", new DashboardQueryOptions(), CancellationToken.None);

        Assert.Collection(
            diagnoses,
            diagnosis =>
            {
                Assert.Equal("Driver", diagnosis.EntityKind);
                Assert.Equal("driver.bob", diagnosis.EntityId);
                Assert.Equal("Bob Smith", diagnosis.DisplayName);
                Assert.Equal(-500, diagnosis.Profit);
                Assert.Equal(-500, diagnosis.ProfitPerDay);
                Assert.Equal(1, diagnosis.JobCount);
                Assert.Contains("below the company average", diagnosis.Reason);
            },
            diagnosis =>
            {
                Assert.Equal("Truck", diagnosis.EntityKind);
                Assert.Equal("truck.bob", diagnosis.EntityId);
            },
            diagnosis =>
            {
                Assert.Equal("Trailer", diagnosis.EntityKind);
                Assert.Equal("trailer.lowboy.1", diagnosis.EntityId);
            });
    }

    [Fact]
    public async Task DiagnoseAsync_limits_count_and_returns_empty_for_missing_or_healthy_company()
    {
        var useCase = CreateUseCase();

        var limited = await useCase.DiagnoseAsync(
            "desert-line",
            new DashboardQueryOptions(),
            CancellationToken.None,
            count: 1);

        Assert.Single(limited);
        Assert.Empty(await useCase.DiagnoseAsync("missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Empty(await useCase.DiagnoseAsync("healthy-line", new DashboardQueryOptions(), CancellationToken.None));
    }

    private static DiagnoseUnderperformersUseCase CreateUseCase() =>
        new(new StatisticsDashboardUseCases(new StatisticsService(new StubStatisticsSource(CreateStatistics()))));

    private static AtsStatistics CreateStatistics()
    {
        var updated = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        return new AtsStatistics(
            updated,
            [
                new CompanyStatistics(
                    "desert-line",
                    "Desert Line",
                    updated,
                    [
                        new GarageStatistic("garage.phoenix", "Phoenix", 9000, 2, 2),
                        new GarageStatistic("garage.empty", "Empty", 0, 0, 0)
                    ],
                    [
                        new DriverStatistic("driver.alice", "Alice Ramirez", 10000, "garage.phoenix", "truck.alice"),
                        new DriverStatistic("driver.bob", "Bob Smith", -500, "garage.phoenix", "truck.bob"),
                        new DriverStatistic("driver.idle", "Idle Driver", 0, "garage.empty", null)
                    ],
                    [
                        new TruckStatistic("truck.alice", "ATS-100", 10000, "garage.phoenix", "driver.alice"),
                        new TruckStatistic("truck.bob", "ATS-200", -500, "garage.phoenix", "driver.bob")
                    ],
                    [
                        new MissionStatistic("job.1", "driver.alice", "truck.alice", "trailer.reefer.1", "reefer", null, null, null, 10000, 200, "garage.phoenix"),
                        new MissionStatistic("job.2", "driver.bob", "truck.bob", "trailer.lowboy.1", "lowboy", null, null, null, -500, 201, "garage.phoenix")
                    ],
                    [],
                    [],
                    [
                        new TrailerStatistic("trailer.reefer.1", "reefer", 10000, 1, GarageId: "garage.phoenix"),
                        new TrailerStatistic("trailer.lowboy.1", "lowboy", -500, 1, GarageId: "garage.phoenix")
                    ],
                    [],
                    [],
                    []),
                new CompanyStatistics(
                    "healthy-line",
                    "Healthy Line",
                    updated,
                    [new GarageStatistic("garage.tucson", "Tucson", 1000, 1, 1)],
                    [new DriverStatistic("driver.carol", "Carol Lee", 1000, "garage.tucson", "truck.carol")],
                    [new TruckStatistic("truck.carol", "ATS-300", 1000, "garage.tucson", "driver.carol")],
                    [new MissionStatistic("job.3", "driver.carol", "truck.carol", null, null, null, null, null, 1000, 200, "garage.tucson")],
                    [])
            ]);
    }

    private sealed class StubStatisticsSource(AtsStatistics statistics) : ISaveSnapshotSource, IStatisticsQuerySource
    {
        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult<IReadOnlyList<SaveSnapshot>>([]);

        public Task<AtsStatistics> ReadStatisticsAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult(statistics);
    }
}
