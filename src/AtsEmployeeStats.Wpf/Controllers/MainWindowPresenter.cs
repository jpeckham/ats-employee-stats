using System.Collections.ObjectModel;
using System.ComponentModel;
using AtsEmployeeStats.Wpf.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AtsEmployeeStats.Wpf.Controllers;

public sealed partial class MainWindowPresenter : ObservableObject
{
    private readonly DashboardPresenter _dashboardPresenter;
    private readonly GameSourcePresenter _gameSourcePresenter;

    public MainWindowPresenter(
        DashboardPresenter dashboardPresenter,
        GameSourcePresenter gameSourcePresenter)
    {
        _dashboardPresenter = dashboardPresenter;
        _gameSourcePresenter = gameSourcePresenter;
        _dashboardPresenter.PropertyChanged += OnDashboardPresenterPropertyChanged;
        _gameSourcePresenter.PropertyChanged += OnGameSourcePresenterPropertyChanged;
        _gameSourcePresenter.GameSources.CollectionChanged += (_, _) => UpdateNavigationState();
        _gameSourcePresenter.GameSaves.CollectionChanged += (_, _) => UpdateNavigationState();
    }

    [ObservableProperty]
    private bool isEmptyStateVisible = true;

    [ObservableProperty]
    private bool isExplorerVisible;

    public CompanyExplorerViewModel Explorer => _dashboardPresenter.Explorer;

    public EntityDetailViewModel? SelectedDetail => _dashboardPresenter.SelectedDetail;

    public string StatusText => _dashboardPresenter.StatusText;

    public bool IsBusy => _dashboardPresenter.IsBusy;

    public bool IsLoadProgressVisible => _dashboardPresenter.IsLoadProgressVisible;

    public bool ExcludePlayerDriver
    {
        get => _dashboardPresenter.ExcludePlayerDriver;
        set => _dashboardPresenter.ExcludePlayerDriver = value;
    }

    public double SaveFileProgressValue => _dashboardPresenter.SaveFileProgressValue;

    public double SaveContentProgressValue => _dashboardPresenter.SaveContentProgressValue;

    public string SaveFileProgressText => _dashboardPresenter.SaveFileProgressText;

    public string SaveContentProgressText => _dashboardPresenter.SaveContentProgressText;

    public bool IsSourceWizardVisible => _gameSourcePresenter.IsSourceWizardVisible;

    public int CurrentWizardIndex => _gameSourcePresenter.CurrentWizardIndex;

    public GameSourceWizardGameViewModel? CurrentWizardGame => _gameSourcePresenter.CurrentWizardGame;

    public ObservableCollection<GameSourceRowViewModel> GameSources => _gameSourcePresenter.GameSources;

    public ObservableCollection<GameSaveRowViewModel> GameSaves => _gameSourcePresenter.GameSaves;

    public ObservableCollection<GameSourceWizardGameViewModel> SourceWizardGames => _gameSourcePresenter.SourceWizardGames;

    public bool CanReloadSaves =>
        !IsBusy && _gameSourcePresenter.CanReloadSaves;

    public bool CanRefreshDashboard =>
        _dashboardPresenter.CanRefreshDashboard;

    public string SourceWizardStepText =>
        _gameSourcePresenter.SourceWizardStepText;

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
        else if (!CanRefreshDashboard)
        {
            await _dashboardPresenter.SyncOnStartupAsync(GameSources, GameSaves);
            UpdateNavigationState();
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
            SetDashboardBusy(true);
            SetDashboardStatus("Searching for ATS and ETS2 sources...");
            var result = await _gameSourcePresenter.StartSourceWizardAsync();
            IsEmptyStateVisible = false;
            SetDashboardStatus(result.StatusText);
        }
        catch (Exception ex)
        {
            SetDashboardStatus($"Unable to discover game sources: {ex.Message}");
        }
        finally
        {
            SetDashboardBusy(false);
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
            SetDashboardBusy(true);
            var result = await _gameSourcePresenter.FinishSourceWizardAsync();
            if (!result.Succeeded)
            {
                SetDashboardStatus(result.StatusText);
                return;
            }

            UpdateNavigationState();
            SetDashboardStatus(result.StatusText);
        }
        catch (Exception ex)
        {
            SetDashboardStatus($"Unable to save source setup: {ex.Message}");
        }
        finally
        {
            SetDashboardBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshDashboardCommand))]
    private async Task RefreshAsync()
    {
        await _dashboardPresenter.RefreshAsync(GameSources, GameSaves);
        UpdateNavigationState();
    }

    [RelayCommand(CanExecute = nameof(CanReloadSavesCommand))]
    private async Task ReloadAsync()
    {
        await _dashboardPresenter.ReloadAsync(GameSources, GameSaves);
        UpdateNavigationState();
    }

    [RelayCommand]
    private void OpenRow(GridRowViewModel? row)
    {
        _dashboardPresenter.OpenRow(row);
    }

    [RelayCommand]
    private void SelectExplorerNode(ExplorerNodeViewModel? node)
    {
        _dashboardPresenter.SelectExplorerNode(node, GameSaves);
    }

    private bool CanReloadSavesCommand() => CanReloadSaves;

    private bool CanRefreshDashboardCommand() => CanRefreshDashboard;

    internal void NotifyTabSelected(string? title) => _dashboardPresenter.NotifyTabSelected(title);

    private void OnDashboardPresenterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not null)
            OnPropertyChanged(e.PropertyName);

        if (e.PropertyName is nameof(DashboardPresenter.IsBusy) or
            nameof(DashboardPresenter.CanRefreshDashboard) or
            nameof(DashboardPresenter.HasExplorerContent))
        {
            UpdateNavigationState();
        }
    }

    private void OnGameSourcePresenterPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
    }

    private void SetDashboardBusy(bool isBusy)
    {
        _dashboardPresenter.IsBusy = isBusy;
    }

    private void SetDashboardStatus(string statusText)
    {
        _dashboardPresenter.StatusText = statusText;
    }

    private void UpdateNavigationState()
    {
        IsExplorerVisible = _dashboardPresenter.HasExplorerContent;
        IsEmptyStateVisible = !IsSourceWizardVisible && !IsExplorerVisible;
        OnPropertyChanged(nameof(CanRefreshDashboard));
        OnPropertyChanged(nameof(CanReloadSaves));
        RefreshCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
    }
}
