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
        builder.Services.TryAddSingleton<ISaveSnapshotSource>(services =>
        {
            var options = services.GetRequiredService<IOptions<StatisticsApiOptions>>().Value;
            var referenceDataOptions = new AtsReferenceDataOptions(
                options.ReferenceDataEnabled,
                options.AtsInstallRoot,
                Path.Combine(Path.GetDirectoryName(options.DatabasePath) ?? CommandLineDefaults.DefaultDataDirectory(), "reference-cache"));
            return new SqliteMedallionSaveSnapshotSource(
                options.SaveRoot,
                options.DatabasePath,
                TimeSpan.FromDays(options.HistoryDays),
                referenceDataOptions);
        });
        builder.Services.AddSingleton<StatisticsService>();

        var app = builder.Build();

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.MapGet("/api/config", (IOptions<StatisticsApiOptions> options) =>
            new DashboardConfigDto(options.Value.SaveRoot, options.Value.HistoryDays));

        app.MapGet("/api/statistics", async (
            int? rangeDays,
            StatisticsService service,
            IOptions<StatisticsApiOptions> options,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            var progress = BuildSignalRProgress(hub, cancellationToken);
            var statistics = await service.LoadAsync(cancellationToken, progress);
            return StatisticsDashboardMapper.ToDashboardDto(statistics, rangeDays ?? options.Value.HistoryDays);
        });

        app.MapPost("/api/statistics/reload", async (
            int? rangeDays,
            StatisticsService service,
            IOptions<StatisticsApiOptions> options,
            IHubContext<StatisticsHub> hub,
            CancellationToken cancellationToken) =>
        {
            await hub.Clients.All.SendAsync(
                "StatusChanged",
                new DashboardStatusDto("Reloading saves...", IsError: false),
                cancellationToken);
            var progress = BuildSignalRProgress(hub, cancellationToken);
            var statistics = await service.LoadAsync(cancellationToken, progress);
            var dto = StatisticsDashboardMapper.ToDashboardDto(statistics, rangeDays ?? options.Value.HistoryDays);
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
}
