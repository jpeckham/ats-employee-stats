using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class RecommendNextGarageCityUseCaseTests
{
    [Fact]
    public async Task RecommendAsync_returns_highest_scoring_eligible_unowned_city()
    {
        var useCase = CreateUseCase();

        var recommendation = await useCase.RecommendAsync("desert-line", new DashboardQueryOptions(), CancellationToken.None);

        Assert.NotNull(recommendation);
        Assert.Equal("desert-line", recommendation.CompanyId);
        Assert.Equal("denver", recommendation.CityId);
        Assert.Equal("Denver", recommendation.DisplayName);
        Assert.Equal(3.5m, recommendation.ExpansionScore);
        Assert.Equal(8500, recommendation.BidirectionalProfit);
        Assert.Contains("highest expansion score", recommendation.Reason);
    }

    [Fact]
    public async Task RecommendAsync_breaks_score_ties_by_bidirectional_profit()
    {
        var useCase = CreateUseCase();

        var recommendation = await useCase.RecommendAsync("mountain-haul", new DashboardQueryOptions(), CancellationToken.None);

        Assert.NotNull(recommendation);
        Assert.Equal("boise", recommendation.CityId);
        Assert.Equal(6000, recommendation.BidirectionalProfit);
    }

    [Fact]
    public async Task RecommendAsync_returns_null_when_company_or_candidate_is_missing()
    {
        var useCase = CreateUseCase();

        Assert.Null(await useCase.RecommendAsync("missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Null(await useCase.RecommendAsync("owned-only", new DashboardQueryOptions(), CancellationToken.None));
    }

    private static RecommendNextGarageCityUseCase CreateUseCase() =>
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
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    [
                        new CityStatistic("phoenix", "Phoenix", true, true, 5, 4000, 3000, 7000, 4.0m),
                        new CityStatistic("denver", "Denver", false, true, 7, 5000, 3500, 8500, 3.5m),
                        new CityStatistic("reno", "Reno", false, true, 9, 9000, 5000, 14000, 2.5m),
                        new CityStatistic("hidden", "Hidden", false, false, 12, 10000, 10000, 20000, 10.0m)
                    ],
                    [],
                    []),
                new CompanyStatistics(
                    "mountain-haul",
                    "Mountain Haul",
                    updated,
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    [
                        new CityStatistic("boise", "Boise", false, true, 3, 4000, 2000, 6000, 2.0m),
                        new CityStatistic("spokane", "Spokane", false, true, 6, 2500, 2500, 5000, 2.0m)
                    ],
                    [],
                    []),
                new CompanyStatistics(
                    "owned-only",
                    "Owned Only",
                    updated,
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    [
                        new CityStatistic("phoenix", "Phoenix", true, true, 5, 4000, 3000, 7000, 4.0m)
                    ],
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
