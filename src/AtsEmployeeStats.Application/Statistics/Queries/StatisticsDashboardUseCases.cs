using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed class StatisticsDashboardUseCases(StatisticsService statisticsService) : IStatisticsDashboardUseCases
{
    public async Task ExecuteDashboardAsync(
        IOutputBoundaryAdapter<DashboardStatisticsDto> output,
        DashboardQueryRequest request,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var dashboard = await GetDashboardAsync(
            request.ToOptions(),
            cancellationToken,
            ToProgress(progress, cancellationToken));
        await output.PresentAsync(dashboard, cancellationToken);
    }

    public async Task ExecuteListCompaniesAsync(
        IOutputBoundaryAdapter<IReadOnlyList<CompanyDto>> output,
        ListCompaniesInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var companies = await ListCompaniesAsync(input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(companies, cancellationToken);
    }

    public async Task ExecuteCompanyAsync(
        IOutputBoundaryAdapter<CompanyDto?> output,
        CompanyInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var company = await GetCompanyAsync(input.CompanyId, input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(company, cancellationToken);
    }

    public async Task ExecuteDriverAsync(
        IOutputBoundaryAdapter<DriverDto?> output,
        DriverInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var driver = await GetDriverAsync(input.CompanyId, input.DriverId, input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(driver, cancellationToken);
    }

    public async Task ExecuteGarageAsync(
        IOutputBoundaryAdapter<GarageDto?> output,
        GarageInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var garage = await GetGarageAsync(input.CompanyId, input.GarageId, input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(garage, cancellationToken);
    }

    public async Task ExecuteTruckAsync(
        IOutputBoundaryAdapter<TruckDto?> output,
        TruckInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var truck = await GetTruckAsync(input.CompanyId, input.TruckId, input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(truck, cancellationToken);
    }

    public async Task ExecuteTrailerAsync(
        IOutputBoundaryAdapter<TrailerDto?> output,
        TrailerInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var trailer = await GetTrailerAsync(input.CompanyId, input.LicensePlate, input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(trailer, cancellationToken);
    }

    public async Task ExecuteJobAsync(
        IOutputBoundaryAdapter<MissionDto?> output,
        JobInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var job = await GetJobAsync(input.CompanyId, input.JobId, input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(job, cancellationToken);
    }

    public async Task ExecuteCityAsync(
        IOutputBoundaryAdapter<CityDto?> output,
        CityInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var city = await GetCityAsync(input.CompanyId, input.CityId, input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(city, cancellationToken);
    }

    public async Task ExecuteRouteAsync(
        IOutputBoundaryAdapter<RouteDto?> output,
        RouteInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var route = await GetRouteAsync(input.CompanyId, input.OriginCityId, input.DestinationCityId, input.Query.ToOptions(), cancellationToken, ToProgress(progress, cancellationToken));
        await output.PresentAsync(route, cancellationToken);
    }

    public async Task<DashboardStatisticsDto> GetDashboardAsync(
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        var statistics = await statisticsService.LoadAsync(cancellationToken, progress);
        statistics = FilterBySourceKey(statistics, options.SourceKey);
        return StatisticsDashboardMapper.ToDashboardDto(
            statistics,
            options.FromDay ?? 0,
            options.ToDay,
            options.Sort,
            options.ExcludePlayerDriver);
    }

    public async Task<IReadOnlyList<CompanyDto>> ListCompaniesAsync(
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        (await GetDashboardAsync(options, cancellationToken, progress)).Companies;

    public async Task<CompanyDto?> GetCompanyAsync(
        string companyId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        FindCompany(await GetDashboardAsync(options, cancellationToken, progress), companyId);

    public async Task<DriverDto?> GetDriverAsync(
        string companyId,
        string driverId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        (await GetCompanyAsync(companyId, options, cancellationToken, progress))
            ?.Drivers
            .FirstOrDefault(driver => IdEquals(driver.Id, driverId));

    public async Task<GarageDto?> GetGarageAsync(
        string companyId,
        string garageId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        (await GetCompanyAsync(companyId, options, cancellationToken, progress))
            ?.Garages
            .FirstOrDefault(garage => IdEquals(garage.Id, garageId));

    public async Task<TruckDto?> GetTruckAsync(
        string companyId,
        string truckId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        (await GetCompanyAsync(companyId, options, cancellationToken, progress))
            ?.Trucks
            .FirstOrDefault(truck => IdEquals(truck.Id, truckId));

    public async Task<TrailerDto?> GetTrailerAsync(
        string companyId,
        string licensePlate,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        (await GetCompanyAsync(companyId, options, cancellationToken, progress))
            ?.Trailers
            ?.FirstOrDefault(trailer => IdEquals(trailer.LicensePlate, licensePlate));

    public async Task<MissionDto?> GetJobAsync(
        string companyId,
        string jobId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        (await GetCompanyAsync(companyId, options, cancellationToken, progress))
            ?.Missions
            .FirstOrDefault(job => IdEquals(job.Id, jobId));

    public async Task<CityDto?> GetCityAsync(
        string companyId,
        string cityId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        (await GetCompanyAsync(companyId, options, cancellationToken, progress))
            ?.Cities
            ?.FirstOrDefault(city => IdEquals(city.Id, cityId));

    public async Task<RouteDto?> GetRouteAsync(
        string companyId,
        string originCityId,
        string destinationCityId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null) =>
        (await GetCompanyAsync(companyId, options, cancellationToken, progress))
            ?.Routes
            ?.FirstOrDefault(route =>
                IdEquals(route.OriginCityId, originCityId) &&
                IdEquals(route.DestinationCityId, destinationCityId));

    private static CompanyDto? FindCompany(DashboardStatisticsDto statistics, string companyId) =>
        statistics.Companies.FirstOrDefault(company => IdEquals(company.Id, companyId));

    private static AtsStatistics FilterBySourceKey(AtsStatistics statistics, string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return statistics;

        var prefix = $"{NormalizeSourceKey(sourceKey)}:";
        return statistics with
        {
            Companies = statistics.Companies
                .Where(company => company.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
    }

    private static string NormalizeSourceKey(string sourceKey)
    {
        var normalized = new string(sourceKey
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? "default" : normalized;
    }

    private static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    private static IProgress<SaveLoadProgress>? ToProgress(
        IProgressOutputBoundaryAdapter? output,
        CancellationToken cancellationToken) =>
        output is null
            ? null
            : ProgressOutputAdapter.ToProgress(output, cancellationToken);
}
