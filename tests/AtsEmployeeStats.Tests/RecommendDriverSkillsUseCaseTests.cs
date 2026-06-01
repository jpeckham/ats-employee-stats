using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class RecommendDriverSkillsUseCaseTests
{
    [Fact]
    public async Task RecommendAsync_returns_skill_recommendations_ranked_by_driver_job_signals()
    {
        var useCase = CreateUseCase();

        var recommendations = await useCase.RecommendAsync("desert-line", new DashboardQueryOptions(), CancellationToken.None);

        Assert.Collection(
            recommendations,
            recommendation =>
            {
                Assert.Equal("driver.alice", recommendation.DriverId);
                Assert.Equal("Alice Ramirez", recommendation.DriverName);
                Assert.Equal("Long Distance", recommendation.SkillName);
                Assert.True(recommendation.Score > 0);
                Assert.Contains("long recent routes", recommendation.Reason);
            },
            recommendation =>
            {
                Assert.Equal("driver.bob", recommendation.DriverId);
                Assert.Equal("High Value Cargo", recommendation.SkillName);
                Assert.Contains("profit per job", recommendation.Reason);
            });
    }

    [Fact]
    public async Task RecommendAsync_limits_count_and_returns_empty_for_missing_or_inactive_company()
    {
        var useCase = CreateUseCase();

        var limited = await useCase.RecommendAsync("desert-line", new DashboardQueryOptions(), CancellationToken.None, count: 1);

        Assert.Single(limited);
        Assert.Empty(await useCase.RecommendAsync("missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Empty(await useCase.RecommendAsync("inactive-line", new DashboardQueryOptions(), CancellationToken.None));
    }

    private static RecommendDriverSkillsUseCase CreateUseCase() =>
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
                    [],
                    [
                        new DriverStatistic("driver.alice", "Alice Ramirez", 10000, null, "truck.alice"),
                        new DriverStatistic("driver.bob", "Bob Smith", 9000, null, "truck.bob")
                    ],
                    [],
                    [],
                    [],
                    [
                        new DriverRecentJobStatistic("job.1", "driver.alice", "truck.alice", "machinery", "phoenix", "seattle", 7000, 1000, 6000, 875, 200),
                        new DriverRecentJobStatistic("job.2", "driver.alice", "truck.alice", "lumber", "seattle", "phoenix", 6000, 2000, 4000, 820, 201),
                        new DriverRecentJobStatistic("job.3", "driver.bob", "truck.bob", "medical_equipment", "phoenix", "denver", 11000, 2000, 9000, 310, 201)
                    ]),
                new CompanyStatistics(
                    "inactive-line",
                    "Inactive Line",
                    updated,
                    [],
                    [new DriverStatistic("driver.idle", "Idle Driver", 0, null, null)],
                    [],
                    [],
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
