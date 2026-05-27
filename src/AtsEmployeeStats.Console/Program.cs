using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Domain.Statistics;
using AtsEmployeeStats.Infrastructure.Saves;
using System.Collections.ObjectModel;
using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using GuiApplication = Terminal.Gui.App.Application;

var options = CommandLineOptions.Parse(args);
var saveRoot = options.SaveRoot ?? DefaultAtsSaveRoot.Find();

if (saveRoot is null)
{
    Console.Error.WriteLine("Could not find an ATS save root. Pass --save-root <path>.");
    return 2;
}

var dbPath = options.DbPath ?? CommandLineOptions.DefaultDatabasePath();
var atsInstallRoot = options.AtsInstallRoot ?? DefaultAtsInstallRoot.Find();
var referenceDataOptions = new AtsReferenceDataOptions(
    Enabled: atsInstallRoot is not null,
    GameInstallRoot: atsInstallRoot,
    CacheRoot: Path.Combine(Path.GetDirectoryName(dbPath) ?? CommandLineOptions.DefaultDataDirectory(), "reference-cache"));
var source = new SqliteMedallionSaveSnapshotSource(
    saveRoot,
    dbPath,
    TimeSpan.FromDays(options.HistoryDays),
    referenceDataOptions);
var service = new StatisticsService(source);

if (options.Once)
{
    var statistics = await service.LoadAsync(CancellationToken.None);
    TerminalDashboard.Render(statistics, saveRoot, selectedCompanyIndex: 0, selectedView: options.View);
    return 0;
}

var app = new TerminalDashboardApp(service, saveRoot);
await app.RunAsync(CancellationToken.None);
return 0;

internal sealed record CommandLineOptions(
    string? SaveRoot,
    string? DbPath,
    string? AtsInstallRoot,
    bool Once,
    int HistoryDays,
    DashboardView View)
{
    public const int DefaultHistoryDays = 14;

    public static CommandLineOptions Parse(string[] args)
    {
        string? saveRoot = null;
        string? dbPath = null;
        string? atsInstallRoot = null;
        var once = false;
        var historyDays = DefaultHistoryDays;
        var view = DashboardView.Garages;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--save-root" when i + 1 < args.Length:
                    saveRoot = args[++i];
                    break;
                case "--db-path" when i + 1 < args.Length:
                    dbPath = args[++i];
                    break;
                case "--ats-install-root" when i + 1 < args.Length:
                    atsInstallRoot = args[++i];
                    break;
                case "--once":
                    once = true;
                    break;
                case "--history-days" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedDays):
                    historyDays = Math.Max(1, parsedDays);
                    i++;
                    break;
                case "--view" when i + 1 < args.Length && TryParseView(args[i + 1], out var parsedView):
                    view = parsedView;
                    i++;
                    break;
            }
        }

        return new CommandLineOptions(saveRoot, dbPath, atsInstallRoot, once, historyDays, view);
    }

    public static string DefaultDatabasePath() =>
        Path.Combine(DefaultDataDirectory(), "ats-employee-stats.db");

    public static string DefaultDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsEmployeeStats");

    private static bool TryParseView(string value, out DashboardView view)
    {
        if (Enum.TryParse(value, ignoreCase: true, out view))
        {
            return true;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(value, "trailer-types"))
        {
            view = DashboardView.Trailers;
            return true;
        }

        return false;
    }
}

internal static class DefaultAtsInstallRoot
{
    public static string? Find()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam",
                "steamapps",
                "common",
                "American Truck Simulator")
        };

        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "locale.scs")));
    }
}

internal static class DefaultAtsSaveRoot
{
    private const string AtsAppId = "270880";

