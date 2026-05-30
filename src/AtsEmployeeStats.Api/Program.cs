using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Infrastructure.Saves;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AtsEmployeeStats.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseStaticWebAssets();

        builder.Services.AddSignalR();
        builder.Services.AddOptions<StatisticsApiOptions>()
            .Bind(builder.Configuration.GetSection("Statistics"))
            .PostConfigure(options =>
            {
                options.SaveRoot = string.IsNullOrWhiteSpace(options.SaveRoot)
                    ? DefaultAtsSaveRoot.Find() ?? Environment.CurrentDirectory
                    : options.SaveRoot;
                options.DatabasePath = string.IsNullOrWhiteSpace(options.DatabasePath)
                    ? Path.Combine(CommandLineDefaults.DefaultDataDirectory(), "ats-employee-stats.db")
                    : options.DatabasePath;
                options.HistoryDays = Math.Max(1, options.HistoryDays);
            });
        builder.Services.TryAddSingleton<SqliteMedallionSaveSnapshotSource>(services =>
        {
            var options = services.GetRequiredService<IOptions<StatisticsApiOptions>>().Value;
            var referenceDataOptions = new AtsReferenceDataOptions(
                options.ReferenceDataEnabled,
                options.AtsInstallRoot,
                Path.Combine(Path.GetDirectoryName(options.DatabasePath) ?? CommandLineDefaults.DefaultDataDirectory(), "reference-cache"));
            return new SqliteMedallionSaveSnapshotSource(
                options.SaveRoot,
                options.DatabasePath,
                referenceDataOptions);
        });
        builder.Services.TryAddSingleton<ISaveSnapshotSource>(sp =>
            sp.GetRequiredService<SqliteMedallionSaveSnapshotSource>());
        builder.Services.TryAddSingleton<IStatisticsIngestor>(sp =>
            sp.GetRequiredService<SqliteMedallionSaveSnapshotSource>());
        builder.Services.AddSingleton<StatisticsService>();
        builder.Services.AddHostedService<SaveIngestionService>();

        var app = builder.Build();

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.MapGet("/api/config", (IOptions<StatisticsApiOptions> options) =>
            new DashboardConfigDto(options.Value.SaveRoot, options.Value.HistoryDays));

        app.MapGet("/api/statistics", async (
            int? fromDay,
            int? toDay,
            [AsParameters] CollectionSortDto sort,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var progress = BuildSignalRProgress(hub, cancellationToken);
            var statistics = await service.LoadAsync(cancellationToken, progress);
            return StatisticsDashboardMapper.ToDashboardDto(statistics, fromDay ?? 0, toDay, sort);
        });

        app.MapGet("/api/companies", async (
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var dto = await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken);
            return Results.Ok(dto.Companies);
        });

        app.MapGet("/api/companies/{companyId}", async (
            string companyId,
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var dto = await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken);
            var company = FindCompany(dto, companyId);
            return company is null ? Results.NotFound() : Results.Ok(company);
        });

        app.MapGet("/api/companies/{companyId}/drivers/{driverId}", async (
            string companyId,
            string driverId,
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
            var driver = company?.Drivers.FirstOrDefault(driver => IdEquals(driver.Id, driverId));
            return driver is null ? Results.NotFound() : Results.Ok(driver);
        });

        app.MapGet("/api/companies/{companyId}/garages/{garageId}", async (
            string companyId,
            string garageId,
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
            var garage = company?.Garages.FirstOrDefault(garage => IdEquals(garage.Id, garageId));
            return garage is null ? Results.NotFound() : Results.Ok(garage);
        });

        app.MapGet("/api/companies/{companyId}/trucks/{truckId}", async (
            string companyId,
            string truckId,
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
            var truck = company?.Trucks.FirstOrDefault(truck => IdEquals(truck.Id, truckId));
            return truck is null ? Results.NotFound() : Results.Ok(truck);
        });

        app.MapGet("/api/companies/{companyId}/trailers/{licensePlate}", async (
            string companyId,
            string licensePlate,
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
            var trailer = company?.Trailers?.FirstOrDefault(trailer => IdEquals(trailer.LicensePlate, licensePlate));
            return trailer is null ? Results.NotFound() : Results.Ok(trailer);
        });

        app.MapGet("/api/companies/{companyId}/jobs/{jobId}", async (
            string companyId,
            string jobId,
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
            var job = company?.Missions.FirstOrDefault(job => IdEquals(job.Id, jobId));
            return job is null ? Results.NotFound() : Results.Ok(job);
        });

        app.MapGet("/api/companies/{companyId}/cities/{cityId}", async (
            string companyId,
            string cityId,
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
            var city = company?.Cities?.FirstOrDefault(city => IdEquals(city.Id, cityId));
            return city is null ? Results.NotFound() : Results.Ok(city);
        });

        app.MapGet("/api/companies/{companyId}/routes/{originCityId}/{destinationCityId}", async (
            string companyId,
            string originCityId,
            string destinationCityId,
            int? fromDay,
            int? toDay,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
            var route = company?.Routes?.FirstOrDefault(route =>
                IdEquals(route.OriginCityId, originCityId) &&
                IdEquals(route.DestinationCityId, destinationCityId));
            return route is null ? Results.NotFound() : Results.Ok(route);
        });

        app.MapPost("/api/statistics/reload", async (
            int? fromDay,
            int? toDay,
            [AsParameters] CollectionSortDto sort,
            StatisticsService service,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            await hub.Clients.All.SendAsync(
                "StatusChanged",
                new DashboardStatusDto("Reloading saves...", IsError: false),
                cancellationToken);
            var progress = BuildSignalRProgress(hub, cancellationToken);
            await service.IngestAsync(cancellationToken, progress, force: true);
            var statistics = await service.LoadAsync(cancellationToken, progress);
            var dto = StatisticsDashboardMapper.ToDashboardDto(statistics, fromDay ?? 0, toDay, sort);
            await hub.Clients.All.SendAsync("StatisticsUpdated", dto, cancellationToken);
            return Results.Ok(dto);
        });

        app.MapHub<StatisticsHub>("/hubs/statistics");
        app.MapFallbackToFile("index.html");

        app.Run();
    }

    private static Progress<SaveLoadProgress> BuildSignalRProgress(
        IHubContext<StatisticsHub> hub,
        CancellationToken cancellationToken) =>
        new(update =>
        {
            _ = hub.Clients.All.SendAsync(
                "LoadingProgress",
                DashboardProgressMapper.ToDashboardProgressDto(update),
                cancellationToken);
        });

    private static async Task<DashboardStatisticsDto> LoadDashboardAsync(
        int? fromDay,
        int? toDay,
        StatisticsService service,
        IHubContext<StatisticsHub> hub,
        CancellationToken cancellationToken,
        CollectionSortDto? sort = null)
    {
        var progress = BuildSignalRProgress(hub, cancellationToken);
        var statistics = await service.LoadAsync(cancellationToken, progress);
        return StatisticsDashboardMapper.ToDashboardDto(statistics, fromDay ?? 0, toDay, sort);
    }

    private static CompanyDto? FindCompany(DashboardStatisticsDto statistics, string companyId) =>
        statistics.Companies.FirstOrDefault(company => IdEquals(company.Id, companyId));

    private static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);
}
