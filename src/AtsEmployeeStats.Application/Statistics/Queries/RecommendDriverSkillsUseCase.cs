using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed class RecommendDriverSkillsUseCase(
    IStatisticsDashboardUseCases dashboardUseCases) : IRecommendDriverSkillsUseCase
{
    public async Task ExecuteAsync(
        IOutputBoundaryAdapter<IReadOnlyList<DriverSkillRecommendationDto>> output,
        RecommendDriverSkillsInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var recommendations = await RecommendAsync(
            input.CompanyId,
            input.Query.ToOptions(),
            cancellationToken,
            ProgressOutputAdapter.ToProgress(progress, cancellationToken),
            input.Count);
        await output.PresentAsync(recommendations, cancellationToken);
    }

    public async Task<IReadOnlyList<DriverSkillRecommendationDto>> RecommendAsync(
        string companyId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        int count = 5)
    {
        var company = await dashboardUseCases.GetCompanyAsync(companyId, options, cancellationToken, progress);
        if (company is null)
            return [];

        return company.Drivers
            .Select(driver => RecommendForDriver(company, driver))
            .Where(recommendation => recommendation is not null)
            .Select(recommendation => recommendation!)
            .OrderByDescending(recommendation => recommendation.Score)
            .ThenBy(recommendation => recommendation.DriverName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, count))
            .ToList();
    }

    private static DriverSkillRecommendationDto? RecommendForDriver(CompanyDto company, DriverDto driver)
    {
        var recentJobs = (company.RecentDriverJobs ?? [])
            .Where(job => IdEquals(job.DriverId, driver.Id))
            .ToList();
        if (recentJobs.Count == 0)
            return null;

        var averageDistance = recentJobs
            .Where(job => job.Distance is > 0)
            .Select(job => job.Distance!.Value)
            .DefaultIfEmpty(0)
            .Average();
        var profitPerJobScore = recentJobs.Average(job => (decimal)job.Profit);
        var specializedCargoScore = recentJobs.Count(job => IsSpecializedCargo(job.Cargo)) * 1000m;

        if (averageDistance >= 750)
        {
            var score = (decimal)averageDistance * 12m;
            return new DriverSkillRecommendationDto(
                company.Id,
                driver.Id,
                driver.DisplayName,
                "Long Distance",
                score,
                "Recommended because this driver has long recent routes.");
        }

        if (profitPerJobScore >= 5000 || profitPerJobScore >= specializedCargoScore)
        {
            return new DriverSkillRecommendationDto(
                company.Id,
                driver.Id,
                driver.DisplayName,
                "High Value Cargo",
                profitPerJobScore,
                "Recommended because this driver has strong profit per job.");
        }

        return new DriverSkillRecommendationDto(
            company.Id,
            driver.Id,
            driver.DisplayName,
            "Specialized Cargo",
            specializedCargoScore,
            "Recommended because this driver has recent specialized cargo jobs.");
    }

    private static bool IsSpecializedCargo(string? cargo) =>
        cargo is not null &&
        (cargo.Contains("medical", StringComparison.OrdinalIgnoreCase) ||
         cargo.Contains("hazmat", StringComparison.OrdinalIgnoreCase) ||
         cargo.Contains("fragile", StringComparison.OrdinalIgnoreCase) ||
         cargo.Contains("glass", StringComparison.OrdinalIgnoreCase));

    private static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);
}
