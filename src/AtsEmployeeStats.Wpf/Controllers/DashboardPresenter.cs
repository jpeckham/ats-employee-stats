using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Wpf.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using static AtsEmployeeStats.Wpf.ViewModels.DetailHelpers;

namespace AtsEmployeeStats.Wpf.Controllers;

public sealed partial class DashboardPresenter(
    IStatisticsDashboardUseCases dashboardUseCases,
    IStatisticsReloadUseCase reloadUseCase,
    ExplorerPresenter explorerPresenter) : ObservableObject
{
    private DashboardQueryRequest _query = new();
    private DashboardStatisticsDto? _dashboard;
    private ExplorerNodeViewModel? _selectedNode;
    private string? _activeTabTitle;
    private IReadOnlyList<GameSourceRowViewModel> _gameSources = [];
    private IReadOnlyList<GameSaveRowViewModel> _gameSaves = [];

    [ObservableProperty]
    private EntityDetailViewModel? selectedDetail;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool isBusy;

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

    public CompanyExplorerViewModel Explorer => explorerPresenter.Explorer;

    public bool CanRefreshDashboard => !IsBusy && _dashboard is not null;

    public bool HasExplorerContent =>
        Explorer.Roots.Count > 0 && Explorer.Roots.Any(root => root.Children.Count > 0);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanRefreshDashboard));

    partial void OnExcludePlayerDriverChanged(bool value)
    {
        _query = _query with { ExcludePlayerDriver = value };
        if (_dashboard is not null && !IsBusy)
            _ = RefreshAsync(_gameSources, _gameSaves);
    }

    public async Task SyncOnStartupAsync(
        IEnumerable<GameSourceRowViewModel> gameSources,
        IEnumerable<GameSaveRowViewModel> gameSaves)
    {
        SetNavigationInputs(gameSources, gameSaves);
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
            StatusText = $"Loaded {_dashboard.Companies.Count:N0} companies";
            NotifyDashboardChanged();
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

    public async Task RefreshAsync(
        IEnumerable<GameSourceRowViewModel> gameSources,
        IEnumerable<GameSaveRowViewModel> gameSaves)
    {
        if (IsBusy)
            return;

        SetNavigationInputs(gameSources, gameSaves);
        var savedTabTitle = _activeTabTitle;
        try
        {
            IsBusy = true;
            StatusText = "Loading local statistics...";
            _dashboard = await Task.Run(() => dashboardUseCases.GetDashboardAsync(_query.ToOptions(), CancellationToken.None));
            BuildExplorer(_dashboard.Companies);
            RestoreSelectedDetail(savedTabTitle);
            StatusText = $"Loaded {_dashboard.Companies.Count:N0} companies";
            NotifyDashboardChanged();
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

    public async Task ReloadAsync(
        IEnumerable<GameSourceRowViewModel> gameSources,
        IEnumerable<GameSaveRowViewModel> gameSaves)
    {
        if (IsBusy)
            return;

        SetNavigationInputs(gameSources, gameSaves);
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
            RestoreSelectedDetail(savedTabTitle);
            StatusText = $"Reloaded {_dashboard.Companies.Count:N0} companies";
            NotifyDashboardChanged();
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

    public void OpenRow(GridRowViewModel? row)
    {
        if (row?.Target is not { } target)
            return;

        explorerPresenter.ExpandExplorerToNode(target);
        SelectExplorerNode(new ExplorerNodeViewModel(
            row.Name,
            target.Kind,
            target.CompanyId,
            target.EntityId),
            _gameSaves);
    }

    public void SelectExplorerNode(
        ExplorerNodeViewModel? node,
        IEnumerable<GameSaveRowViewModel> gameSaves)
    {
        if (node is null || _dashboard is null)
            return;

        _gameSaves = gameSaves.ToList();
        var result = explorerPresenter.SelectNode(node, _dashboard.Companies, _gameSaves, SelectedDetail);
        if (result is null)
            return;

        SelectedDetail = result.Detail;
        if (result.StatusText is not null)
            StatusText = result.StatusText;
        _selectedNode = result.SelectedNode;
    }

    internal void NotifyTabSelected(string? title) => _activeTabTitle = title;

    internal void ApplyLoadProgress(SaveLoadProgress progress)
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

    private void RestoreSelectedDetail(string? savedTabTitle)
    {
        if (_selectedNode is not null)
            SelectExplorerNode(_selectedNode, _gameSaves);
        else
            SelectedDetail = new CompaniesDetailViewModel(_dashboard!.Companies);
        RestoreTab(savedTabTitle);
    }

    private void BuildExplorer(IReadOnlyList<CompanyDto> companies)
    {
        explorerPresenter.BuildExplorer(companies, _gameSources, _gameSaves);
        OnPropertyChanged(nameof(Explorer));
        OnPropertyChanged(nameof(HasExplorerContent));
    }

    private void SetNavigationInputs(
        IEnumerable<GameSourceRowViewModel> gameSources,
        IEnumerable<GameSaveRowViewModel> gameSaves)
    {
        _gameSources = gameSources.ToList();
        _gameSaves = gameSaves.ToList();
    }

    private void ResetLoadProgress()
    {
        SaveFileProgressValue = 0;
        SaveContentProgressValue = 0;
        SaveFileProgressText = "Preparing save reload...";
        SaveContentProgressText = "Waiting for save contents...";
    }

    private void RestoreTab(string? tabTitle)
    {
        if (tabTitle is null || SelectedDetail is null)
            return;
        var index = SelectedDetail.Tabs.ToList().FindIndex(t => Same(t.Title, tabTitle));
        if (index > 0)
            SelectedDetail.SelectedTabIndex = index;
    }

    private void NotifyDashboardChanged()
    {
        OnPropertyChanged(nameof(CanRefreshDashboard));
        OnPropertyChanged(nameof(HasExplorerContent));
    }

    private static double Percent(int completed, int total) =>
        total <= 0 ? 0 : Math.Clamp((double)completed / total * 100, 0, 100);

    private static bool IsMedallionStage(SaveLoadStage stage) =>
        stage is SaveLoadStage.ReadingBronze or
            SaveLoadStage.BuildingStatistics or
            SaveLoadStage.WritingSilver or
            SaveLoadStage.WritingGold or
            SaveLoadStage.LoadingDashboard;
}
