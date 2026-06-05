using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed class RecommendNextGarageCityUseCase(
    IStatisticsDashboardUseCases dashboardUseCases) : IRecommendNextGarageCityUseCase
{
    public async Task ExecuteAsync(
        IOutputBoundaryAdapter<GarageCityRecommendationDto?> output,
        RecommendNextGarageCityInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var recommendation = await RecommendAsync(
            input.CompanyId,
            input.Query.ToOptions(),
            cancellationToken,
            ProgressOutputAdapter.ToProgress(progress, cancellationToken));
        await output.PresentAsync(recommendation, cancellationToken);
    }

    public async Task<GarageCityRecommendationDto?> RecommendAsync(
        string companyId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        var company = await dashboardUseCases.GetCompanyAsync(companyId, options, cancellationToken, progress);
        var candidate = company?
            .Cities?
            .Where(city => city.IsGarageEligible && !city.HasOwnedGarage)
            .OrderByDescending(city => city.ExpansionScore)
            .ThenByDescending(city => city.PlayerOriginScore)
            .ThenByDescending(city => city.BidirectionalProfit)
            .ThenBy(city => city.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (company is null || candidate is null)
            return null;

        return new GarageCityRecommendationDto(
            company.Id,
            candidate.Id,
            candidate.DisplayName,
            candidate.ExpansionScore,
            candidate.BidirectionalProfit,
            candidate.VisitCount,
            candidate.PlayerOriginScore > 0
                ? "Recommended because it is the eligible unowned city with the highest expansion score, with supporting player origin data."
                : "Recommended because it is the eligible unowned city with the highest expansion score.",
            candidate.PlayerOriginScore,
            candidate.PlayerOriginJobCount,
            candidate.PlayerOriginProfit);
    }
}
