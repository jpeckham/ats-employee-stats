using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Controllers;
using AtsEmployeeStats.Api.Requests;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Infrastructure.Saves;
using Microsoft.AspNetCore.Mvc;
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
                    ? FindDefaultSaveRoot() ?? Environment.CurrentDirectory
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
        builder.Services.AddSingleton<IStatisticsIngestUseCase, StatisticsIngestUseCase>();
        builder.Services.AddSingleton<IStatisticsDashboardUseCases, StatisticsDashboardUseCases>();
        builder.Services.AddSingleton<IStatisticsReloadUseCase, StatisticsReloadUseCase>();
        builder.Services.AddSingleton<IRecommendNextGarageCityUseCase, RecommendNextGarageCityUseCase>();
        builder.Services.AddSingleton<IRecommendTrailersForGarageUseCase, RecommendTrailersForGarageUseCase>();
        builder.Services.AddSingleton<IRecommendDriverSkillsUseCase, RecommendDriverSkillsUseCase>();
        builder.Services.AddSingleton<IDiagnoseUnderperformersUseCase, DiagnoseUnderperformersUseCase>();
        builder.Services.AddSingleton<IApiRequestMapper, ApiRequestMapper>();
        builder.Services.AddScoped<StatisticsController>();
        builder.Services.AddScoped<CompaniesController>();
        builder.Services.AddScoped<DriversController>();
        builder.Services.AddScoped<GaragesController>();
        builder.Services.AddScoped<TrucksController>();
        builder.Services.AddScoped<TrailersController>();
        builder.Services.AddScoped<JobsController>();
        builder.Services.AddScoped<CitiesController>();
        builder.Services.AddScoped<CompanyPerformanceController>();
        builder.Services.AddHostedService<SaveIngestionService>();

        var app = builder.Build();

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.MapGet("/api/config", (IOptions<StatisticsApiOptions> options) =>
            new DashboardConfigDto(options.Value.SaveRoot, options.Value.HistoryDays));

        app.MapGet("/api/statistics", (
            [AsParameters] StatisticsRouteRequest request,
            StatisticsController controller,
            CancellationToken cancellationToken) =>
            controller.GetStatisticsAsync(request, cancellationToken));

        app.MapGet("/api/companies", (
            [AsParameters] CompaniesRouteRequest request,
            CompaniesController controller,
            CancellationToken cancellationToken) =>
            controller.ListCompaniesAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}", (
            [AsParameters] CompanyRouteRequest request,
            CompaniesController controller,
            CancellationToken cancellationToken) =>
            controller.GetCompanyAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/drivers/{driverId}", (
            [AsParameters] DriverRouteRequest request,
            DriversController controller,
            CancellationToken cancellationToken) =>
            controller.GetDriverAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/garages/{garageId}", (
            [AsParameters] GarageRouteRequest request,
            GaragesController controller,
            CancellationToken cancellationToken) =>
            controller.GetGarageAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/trucks/{truckId}", (
            [AsParameters] TruckRouteRequest request,
            TrucksController controller,
            CancellationToken cancellationToken) =>
            controller.GetTruckAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/trailers/{licensePlate}", (
            [AsParameters] TrailerRouteRequest request,
            TrailersController controller,
            CancellationToken cancellationToken) =>
            controller.GetTrailerAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/jobs/{jobId}", (
            [AsParameters] JobRouteRequest request,
            JobsController controller,
            CancellationToken cancellationToken) =>
            controller.GetJobAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/cities/{cityId}", (
            [AsParameters] CityRouteRequest request,
            CitiesController controller,
            CancellationToken cancellationToken) =>
            controller.GetCityAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/routes/{originCityId}/{destinationCityId}", (
            [AsParameters] RouteRouteRequest request,
            CitiesController controller,
            CancellationToken cancellationToken) =>
            controller.GetRouteAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/recommendations/next-garage-city", (
            [AsParameters] RecommendationRouteRequest request,
            GaragesController controller,
            CancellationToken cancellationToken) =>
            controller.RecommendNextGarageCityAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/garages/{garageId}/recommendations/trailers", (
            [AsParameters] TrailerRecommendationRouteRequest request,
            TrailersController controller,
            CancellationToken cancellationToken) =>
            controller.RecommendTrailersForGarageAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/diagnostics/underperformers", (
            [AsParameters] RecommendationRouteRequest request,
            CompanyPerformanceController controller,
            CancellationToken cancellationToken) =>
            controller.DiagnoseUnderperformersAsync(request, cancellationToken));

        app.MapGet("/api/companies/{companyId}/recommendations/driver-skills", (
            [AsParameters] RecommendationRouteRequest request,
            DriversController controller,
            CancellationToken cancellationToken) =>
            controller.RecommendDriverSkillsAsync(request, cancellationToken));

        app.MapPost("/api/statistics/reload", (
            [AsParameters] StatisticsRouteRequest request,
            StatisticsController controller,
            CancellationToken cancellationToken) =>
            controller.ReloadAsync(request, cancellationToken));

        app.MapHub<StatisticsHub>("/hubs/statistics");
        app.MapFallbackToFile("index.html");

        app.Run();
    }

    private static string? FindDefaultSaveRoot() =>
        new GameSaveDiscoveryUseCase(new LocalGameSaveDiscovery())
            .FindFirstSaveRootAsync(GameSaveKind.AmericanTruckSimulator, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
}
