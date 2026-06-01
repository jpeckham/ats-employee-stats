using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class RecommendTrailersForGarageUseCaseTests
{
    [Fact]
    public async Task RecommendAsync_returns_trailer_types_ranked_by_profit_for_garage()
    {
        var useCase = CreateUseCase();

        var recommendations = await useCase.RecommendAsync("desert-line", "garage.phoenix", new DashboardQueryOptions(), CancellationToken.None);

        Assert.Collection(
            recommendations,
            recommendation =>
            {
                Assert.Equal("reefer", recommendation.TrailerType);
                Assert.Equal(7000, recommendation.Profit);
                Assert.Equal(2, recommendation.JobCount);
                Assert.Equal(3500, recommendation.ProfitPerJob);
                Assert.Contains("highest profit", recommendation.Reason);
            },
            recommendation =>
            {
                Assert.Equal("flatbed", recommendation.TrailerType);
                Assert.Equal(2500, recommendation.Profit);
                Assert.Equal(1, recommendation.JobCount);
                Assert.Equal(2500, recommendation.ProfitPerJob);
            });
    }

    [Fact]
    public async Task RecommendAsync_limits_result_count_and_breaks_ties_by_profit_per_job()
    {
        var useCase = CreateUseCase();

        var recommendations = await useCase.RecommendAsync(
            "desert-line",
            "garage.denver",
            new DashboardQueryOptions(),
            CancellationToken.None,
            count: 1);

        var recommendation = Assert.Single(recommendations);
        Assert.Equal("lowboy", recommendation.TrailerType);
        Assert.Equal(6000, recommendation.Profit);
        Assert.Equal(6000, recommendation.ProfitPerJob);
    }

    [Fact]
    public async Task RecommendAsync_returns_empty_when_company_or_garage_has_no_candidate_jobs()
    {
        var useCase = CreateUseCase();

        Assert.Empty(await useCase.RecommendAsync("missing", "garage.phoenix", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Empty(await useCase.RecommendAsync("desert-line", "missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Empty(await useCase.RecommendAsync("empty-line", "garage.empty", new DashboardQueryOptions(), CancellationToken.None));
    }

    private static RecommendTrailersForGarageUseCase CreateUseCase() =>
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
                        new GarageStatistic("garage.phoenix", "Phoenix", 0, 1, 1),
                        new GarageStatistic("garage.denver", "Denver", 0, 1, 1)
                    ],
                    [],
                    [],
                    [
                        new MissionStatistic("job.1", null, null, null, "reefer", null, null, null, 3000, GarageId: "garage.phoenix"),
                        new MissionStatistic("job.2", null, null, null, "reefer", null, null, null, 4000, GarageId: "garage.phoenix"),
                        new MissionStatistic("job.3", null, null, null, "flatbed", null, null, null, 2500, GarageId: "garage.phoenix"),
                        new MissionStatistic("job.4", null, null, null, "dryvan", null, null, null, 6000, GarageId: "garage.denver"),
                        new MissionStatistic("job.5", null, null, null, "dryvan", null, null, null, 0, GarageId: "garage.denver"),
                        new MissionStatistic("job.6", null, null, null, "lowboy", null, null, null, 6000, GarageId: "garage.denver")
                    ],
                    []),
                new CompanyStatistics(
                    "empty-line",
                    "Empty Line",
                    updated,
                    [new GarageStatistic("garage.empty", "Empty", 0, 0, 0)],
                    [],
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
