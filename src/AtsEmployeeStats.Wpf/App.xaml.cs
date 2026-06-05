using System.Windows;
using System.IO;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Infrastructure.Saves;
using AtsEmployeeStats.Wpf.Controllers;
using AtsEmployeeStats.Wpf.Services;
using AtsEmployeeStats.Wpf.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AtsEmployeeStats.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = ConfigureServices().BuildServiceProvider();
        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameSourceDiscovery, LocalGameSourceDiscovery>();
        services.AddSingleton<IGameSourceSettingsStore>(_ => SqliteGameSourceSettingsStore.CreateDefault());
        services.AddSingleton<GameSourceManagementUseCase>();
        services.AddSingleton<IConfiguredGameSaveDiscovery, LocalConfiguredGameSaveDiscovery>();
        services.AddSingleton<GameSaveCatalogUseCase>();
        services.AddSingleton<IBackgroundRunner, TaskBackgroundRunner>();
        services.AddSingleton<IDatabaseDiskSpaceService>(_ => LocalDatabaseDiskSpaceService.CreateDefault());
        services.AddSingleton<ISourceWizardConfirmation, SourceWizardConfirmation>();
        services.AddSingleton<GameSourcePresenter>();
        services.AddSingleton<ISaveSnapshotSource>(sp =>
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsEmployeeStats");
            Directory.CreateDirectory(dataDirectory);
            var databasePath = Path.Combine(dataDirectory, "ats-employee-stats.db");
            return new DynamicConfiguredSaveSnapshotSource(
                loadSources: cancellationToken => sp.GetRequiredService<GameSourceManagementUseCase>()
                    .DiscoverAsync(cancellationToken),
                createSource: source =>
                {
                    var referenceDataOptions = new AtsReferenceDataOptions(
                        Enabled: true,
                        GameInstallRoot: source.InstallPath,
                        CacheRoot: Path.Combine(dataDirectory, "reference-cache", source.Game.ToString()));
                    var saveRoot = string.IsNullOrWhiteSpace(source.SavePath)
                        ? Environment.CurrentDirectory
                        : source.SavePath;
                    return new SqliteMedallionSaveSnapshotSource(
                        saveRoot,
                        databasePath,
                        referenceDataOptions,
                        sourceKeyPrefix: $"{source.Game}:{saveRoot}");
                });
        });
        services.AddSingleton<IStatisticsIngestor>(sp => (IStatisticsIngestor)sp.GetRequiredService<ISaveSnapshotSource>());
        services.AddSingleton<StatisticsService>();
        services.AddSingleton<IStatisticsIngestUseCase, StatisticsIngestUseCase>();
        services.AddSingleton<IStatisticsDashboardUseCases, StatisticsDashboardUseCases>();
        services.AddSingleton<IStatisticsReloadUseCase, StatisticsReloadUseCase>();
        services.AddSingleton<IRecommendNextGarageCityUseCase, RecommendNextGarageCityUseCase>();
        services.AddSingleton<IRecommendTrailersForGarageUseCase, RecommendTrailersForGarageUseCase>();
        services.AddSingleton<IRecommendDriverSkillsUseCase, RecommendDriverSkillsUseCase>();
        services.AddSingleton<IDiagnoseUnderperformersUseCase, DiagnoseUnderperformersUseCase>();
        services.AddSingleton<ExplorerPresenter>();
        services.AddSingleton<DashboardPresenter>();
        services.AddSingleton<MainWindowPresenter>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
