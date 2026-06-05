using System.Collections.ObjectModel;
using System.IO;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Wpf.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static AtsEmployeeStats.Wpf.ViewModels.DetailHelpers;

namespace AtsEmployeeStats.Wpf.Controllers;

public sealed partial class MainWindowPresenter(
    IStatisticsDashboardUseCases dashboardUseCases,
    IStatisticsReloadUseCase reloadUseCase,
    GameSourceManagementUseCase gameSourceManagement,
    GameSaveCatalogUseCase gameSaveCatalog) : ObservableObject
{
    private readonly ExplorerPresenter _explorerPresenter = new();
    private DashboardQueryRequest _query = new();
    private DashboardStatisticsDto? _dashboard;
    private ExplorerNodeViewModel? _selectedNode;
    private string? _activeTabTitle;

    [ObservableProperty]
    private CompanyExplorerViewModel explorer = new();

    [ObservableProperty]
    private EntityDetailViewModel? selectedDetail;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isSourceWizardVisible;

    [ObservableProperty]
    private bool isEmptyStateVisible = true;

    [ObservableProperty]
    private bool isExplorerVisible;

    [ObservableProperty]
    private bool isLoadProgressVisible;

    [ObservableProperty]
    private bool excludePlayerDriver;

    [ObservableProperty]
    private double saveFileProgressValue;

    [ObservableProperty]
    private double saveContentProgressValue;

    [ObservableProperty]
    private string saveFileProgressText = string.Empty;

    [ObservableProperty]
    private string saveContentProgressText = string.Empty;

    [ObservableProperty]
    private int currentWizardIndex;

    [ObservableProperty]
    private GameSourceWizardGameViewModel? currentWizardGame;

    public ObservableCollection<GameSourceRowViewModel> GameSources { get; } = [];

    public ObservableCollection<GameSaveRowViewModel> GameSaves { get; } = [];

    public ObservableCollection<GameSourceWizardGameViewModel> SourceWizardGames { get; } = [];

    public bool CanReloadSaves =>
        !IsBusy && GameSources.Any(source => source.Enabled && source.SavePath.Length > 0);

    public bool CanRefreshDashboard =>
        !IsBusy && _dashboard is not null;

    public string SourceWizardStepText =>
        SourceWizardGames.Count == 0 ? string.Empty : $"Step {CurrentWizardIndex + 1:N0} of {SourceWizardGames.Count:N0}";

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefreshDashboard));
        OnPropertyChanged(nameof(CanReloadSaves));
        RefreshCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
    }

    partial void OnExcludePlayerDriverChanged(bool value)
    {
        _query = _query with { ExcludePlayerDriver = value };
        if (_dashboard is not null && !IsBusy)
            _ = RefreshAsync();
    }

    public async Task LoadAsync()
    {
        await Task.Yield();
        await LoadGameSourcesAsync();
        if (await Task.Run(() => gameSourceManagement.RequiresWizardAsync(CancellationToken.None)))
        {
            IsEmptyStateVisible = true;
            IsExplorerVisible = false;
            await StartSourceWizardAsync();
        }
        else if (_dashboard is null)
        {
            await SyncOnStartupAsync();
        }
    }

    private async Task SyncOnStartupAsync()
    {
        try
        {
            IsBusy = true;
            IsLoadProgressVisible = true;
            ResetLoadProgress();
            StatusText = "Checking for new save files...";
            var progress = new Progress<SaveLoadProgress>(ApplyLoadProgress);
            _dashboard = await Task.Run(() => reloadUseCase.SyncAsync(_query.ToOptions(), CancellationToken.None, progress));
            BuildExplorer(_dashboard.Companies);
            SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);
            UpdateNavigationState();
            StatusText = $"Loaded {_dashboard.Companies.Count:N0} companies";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to load statistics: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsLoadProgressVisible = false;
        }
    }

    [RelayCommand]
    private async Task ManageSourcesAsync()
    {
        await StartSourceWizardAsync();
    }

    [RelayCommand]
    private async Task StartSourceWizardAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusText = "Searching for ATS and ETS2 sources...";
            var wizardGames = await Task.Run(async () =>
            {
                var rows = GameSources.ToList();
                var games = new List<GameSourceWizardGameViewModel>();
                foreach (var game in new[] { GameType.Ats, GameType.Ets2 })
                {
                    var candidates = await gameSourceManagement.DiscoverCandidatesAsync(game, CancellationToken.None);
                    var existing = rows.FirstOrDefault(source => string.Equals(source.GameKey, game.ToString(), StringComparison.OrdinalIgnoreCase));
                    games.Add(new GameSourceWizardGameViewModel(
                        game.ToString(),
                        game == GameType.Ats ? "ATS" : "ETS2",
                        game == GameType.Ats ? "American Truck Simulator" : "Euro Truck Simulator 2",
                        candidates.InstallCandidates.Select(candidate => new GameSourceWizardInstallCandidateViewModel(
                            candidate.Path,
                            candidate.IsValid,
                            candidate.Proofs)),
                        candidates.SaveRootCandidates.Select(candidate => new GameSourceWizardSaveRootCandidateViewModel(
                            candidate.Path,
                            candidate.IsValid,
                            candidate.SaveFileCount,
                            candidate.Proofs)),
                        existing));
                }

                return games;
            });

            SourceWizardGames.Clear();
            foreach (var wizardGame in wizardGames)
                SourceWizardGames.Add(wizardGame);

            CurrentWizardIndex = 0;
            CurrentWizardGame = SourceWizardGames.FirstOrDefault();
            IsSourceWizardVisible = true;
            IsEmptyStateVisible = false;
            StatusText = "Review game sources before importing saves.";
            OnPropertyChanged(nameof(SourceWizardStepText));
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to discover game sources: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void PreviousSourceWizardStep()
    {
        if (CurrentWizardIndex <= 0)
            return;

        CurrentWizardIndex--;
        CurrentWizardGame = SourceWizardGames[CurrentWizardIndex];
        OnPropertyChanged(nameof(SourceWizardStepText));
    }

    [RelayCommand]
    private void NextSourceWizardStep()
    {
        if (CurrentWizardIndex >= SourceWizardGames.Count - 1)
            return;

        CurrentWizardIndex++;
        CurrentWizardGame = SourceWizardGames[CurrentWizardIndex];
        OnPropertyChanged(nameof(SourceWizardStepText));
    }

    [RelayCommand]
    private async Task FinishSourceWizardAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            var configurations = SourceWizardGames.Select(ToConfiguration).ToList();
            var result = await Task.Run(() => gameSourceManagement.SaveValidatedAsync(
                configurations,
                CancellationToken.None));
            if (!result.Saved)
            {
                StatusText = string.Join(" ", result.Errors);
                return;
            }

            IsSourceWizardVisible = false;
            await LoadGameSourcesAsync();
            await LoadGameSavesAsync();
            UpdateNavigationState();
            StatusText = "Source setup saved. Reload saves to import enabled sources.";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to save source setup: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshDashboardCommand))]
    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        var savedTabTitle = _activeTabTitle;
        try
        {
            IsBusy = true;
            StatusText = "Loading local statistics...";
            _dashboard = await Task.Run(() => dashboardUseCases.GetDashboardAsync(_query.ToOptions(), CancellationToken.None));
            BuildExplorer(_dashboard.Companies);
            if (_selectedNode is not null)
                SelectExplorerNode(_selectedNode);
            else
                SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);
            RestoreTab(savedTabTitle);
            UpdateNavigationState();
            StatusText = $"Loaded {_dashboard.Companies.Count:N0} companies";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to load statistics: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanReloadSavesCommand))]
    private async Task ReloadAsync()
    {
        if (IsBusy)
            return;

        var savedTabTitle = _activeTabTitle;
        try
        {
            IsBusy = true;
            IsLoadProgressVisible = true;
            ResetLoadProgress();
            StatusText = "Reloading local save statistics...";
            var progress = new Progress<SaveLoadProgress>(ApplyLoadProgress);
            _dashboard = await Task.Run(() => reloadUseCase.ReloadAsync(_query.ToOptions(), CancellationToken.None, progress));
            BuildExplorer(_dashboard.Companies);
            if (_selectedNode is not null)
                SelectExplorerNode(_selectedNode);
            else
                SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);
            RestoreTab(savedTabTitle);
            UpdateNavigationState();
            StatusText = $"Reloaded {_dashboard.Companies.Count:N0} companies";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to reload statistics: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsLoadProgressVisible = false;
        }
    }

    [RelayCommand]
    private void OpenRow(GridRowViewModel? row)
    {
        if (row?.Target is not { } target)
            return;

        _explorerPresenter.ExpandExplorerToNode(target);
        SelectExplorerNode(new ExplorerNodeViewModel(
            row.Name,
            target.Kind,
            target.CompanyId,
            target.EntityId));
    }

    [RelayCommand]
    private void SelectExplorerNode(ExplorerNodeViewModel? node)
    {
        if (node is null || _dashboard is null)
            return;

        var result = _explorerPresenter.SelectNode(node, _dashboard.Companies, GameSaves, SelectedDetail);
        if (result is null)
            return;

        SelectedDetail = result.Detail;
        if (result.StatusText is not null)
            StatusText = result.StatusText;
        _selectedNode = result.SelectedNode;
    }

    private void BuildExplorer(IReadOnlyList<CompanyDto> companies)
    {
        _explorerPresenter.BuildExplorer(companies, GameSources, GameSaves);
        Explorer = _explorerPresenter.Explorer;
    }


    private async Task LoadGameSourcesAsync()
    {
        var sources = await Task.Run(() => gameSourceManagement.DiscoverAsync(CancellationToken.None));
        GameSources.Clear();
        foreach (var source in sources)
            GameSources.Add(ToViewModel(source));
        await LoadGameSavesAsync();
        UpdateNavigationState();
    }

    private async Task LoadGameSavesAsync()
    {
        var configurations = GameSources.Select(ToConfiguration).ToList();
        var saves = await Task.Run(() => gameSaveCatalog.FindSaveGamesAsync(
            configurations,
            CancellationToken.None));
        GameSaves.Clear();
        foreach (var save in saves)
            GameSaves.Add(ToViewModel(save));
        UpdateNavigationState();
    }

    private static GameSourceRowViewModel ToViewModel(GameSourceConfiguration source) =>
        new(
            source.Game.ToString(),
            source.Game == GameType.Ats ? "ATS" : "ETS2",
            source.Game == GameType.Ats ? "ats-" : "ets2-",
            source.Enabled,
            source.InstallPath,
            source.ProfilePath,
            source.SavePath,
            source.EffectiveSavePaths);

    private static GameSaveRowViewModel ToViewModel(SaveGame save) =>
        new(
            save.Game.ToString(),
            save.ProfileName,
            save.SaveName,
            save.SaveDirectory,
            save.SourceKey,
            save.SaveRootPath);

    private static GameSourceConfiguration ToConfiguration(GameSourceRowViewModel source) =>
        new(
            ParseGameType(source.GameKey),
            source.Enabled,
            string.IsNullOrWhiteSpace(source.InstallPath) ? null : source.InstallPath,
            string.IsNullOrWhiteSpace(source.ProfilePath) ? null : source.ProfilePath,
            string.IsNullOrWhiteSpace(source.SavePath) ? null : source.SavePath,
            source.SavePaths);

    private static GameSourceConfiguration ToConfiguration(GameSourceWizardGameViewModel game)
    {
        var savePaths = game.SaveRootCandidates
            .Where(candidate => candidate.IsSelected)
            .Select(candidate => candidate.Path)
            .ToList();
        return new(
            ParseGameType(game.GameKey),
            game.HasGame,
            game.InstallCandidates.FirstOrDefault(candidate => candidate.IsSelected)?.Path,
            game.DeriveProfilePath(),
            savePaths.FirstOrDefault(),
            savePaths);
    }

    private static GameType ParseGameType(string gameKey) =>
        Enum.Parse<GameType>(gameKey, ignoreCase: true);

    private void ResetLoadProgress()
    {
        SaveFileProgressValue = 0;
        SaveContentProgressValue = 0;
        SaveFileProgressText = "Preparing save reload...";
        SaveContentProgressText = "Waiting for save contents...";
    }

    private void ApplyLoadProgress(SaveLoadProgress progress)
    {
        if (IsMedallionStage(progress.Stage))
        {
            SaveFileProgressValue = Percent(progress.PhaseCompleted, progress.PhaseTotal);
            SaveContentProgressValue = 0;
            SaveFileProgressText = progress.Message;
            SaveContentProgressText = progress.PhaseTotal > 0
                ? $"Phase progress: {progress.PhaseCompleted:N0} of {progress.PhaseTotal:N0}"
                : "Preparing this phase...";
            StatusText = progress.Message;
            return;
        }

        SaveFileProgressValue = Percent(progress.CompletedFiles, progress.TotalFiles);
        SaveContentProgressValue = Percent(progress.CurrentFileCompletedUnits, progress.CurrentFileTotalUnits);
        SaveFileProgressText = progress.TotalFiles > 0
            ? $"Save files: {progress.CompletedFiles:N0} of {progress.TotalFiles:N0}"
            : progress.Message;
        SaveContentProgressText = progress.CurrentFileTotalUnits > 0
            ? $"Current save contents: {progress.CurrentFileCompletedUnits:N0} of {progress.CurrentFileTotalUnits:N0}"
            : "Waiting for save contents...";
        StatusText = progress.Message;
    }

    private static double Percent(int completed, int total) =>
        total <= 0 ? 0 : Math.Clamp((double)completed / total * 100, 0, 100);

    private static bool IsMedallionStage(SaveLoadStage stage) =>
        stage is SaveLoadStage.ReadingBronze or
            SaveLoadStage.BuildingStatistics or
            SaveLoadStage.WritingSilver or
            SaveLoadStage.WritingGold or
            SaveLoadStage.LoadingDashboard;

    private bool CanReloadSavesCommand() => CanReloadSaves;

    private bool CanRefreshDashboardCommand() => CanRefreshDashboard;

    internal void NotifyTabSelected(string? title) => _activeTabTitle = title;

    private void RestoreTab(string? tabTitle)
    {
        if (tabTitle is null || SelectedDetail is null)
            return;
        var index = SelectedDetail.Tabs.ToList().FindIndex(t => Same(t.Title, tabTitle));
        if (index > 0)
            SelectedDetail.SelectedTabIndex = index;
    }

    private void UpdateNavigationState()
    {
        IsExplorerVisible = Explorer.Roots.Count > 0 && Explorer.Roots.Any(root => root.Children.Count > 0);
        IsEmptyStateVisible = !IsSourceWizardVisible && !IsExplorerVisible;
        OnPropertyChanged(nameof(CanRefreshDashboard));
        OnPropertyChanged(nameof(CanReloadSaves));
        RefreshCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
    }

}
