using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Infrastructure.Saves;
using Microsoft.Extensions.Logging;

namespace AtsEmployeeStats.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<SqliteMedallionSaveSnapshotSource>(_ =>
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsEmployeeStats");
            Directory.CreateDirectory(dataDirectory);
            var saveRoot = new GameSaveDiscoveryUseCase(new LocalGameSaveDiscovery())
                .FindFirstSaveRootAsync(GameSaveKind.AmericanTruckSimulator, CancellationToken.None)
                .GetAwaiter()
                .GetResult() ?? Environment.CurrentDirectory;
            var databasePath = Path.Combine(dataDirectory, "ats-employee-stats.db");
            var referenceDataOptions = new AtsReferenceDataOptions(
                Enabled: false,
                GameInstallRoot: null,
                CacheRoot: Path.Combine(dataDirectory, "reference-cache"));

            return new SqliteMedallionSaveSnapshotSource(saveRoot, databasePath, referenceDataOptions);
        });
        builder.Services.AddSingleton<ISaveSnapshotSource>(sp =>
            sp.GetRequiredService<SqliteMedallionSaveSnapshotSource>());
        builder.Services.AddSingleton<IStatisticsIngestor>(sp =>
            sp.GetRequiredService<SqliteMedallionSaveSnapshotSource>());
        builder.Services.AddSingleton<StatisticsService>();
        builder.Services.AddSingleton<IStatisticsIngestUseCase, StatisticsIngestUseCase>();
        builder.Services.AddSingleton<IStatisticsDashboardUseCases, StatisticsDashboardUseCases>();
        builder.Services.AddSingleton<IStatisticsReloadUseCase, StatisticsReloadUseCase>();
        builder.Services.AddSingleton<IRecommendNextGarageCityUseCase, RecommendNextGarageCityUseCase>();
        builder.Services.AddSingleton<IRecommendTrailersForGarageUseCase, RecommendTrailersForGarageUseCase>();
        builder.Services.AddSingleton<IRecommendDriverSkillsUseCase, RecommendDriverSkillsUseCase>();
        builder.Services.AddSingleton<IDiagnoseUnderperformersUseCase, DiagnoseUnderperformersUseCase>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
