using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed class RecommendTrailersForGarageUseCase(
    IStatisticsDashboardUseCases dashboardUseCases) : IRecommendTrailersForGarageUseCase
{
    public async Task ExecuteAsync(
        IOutputBoundaryAdapter<IReadOnlyList<TrailerRecommendationDto>> output,
        RecommendTrailersForGarageInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var recommendations = await RecommendAsync(
            input.CompanyId,
            input.GarageId,
            input.Query.ToOptions(),
            cancellationToken,
            ProgressOutputAdapter.ToProgress(progress, cancellationToken),
            input.Count);
        await output.PresentAsync(recommendations, cancellationToken);
    }

    public async Task<IReadOnlyList<TrailerRecommendationDto>> RecommendAsync(
        string companyId,
        string garageId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        int count = 3)
    {
        var company = await dashboardUseCases.GetCompanyAsync(companyId, options, cancellationToken, progress);
        if (company is null || !company.Garages.Any(garage => IdEquals(garage.Id, garageId)))
            return [];

        return company.Missions
            .Where(mission =>
                IdEquals(mission.GarageId, garageId) &&
                !string.IsNullOrWhiteSpace(mission.TrailerType))
            .GroupBy(mission => mission.TrailerType!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var profit = group.Sum(mission => mission.Profit);
                var jobCount = group.Count();
                var profitPerJob = (long)Math.Round(profit / (decimal)jobCount, MidpointRounding.AwayFromZero);
                return new TrailerRecommendationDto(
                    company.Id,
                    garageId,
                    group.Key,
                    profit,
                    jobCount,
                    profitPerJob,
                    "Recommended because this trailer type has the highest profit for this garage.");
            })
            .Where(recommendation => recommendation.Profit > 0)
            .OrderByDescending(recommendation => recommendation.Profit)
            .ThenByDescending(recommendation => recommendation.ProfitPerJob)
            .ThenBy(recommendation => recommendation.TrailerType, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, count))
            .ToList();
    }

    private static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);
}
