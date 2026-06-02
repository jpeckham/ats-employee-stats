using System.Windows;
using System.IO;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Infrastructure.Saves;
using AtsEmployeeStats.Wpf.ViewModels;
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
        services.AddSingleton<SqliteMedallionSaveSnapshotSource>(_ =>
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
        services.AddSingleton<ISaveSnapshotSource>(sp => sp.GetRequiredService<SqliteMedallionSaveSnapshotSource>());
        services.AddSingleton<IStatisticsIngestor>(sp => sp.GetRequiredService<SqliteMedallionSaveSnapshotSource>());
        services.AddSingleton<StatisticsService>();
        services.AddSingleton<IStatisticsIngestUseCase, StatisticsIngestUseCase>();
        services.AddSingleton<IStatisticsDashboardUseCases, StatisticsDashboardUseCases>();
        services.AddSingleton<IStatisticsReloadUseCase, StatisticsReloadUseCase>();
        services.AddSingleton<IRecommendNextGarageCityUseCase, RecommendNextGarageCityUseCase>();
        services.AddSingleton<IRecommendTrailersForGarageUseCase, RecommendTrailersForGarageUseCase>();
        services.AddSingleton<IRecommendDriverSkillsUseCase, RecommendDriverSkillsUseCase>();
        services.AddSingleton<IDiagnoseUnderperformersUseCase, DiagnoseUnderperformersUseCase>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
