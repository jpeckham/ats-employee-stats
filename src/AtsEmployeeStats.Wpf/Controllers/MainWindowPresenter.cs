using System.Collections.ObjectModel;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Wpf.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static AtsEmployeeStats.Wpf.ViewModels.DetailHelpers;

namespace AtsEmployeeStats.Wpf.Controllers;

public sealed partial class MainWindowPresenter : ObservableObject
{
    private readonly ExplorerPresenter _explorerPresenter = new();
    private readonly IStatisticsDashboardUseCases dashboardUseCases;
    private readonly IStatisticsReloadUseCase reloadUseCase;
    private readonly GameSourcePresenter _gameSourcePresenter;
    private DashboardQueryRequest _query = new();
    private DashboardStatisticsDto? _dashboard;
    private ExplorerNodeViewModel? _selectedNode;
    private string? _activeTabTitle;

    public MainWindowPresenter(
        IStatisticsDashboardUseCases dashboardUseCases,
        IStatisticsReloadUseCase reloadUseCase,
        GameSourcePresenter gameSourcePresenter)
    {
        this.dashboardUseCases = dashboardUseCases;
        this.reloadUseCase = reloadUseCase;
        _gameSourcePresenter = gameSourcePresenter;
        _gameSourcePresenter.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsSourceWizardVisible) or
                nameof(CurrentWizardIndex) or
                nameof(CurrentWizardGame) or
                nameof(SourceWizardStepText))
            {
                OnPropertyChanged(e.PropertyName);
            }

            if (e.PropertyName is nameof(IsSourceWizardVisible))
                UpdateNavigationState();
        };
        _gameSourcePresenter.GameSources.CollectionChanged += (_, _) => UpdateNavigationState();
        _gameSourcePresenter.GameSaves.CollectionChanged += (_, _) => UpdateNavigationState();
    }

    [ObservableProperty]
    private CompanyExplorerViewModel explorer = new();

    [ObservableProperty]
    private EntityDetailViewModel? selectedDetail;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool isBusy;

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

    public bool IsSourceWizardVisible => _gameSourcePresenter.IsSourceWizardVisible;

    public int CurrentWizardIndex => _gameSourcePresenter.CurrentWizardIndex;

    public GameSourceWizardGameViewModel? CurrentWizardGame => _gameSourcePresenter.CurrentWizardGame;

    public ObservableCollection<GameSourceRowViewModel> GameSources => _gameSourcePresenter.GameSources;

    public ObservableCollection<GameSaveRowViewModel> GameSaves => _gameSourcePresenter.GameSaves;

    public ObservableCollection<GameSourceWizardGameViewModel> SourceWizardGames => _gameSourcePresenter.SourceWizardGames;

    public bool CanReloadSaves =>
        !IsBusy && _gameSourcePresenter.CanReloadSaves;

    public bool CanRefreshDashboard =>
        !IsBusy && _dashboard is not null;

    public string SourceWizardStepText =>
        _gameSourcePresenter.SourceWizardStepText;

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
        await _gameSourcePresenter.LoadGameSourcesAsync();
        if (await _gameSourcePresenter.RequiresWizardAsync())
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
            var result = await _gameSourcePresenter.StartSourceWizardAsync();
            IsEmptyStateVisible = false;
            StatusText = result.StatusText;
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
        _gameSourcePresenter.PreviousSourceWizardStep();
    }

    [RelayCommand]
    private void NextSourceWizardStep()
    {
        _gameSourcePresenter.NextSourceWizardStep();
    }

    [RelayCommand]
    private async Task FinishSourceWizardAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            var result = await _gameSourcePresenter.FinishSourceWizardAsync();
            if (!result.Succeeded)
            {
                StatusText = result.StatusText;
                return;
            }

            UpdateNavigationState();
            StatusText = result.StatusText;
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
