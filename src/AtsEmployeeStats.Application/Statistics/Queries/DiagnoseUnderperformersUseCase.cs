using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed class DiagnoseUnderperformersUseCase(
    IStatisticsDashboardUseCases dashboardUseCases) : IDiagnoseUnderperformersUseCase
{
    private static readonly IReadOnlyDictionary<string, int> EntityKindOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Driver"] = 0,
        ["Truck"] = 1,
        ["Trailer"] = 2,
        ["Garage"] = 3
    };

    public async Task ExecuteAsync(
        IOutputBoundaryAdapter<IReadOnlyList<UnderperformerDiagnosisDto>> output,
        DiagnoseUnderperformersInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var diagnoses = await DiagnoseAsync(
            input.CompanyId,
            input.Query.ToOptions(),
            cancellationToken,
            ProgressOutputAdapter.ToProgress(progress, cancellationToken),
            input.Count);
        await output.PresentAsync(diagnoses, cancellationToken);
    }

    public async Task<IReadOnlyList<UnderperformerDiagnosisDto>> DiagnoseAsync(
        string companyId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        int count = 5)
    {
        var company = await dashboardUseCases.GetCompanyAsync(companyId, options, cancellationToken, progress);
        if (company is null)
            return [];

        var candidates = BuildCandidates(company).ToList();
        if (candidates.Count == 0)
            return [];

        return candidates
            .Where(candidate => candidate.Profit <= 0)
            .OrderBy(candidate => candidate.ProfitPerDay)
            .ThenBy(candidate => EntityKindOrder.GetValueOrDefault(candidate.EntityKind, int.MaxValue))
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, count))
            .Select(candidate => candidate with
            {
                Reason = $"This {candidate.EntityKind.ToLowerInvariant()} is below the company average with non-positive total profit."
            })
            .ToList();
    }

    private static IEnumerable<UnderperformerDiagnosisDto> BuildCandidates(CompanyDto company)
    {
        foreach (var driver in company.Drivers.Where(driver => driver.JobCount > 0))
        {
            yield return new UnderperformerDiagnosisDto(
                company.Id,
                "Driver",
                driver.Id,
                driver.DisplayName,
                driver.Profit,
                driver.ProfitPerDay,
                driver.JobCount,
                string.Empty);
        }

        foreach (var truck in company.Trucks)
        {
            var jobCount = company.Missions.Count(mission => IdEquals(mission.TruckId, truck.Id));
            if (jobCount == 0)
                continue;

            yield return new UnderperformerDiagnosisDto(
                company.Id,
                "Truck",
                truck.Id,
                truck.DisplayName,
                truck.Profit,
                truck.ProfitPerDay,
                jobCount,
                string.Empty);
        }

        foreach (var trailer in company.Trailers ?? [])
        {
            if (trailer.JobCount == 0)
                continue;

            yield return new UnderperformerDiagnosisDto(
                company.Id,
                "Trailer",
                trailer.Id,
                trailer.TrailerType,
                trailer.Profit,
                trailer.ProfitPerDay,
                trailer.JobCount,
                string.Empty);
        }

        foreach (var garage in company.Garages.Where(garage =>
            garage.EmployeeCount > 0 || garage.TruckCount > 0 || garage.TrailerCount > 0 || garage.Profit != 0))
        {
            yield return new UnderperformerDiagnosisDto(
                company.Id,
                "Garage",
                garage.Id,
                garage.DisplayName,
                garage.Profit,
                garage.ProfitPerDay,
                garage.EmployeeCount,
                string.Empty);
        }
    }

    private static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);
}