    public static string? Find()
    {
        var candidates = new List<string>();

        var steamPath = FindSteamPath();
        if (steamPath is not null)
        {
            var userdataRoot = Path.Combine(steamPath, "userdata");
            if (Directory.Exists(userdataRoot))
            {
                foreach (var userDir in Directory.EnumerateDirectories(userdataRoot))
                {
                    var remote = Path.Combine(userDir, AtsAppId, "remote");
                    candidates.Add(Path.Combine(remote, "profiles"));
                    candidates.Add(Path.Combine(remote, "steam_profiles"));
                    candidates.Add(remote);
                }
            }
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            var atsRoot = Path.Combine(documents, "American Truck Simulator");
            candidates.Add(atsRoot);
            candidates.Add(Path.Combine(atsRoot, "profiles"));
            candidates.Add(Path.Combine(atsRoot, "steam_profiles"));
        }

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? FindSteamPath()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (key?.GetValue("SteamPath") is string regPath &&
                    !string.IsNullOrWhiteSpace(regPath) &&
                    Directory.Exists(regPath))
                {
                    return regPath;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
            }
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam");
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }
}

internal sealed class TerminalDashboardApp(
    StatisticsService service,
    string saveRoot,
    Func<bool>? canReadKeys = null,
    ITerminalDashboardRunner? dashboardRunner = null,
    Func<StatisticsService, string, CancellationToken, Task<LoadedStatistics>>? initialLoad = null)
{
    private readonly object _sync = new();
    private readonly Func<bool> _canReadKeys = canReadKeys ?? ConsoleCapabilities.CanReadKeys;
    private readonly ITerminalDashboardRunner _dashboardRunner = dashboardRunner ?? new TerminalGuiDashboardRunner();
    private readonly Func<StatisticsService, string, CancellationToken, Task<LoadedStatistics>> _initialLoad =
        initialLoad ?? TerminalGuiLoadingScreen.LoadAsync;
    private AtsStatistics _statistics = new(null, []);
    private DashboardView _selectedView = DashboardView.Garages;
    private int _selectedCompanyIndex;
    private string _status = "Loading saves...";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_canReadKeys())
        {
            await ReloadAsync(cancellationToken);
            _status += " Key input is unavailable; run with --once or start from an interactive terminal for live navigation.";
            Render();
            return;
        }

        await InitialLoadAsync(cancellationToken);
        await _dashboardRunner.RunAsync(
            service,
            saveRoot,
            new LoadedStatistics(_statistics, _status),
            cancellationToken);
    }

    private async Task InitialLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var loaded = await _initialLoad(service, saveRoot, cancellationToken);
            lock (_sync)
            {
                _statistics = loaded.Statistics;
                _selectedCompanyIndex = Math.Min(_selectedCompanyIndex, Math.Max(0, loaded.Statistics.Companies.Count - 1));
                _status = loaded.Status;
            }
        }
        catch (Exception)
        {
            await ReloadAsync(cancellationToken);
        }
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var statistics = await service.LoadAsync(cancellationToken);
            lock (_sync)
            {
                _statistics = statistics;
                _selectedCompanyIndex = Math.Min(_selectedCompanyIndex, Math.Max(0, statistics.Companies.Count - 1));
                _status = statistics.Companies.Count == 0
                    ? "No plain-text game.sii saves found. If ATS saves are encrypted, decrypt them first or disable save compression."
                    : $"Loaded {statistics.Companies.Count} trucking compan{(statistics.Companies.Count == 1 ? "y" : "ies")} at {DateTime.Now:t}.";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (_sync)
            {
                _status = $"Read failed: {ex.Message}";
            }
        }
    }

    private void Render()
    {
        lock (_sync)
        {
            TerminalDashboard.Render(_statistics, saveRoot, _selectedCompanyIndex, _selectedView, _status);
        }
    }
}

internal sealed record LoadedStatistics(AtsStatistics Statistics, string Status);

internal interface ITerminalDashboardRunner
{
    Task RunAsync(
        StatisticsService service,
        string saveRoot,
        LoadedStatistics initial,
        CancellationToken cancellationToken);
}

