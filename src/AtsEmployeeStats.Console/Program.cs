using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Domain.Statistics;
using AtsEmployeeStats.Infrastructure.Saves;
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

var source = new FileSaveSnapshotSource(saveRoot, TimeSpan.FromDays(options.HistoryDays));
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

internal sealed record CommandLineOptions(string? SaveRoot, bool Once, int HistoryDays, DashboardView View)
{
    public const int DefaultHistoryDays = 5;

    public static CommandLineOptions Parse(string[] args)
    {
        string? saveRoot = null;
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

        return new CommandLineOptions(saveRoot, once, historyDays, view);
    }

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

internal static class DefaultAtsSaveRoot
{
    public static string? Find()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            return null;
        }

        var atsRoot = Path.Combine(documents, "American Truck Simulator");
        var candidates = new[]
        {
            atsRoot,
            Path.Combine(atsRoot, "profiles"),
            Path.Combine(atsRoot, "steam_profiles")
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }
}

internal sealed class TerminalDashboardApp(
    StatisticsService service,
    string saveRoot,
    Func<bool>? canReadKeys = null)
{
    private readonly object _sync = new();
    private readonly Func<bool> _canReadKeys = canReadKeys ?? ConsoleCapabilities.CanReadKeys;
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

        using var watcher = new SaveFileWatcher(saveRoot, async () =>
        {
            await ReloadAsync(cancellationToken);
            Render();
        });

        while (!cancellationToken.IsCancellationRequested)
        {
            Render();
            ConsoleKeyInfo key;
            try
            {
                key = Console.ReadKey(intercept: true);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                break;
            }

            if (key.Key is ConsoleKey.R)
            {
                await ReloadAsync(cancellationToken);
            }
            else if (key.Key is ConsoleKey.LeftArrow)
            {
                _selectedView = _selectedView.Previous();
            }
            else if (key.Key is ConsoleKey.RightArrow)
            {
                _selectedView = _selectedView.Next();
            }
            else if (key.Key is ConsoleKey.UpArrow)
            {
                _selectedCompanyIndex = Math.Max(0, _selectedCompanyIndex - 1);
            }
            else if (key.Key is ConsoleKey.DownArrow)
            {
                _selectedCompanyIndex = Math.Min(Math.Max(0, _statistics.Companies.Count - 1), _selectedCompanyIndex + 1);
            }
        }
    }

    private async Task InitialLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var loaded = await TerminalGuiLoadingScreen.LoadAsync(service, saveRoot, cancellationToken);
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

#pragma warning disable CS0618
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