#pragma warning disable CS0618
internal sealed class TerminalGuiDashboardRunner : ITerminalDashboardRunner
{
    public async Task RunAsync(
        StatisticsService service,
        string saveRoot,
        LoadedStatistics initial,
        CancellationToken cancellationToken)
    {
        LoadedStatistics current = initial;
        var reloadRequested = false;
        var navigationRequested = false;
        var state = DrilldownDashboardState.Initial;

        GuiApplication.Init();
        try
        {
            using var watcher = new SaveFileWatcher(saveRoot, async () =>
            {
                current = await ReloadAsync(service, cancellationToken);
                GuiApplication.Invoke(() =>
                {
                    reloadRequested = true;
                    GuiApplication.RequestStop();
                });
            });

            while (!cancellationToken.IsCancellationRequested)
            {
                reloadRequested = false;
                navigationRequested = false;
                using var window = TerminalGuiDashboard.BuildWindow(
                    current.Statistics,
                    saveRoot,
                    current.Status,
                    state,
                    nextState =>
                    {
                        state = nextState;
                        navigationRequested = true;
                        GuiApplication.RequestStop();
                    });
                GuiApplication.Run(window);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (!reloadRequested && !navigationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            GuiApplication.Shutdown();
        }
    }

    private static async Task<LoadedStatistics> ReloadAsync(
        StatisticsService service,
        CancellationToken cancellationToken)
    {
        var statistics = await service.LoadAsync(cancellationToken);
        var status = statistics.Companies.Count == 0
            ? "No plain-text game.sii saves found. If ATS saves are encrypted, decrypt them first or disable save compression."
            : $"Loaded {statistics.Companies.Count} trucking compan{(statistics.Companies.Count == 1 ? "y" : "ies")} at {DateTime.Now:t}.";

        return new LoadedStatistics(statistics, status);
    }
}

internal static class TerminalGuiLoadingScreen
{
    public static async Task<LoadedStatistics> LoadAsync(
        StatisticsService service,
        string saveRoot,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var loadTaskCompletion = new TaskCompletionSource<LoadedStatistics>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadErrorCompletion = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        GuiApplication.Init();
        try
        {
            using var window = new Window
            {
                Title = "ATS Employee Stats - Loading Saves",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            var rootLabel = new Label
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(2),
                Text = $"Save root: {saveRoot}"
            };
            var statusLabel = new Label
            {
                X = 1,
                Y = 3,
                Width = Dim.Fill(2),
                Text = "Discovering game.sii files..."
            };
            var detailLabel = new Label
            {
                X = 1,
                Y = 4,
                Width = Dim.Fill(2),
                Text = "Files: discovering"
            };
            var fileProgressLabel = new Label
            {
                X = 1,
                Y = 6,
                Width = Dim.Fill(2),
                Text = "File progress"
            };
            var fileProgressBar = new ProgressBar
            {
                X = 1,
                Y = 7,
                Width = Dim.Fill(2),
                Height = 1,
                Fraction = 0,
                ProgressBarFormat = ProgressBarFormat.SimplePlusPercentage,
                ProgressBarStyle = ProgressBarStyle.Continuous
            };
            var unitProgressLabel = new Label
            {
                X = 1,
                Y = 9,
                Width = Dim.Fill(2),
                Text = "Current save units"
            };
            var unitProgressBar = new ProgressBar
            {
                X = 1,
                Y = 10,
                Width = Dim.Fill(2),
                Height = 1,
                Fraction = 0,
                ProgressBarFormat = ProgressBarFormat.SimplePlusPercentage,
                ProgressBarStyle = ProgressBarStyle.Continuous
            };

            window.Add(rootLabel, statusLabel, detailLabel, fileProgressLabel, fileProgressBar, unitProgressLabel, unitProgressBar);

            var progress = new Progress<SaveLoadProgress>(update =>
            {
                GuiApplication.Invoke(() =>
                {
                    var elapsed = DateTimeOffset.Now - startedAt;
                    statusLabel.Text = $"{update.Message} Elapsed: {elapsed:mm\\:ss}.";
                    detailLabel.Text = BuildProgressDetail(update);
                    fileProgressLabel.Text = BuildFileProgressText(update);
                    unitProgressLabel.Text = BuildCurrentUnitProgressText(update);
                    SetFileProgress(fileProgressBar, update);
                    SetCurrentUnitProgress(unitProgressBar, update);
                    GuiApplication.LayoutAndDraw();
                });
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    var statistics = await service.LoadAsync(cancellationToken, progress);
                    var elapsed = DateTimeOffset.Now - startedAt;
                    var status = statistics.Companies.Count == 0
                        ? $"No plain-text game.sii saves found after {elapsed:mm\\:ss}."
                        : $"Loaded {statistics.Companies.Count} trucking compan{(statistics.Companies.Count == 1 ? "y" : "ies")} in {elapsed:mm\\:ss}.";
                    loadTaskCompletion.SetResult(new LoadedStatistics(statistics, status));
                    GuiApplication.Invoke(() => GuiApplication.RequestStop());
                }
                catch (Exception ex)
                {
                    loadErrorCompletion.SetResult(ex);
                    GuiApplication.Invoke(() => GuiApplication.RequestStop());
                }
            }, cancellationToken);

            GuiApplication.Run(window);

            if (loadErrorCompletion.Task.IsCompletedSuccessfully)
            {
                throw loadErrorCompletion.Task.Result;
            }

            return await loadTaskCompletion.Task;
        }
        finally
        {
            GuiApplication.Shutdown();
        }
    }

    private static string BuildProgressDetail(SaveLoadProgress update)
    {
        var filePart = update.TotalFiles > 0
            ? $"Files: {update.CompletedFiles:N0}/{update.TotalFiles:N0}"
            : "Files: discovering";
        var unitPart = update.CurrentFileTotalUnits > 0
            ? $"Current save units: {update.CurrentFileCompletedUnits:N0}/{update.CurrentFileTotalUnits:N0}"
            : "Current save units: waiting for parser";

        return $"{filePart} | {unitPart}";
    }

    private static string BuildFileProgressText(SaveLoadProgress update) =>
        update.TotalFiles > 0
            ? $"File progress: {update.CompletedFiles:N0} of {update.TotalFiles:N0}"
            : "File progress: discovering saves";

    private static string BuildCurrentUnitProgressText(SaveLoadProgress update) =>
        update.CurrentFileTotalUnits > 0
            ? $"Current save units: {update.CurrentFileCompletedUnits:N0} of {update.CurrentFileTotalUnits:N0}"
            : "Current save units: waiting for current save";

    private static void SetFileProgress(ProgressBar progressBar, SaveLoadProgress update)
    {
        if (update.TotalFiles > 0)
        {
            progressBar.Fraction = Math.Clamp((float)update.CompletedFiles / update.TotalFiles, 0, 1);
        }
        else
        {
            progressBar.Pulse();
        }
    }

    private static void SetCurrentUnitProgress(ProgressBar progressBar, SaveLoadProgress update)
    {
        if (update.CurrentFileTotalUnits > 0)
        {
            progressBar.Fraction = Math.Clamp((float)update.CurrentFileCompletedUnits / update.CurrentFileTotalUnits, 0, 1);
        }
        else
        {
            progressBar.Pulse();
        }
    }
}
#pragma warning restore CS0618

internal static class ConsoleCapabilities
{
    public static bool CanReadKeys()
    {
        try
        {
            return !Console.IsInputRedirected;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

internal static class TerminalGuiDashboard
{
    public static Window BuildWindow(AtsStatistics statistics, string saveRoot, string status) =>
        BuildWindow(statistics, saveRoot, status, DrilldownDashboardState.Initial);

    public static Window BuildWindow(
        AtsStatistics statistics,
        string saveRoot,
        string status,
        DrilldownDashboardState state) =>
        BuildWindow(statistics, saveRoot, status, state, onStateChanged: null);

    public static Window BuildWindow(
        AtsStatistics statistics,
        string saveRoot,
        string status,
        DrilldownDashboardState state,
        Action<DrilldownDashboardState>? onStateChanged)
    {
        var window = new Window
        {
            Title = $"ATS Employee Stats - {ScreenTitle(state.Screen)}",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var menu = new MenuBar();
        var header = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Text = $"Save root: {saveRoot}"
        };
        var statusLabel = new Label
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(2),
            Text = status
        };
        var rangeSelector = new ListView
        {
            Title = "Time Range",
            X = 1,
            Y = 4,
            Width = 24,
            Height = 4,
            SelectedItem = state.RangeDays == 7 ? 1 : 0
        };
        rangeSelector.SetSource(new ObservableCollection<string>(["Last 14 days", "Last 7 days"]));
        rangeSelector.ValueChanged += (_, args) =>
        {
            if (args.NewValue is null)
            {
                return;
            }

            onStateChanged?.Invoke(state.SelectRangeDays(args.NewValue == 1 ? 7 : 14));
        };

        window.Add(menu, header, statusLabel, rangeSelector);
        foreach (var view in BuildScreenViews(statistics, state, onStateChanged))
        {
            window.Add(view);
        }

        return window;
    }

    private static TableView BuildTable(string title, Pos x, Pos y, Dim width, Dim height) =>
        new()
        {
            Title = title,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

    private static IEnumerable<View> BuildScreenViews(
        AtsStatistics statistics,
        DrilldownDashboardState state,
        Action<DrilldownDashboardState>? onStateChanged)
    {
        var selectedCompany = SelectCompany(statistics, state.CompanyId);
        return state.Screen switch
        {
            DrilldownDashboardScreen.Companies => [BuildCompaniesTable(statistics, state, onStateChanged)],
            DrilldownDashboardScreen.Garages => [BuildGaragesTable(selectedCompany, state, onStateChanged)],
            DrilldownDashboardScreen.Drivers => [BuildDriversTable(selectedCompany, state, onStateChanged)],
            DrilldownDashboardScreen.DriverJobs => BuildDriverJobViews(selectedCompany, state),
            _ => []
        };
    }

    private static TableView BuildCompaniesTable(
        AtsStatistics statistics,
        DrilldownDashboardState state,
        Action<DrilldownDashboardState>? onStateChanged)
    {
        var table = BuildTable("Trucking Companies", 27, 4, Dim.Fill(1), Dim.Fill(1));
        var companies = statistics.Companies.ToList();
        table.Table = TableSource(["Company", "Profit", "$/Day", "Garages", "Drivers", "Trucks"], companies.Select(company => new object[]
        {
            company.DisplayName,
            Money(company.Garages.Sum(garage => garage.Profit)),
            MoneyPerDay(company.Garages.Sum(garage => garage.Profit), state.RangeDays),
            company.Garages.Count.ToString(),
            company.Drivers.Count.ToString(),
            company.Trucks.Count.ToString()
        }));
        table.ValueChanged += (_, args) =>
        {
            var row = args.NewValue?.SelectedCell.Y;
            if (row is >= 0 && row < companies.Count)
            {
                onStateChanged?.Invoke(state.SelectCompany(companies[row.Value].Id));
            }
        };
        return table;
    }

    private static TableView BuildGaragesTable(
        CompanyStatistics? company,
        DrilldownDashboardState state,
        Action<DrilldownDashboardState>? onStateChanged)
    {
        var table = BuildTable("Garages", 27, 4, Dim.Fill(1), Dim.Fill(1));
        var garages = company?.Garages.ToList() ?? [];
        table.Table = TableSource(["Garage", "Profit", "$/Day", "Drivers", "Trucks"], garages.Select(garage => new object[]
        {
            garage.DisplayName,
            Money(garage.Profit),
            MoneyPerDay(garage.Profit, state.RangeDays),
            garage.EmployeeCount.ToString(),
            garage.TruckCount.ToString()
        }));
        table.ValueChanged += (_, args) =>
        {
            var row = args.NewValue?.SelectedCell.Y;
            if (row is >= 0 && row < garages.Count)
            {
                onStateChanged?.Invoke(state.SelectGarage(garages[row.Value].Id));
            }
        };
        return table;
    }

    private static TableView BuildDriversTable(
        CompanyStatistics? company,
        DrilldownDashboardState state,
        Action<DrilldownDashboardState>? onStateChanged)
    {
        var table = BuildTable("Drivers", 27, 4, Dim.Fill(1), Dim.Fill(1));
        var drivers = SelectedGarageDrivers(company, state.GarageId).ToList();
        table.Table = TableSource(["Driver", "Profit", "$/Day", "Truck", "Jobs"], drivers.Select(driver => new object[]
        {
            driver.DisplayName,
            Money(driver.Profit),
            MoneyPerDay(driver.Profit, state.RangeDays),
            driver.TruckId ?? string.Empty,
            company?.Missions.Count(mission => StringComparer.OrdinalIgnoreCase.Equals(mission.DriverId, driver.Id)).ToString() ?? "0"
        }));
        table.ValueChanged += (_, args) =>
        {
            var row = args.NewValue?.SelectedCell.Y;
            if (row is >= 0 && row < drivers.Count)
            {
                onStateChanged?.Invoke(state.SelectDriver(drivers[row.Value].Id));
            }
        };
        return table;
    }

    private static IEnumerable<View> BuildDriverJobViews(CompanyStatistics? company, DrilldownDashboardState state)
    {
        var driverMissions = SelectedDriverMissions(company, state.DriverId).ToList();

        var jobTypes = BuildTable("Job Types", 27, 4, Dim.Percent(45), 8);
        jobTypes.Table = TableSource(["Job Type", "Jobs", "Profit", "$/Day"], driverMissions
            .GroupBy(mission => mission.TrailerType ?? "unknown")
            .OrderByDescending(group => group.Sum(mission => mission.Profit))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new object[]
            {
                group.Key,
                group.Count().ToString(),
                Money(group.Sum(mission => mission.Profit)),
                MoneyPerDay(group.Sum(mission => mission.Profit), state.RangeDays)
            }));

        var pairs = BuildTable("Job Pairs", 27 + Pos.Percent(45), 4, Dim.Fill(1), 8);
        pairs.Table = TableSource(["Route Pair", "Jobs", "Profit", "$/Day"], BuildRoutePairRows(driverMissions, state.RangeDays));

        var jobs = BuildTable("Jobs", 27, 12, Dim.Fill(1), Dim.Fill(1));
        jobs.Table = TableSource(["Job", "Origin", "Destination", "Cargo", "Truck", "Profit"], driverMissions
            .OrderByDescending(mission => mission.Profit)
            .ThenBy(mission => mission.Id, StringComparer.OrdinalIgnoreCase)
            .Select(mission => new object[]
            {
                mission.Id,
                mission.SourceCity ?? string.Empty,
                mission.TargetCity ?? string.Empty,
                mission.Cargo ?? string.Empty,
                mission.TruckId ?? string.Empty,
                Money(mission.Profit)
            }));

        return [jobTypes, pairs, jobs];
    }

    private static IEnumerable<object[]> BuildRoutePairRows(IReadOnlyCollection<MissionStatistic> missions, int rangeDays) =>
        missions
            .Where(mission => !string.IsNullOrWhiteSpace(mission.SourceCity) && !string.IsNullOrWhiteSpace(mission.TargetCity))
            .GroupBy(mission => BuildRoutePair(mission.SourceCity!, mission.TargetCity!))
            .OrderByDescending(group => group.Sum(mission => mission.Profit))
            .ThenBy(group => group.Key.RoutePair, StringComparer.OrdinalIgnoreCase)
            .Select(group => new object[]
            {
                group.Key.RoutePair,
                group.Count().ToString(),
                Money(group.Sum(mission => mission.Profit)),
                MoneyPerDay(group.Sum(mission => mission.Profit), rangeDays)
            });

    private static CompanyStatistics? SelectCompany(AtsStatistics statistics, string? companyId) =>
        statistics.Companies.FirstOrDefault(company => StringComparer.OrdinalIgnoreCase.Equals(company.Id, companyId)) ??
        statistics.Companies.FirstOrDefault();

    private static IEnumerable<DriverStatistic> SelectedGarageDrivers(CompanyStatistics? company, string? garageId) =>
        (company?.Drivers
            .Where(driver => StringComparer.OrdinalIgnoreCase.Equals(driver.GarageId, garageId ?? company.Garages.FirstOrDefault()?.Id))
            .OrderByDescending(driver => driver.Profit)
            .ThenBy(driver => driver.DisplayName, StringComparer.OrdinalIgnoreCase)) ?? Enumerable.Empty<DriverStatistic>();

    private static IEnumerable<MissionStatistic> SelectedDriverMissions(CompanyStatistics? company, string? driverId) =>
        (company?.Missions
            .Where(mission => StringComparer.OrdinalIgnoreCase.Equals(mission.DriverId, driverId ?? company.Drivers.FirstOrDefault()?.Id))) ?? Enumerable.Empty<MissionStatistic>();

    private static RoutePairDisplay BuildRoutePair(string sourceCity, string targetCity)
    {
        var endpoints = new[] { sourceCity, targetCity }
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(FormatRouteEndpoint)
            .ToArray();
        return new RoutePairDisplay($"{endpoints[0]} <-> {endpoints[1]}");
    }

    private static string FormatRouteEndpoint(string value) =>
        string.Join(' ', value
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static string Money(long value) =>
        string.Create(CultureInfo.InvariantCulture, $"${value:N0}");

    private static string MoneyPerDay(long value, int rangeDays) =>
        Money((long)Math.Round(value / (decimal)Math.Max(1, rangeDays), MidpointRounding.AwayFromZero));

    private static string ScreenTitle(DrilldownDashboardScreen screen) =>
        screen switch
        {
            DrilldownDashboardScreen.Companies => "Trucking Companies",
            DrilldownDashboardScreen.Garages => "Garages",
            DrilldownDashboardScreen.Drivers => "Drivers",
            DrilldownDashboardScreen.DriverJobs => "Driver Jobs",
            _ => "Dashboard"
        };

    private static DataTableSource TableSource(string[] columns, IEnumerable<object[]> rows)
    {
        var table = new System.Data.DataTable();
        foreach (var column in columns)
        {
            table.Columns.Add(column);
        }

        foreach (var row in rows)
        {
            table.Rows.Add(row);
        }

        return new DataTableSource(table);
    }
}

internal enum DrilldownDashboardScreen
{
    Companies,
    Garages,
    Drivers,
    DriverJobs
}

internal sealed record DrilldownDashboardState(
    DrilldownDashboardScreen Screen,
    int RangeDays = 14,
    string? CompanyId = null,
    string? GarageId = null,
    string? DriverId = null)
{
    public static DrilldownDashboardState Initial { get; } = new(DrilldownDashboardScreen.Companies);

    public DrilldownDashboardState SelectCompany(string companyId) =>
        this with
        {
            Screen = DrilldownDashboardScreen.Garages,
            CompanyId = companyId,
            GarageId = null,
            DriverId = null
        };

    public DrilldownDashboardState SelectGarage(string garageId) =>
        this with
        {
            Screen = DrilldownDashboardScreen.Drivers,
            GarageId = garageId,
            DriverId = null
        };

    public DrilldownDashboardState SelectDriver(string driverId) =>
        this with
        {
            Screen = DrilldownDashboardScreen.DriverJobs,
            DriverId = driverId
        };

    public DrilldownDashboardState SelectRangeDays(int rangeDays) =>
        this with { RangeDays = rangeDays == 7 ? 7 : 14 };
}

internal sealed record RoutePairDisplay(string RoutePair);

internal enum DashboardView
{
    Garages,
    Drivers,
    Trailers,
    Trucks,
    Missions
}

internal static class DashboardViewExtensions
{
    public static DashboardView Next(this DashboardView view) =>
        view == DashboardView.Missions ? DashboardView.Garages : view + 1;

    public static DashboardView Previous(this DashboardView view) =>
        view == DashboardView.Garages ? DashboardView.Missions : view - 1;
}

internal static class TerminalDashboard
{
    public static void Render(
        AtsStatistics statistics,
        string saveRoot,
        int selectedCompanyIndex,
        DashboardView selectedView,
        string status = "")
    {
        SafeClear();
        Console.WriteLine("ATS Employee Stats");
        Console.WriteLine($"Save root: {saveRoot}");
        Console.WriteLine(string.IsNullOrWhiteSpace(status) ? $"Last update: {statistics.LastUpdated?.LocalDateTime:g}" : status);
        Console.WriteLine();

        if (statistics.Companies.Count == 0)
        {
            Console.WriteLine("No companies loaded.");
            Console.WriteLine("Keys: r refresh, q quit");
            return;
        }

        selectedCompanyIndex = Math.Clamp(selectedCompanyIndex, 0, statistics.Companies.Count - 1);
        var company = statistics.Companies[selectedCompanyIndex];
        Console.WriteLine("Companies");
        for (var i = 0; i < statistics.Companies.Count; i++)
        {
            var marker = i == selectedCompanyIndex ? ">" : " ";
            var candidate = statistics.Companies[i];
            var profit = candidate.Garages.Sum(garage => garage.Profit);
            Console.WriteLine($"{marker} {candidate.DisplayName,-32} {profit,14:C0}");
        }

        Console.WriteLine();
        Console.WriteLine($"View: {selectedView} | Keys: left/right view, up/down company, r refresh, q quit");
        Console.WriteLine(new string('-', Math.Min(SafeWindowWidth() - 1, 120)));

        foreach (var line in BuildRows(company, selectedView).Take(25))
        {
            Console.WriteLine(line);
        }
    }

    private static IEnumerable<string> BuildRows(CompanyStatistics company, DashboardView selectedView) =>
        selectedView switch
        {
            DashboardView.Garages => company.Garages.Select(garage =>
                $"{garage.DisplayName,-32} {garage.Profit,14:C0}  employees {garage.EmployeeCount,3}  trucks {garage.TruckCount,3}"),
            DashboardView.Drivers => company.Drivers.Select(driver =>
                $"{driver.DisplayName,-32} {driver.Profit,14:C0}  garage {driver.GarageId ?? "-",-24} truck {driver.TruckId ?? "-"}"),
            DashboardView.Trailers => company.TrailerTypes.Select(trailer =>
                $"{trailer.Id,-48} {trailer.Profit,14:C0}  missions {trailer.MissionCount,4}"),
            DashboardView.Trucks => company.Trucks.Select(truck =>
                $"{truck.DisplayName,-32} {truck.Profit,14:C0}  garage {truck.GarageId ?? "-",-24} driver {truck.DriverId ?? "-"}"),
            DashboardView.Missions => company.Missions.Select(mission =>
                $"{mission.Profit,14:C0}  {mission.SourceCity ?? "?",-16} -> {mission.TargetCity ?? "?",-16} trailer {mission.TrailerType ?? "-"} cargo {mission.Cargo ?? "-"}"),
            _ => []
        };

    private static void SafeClear()
    {
        try
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }
        }
        catch (IOException)
        {
            // Some hosts report non-redirected output but do not expose a console buffer.
        }
    }

    private static int SafeWindowWidth()
    {
        try
        {
            return Console.WindowWidth > 0 ? Console.WindowWidth : 100;
        }
        catch (IOException)
        {
            return 100;
        }
    }
}
