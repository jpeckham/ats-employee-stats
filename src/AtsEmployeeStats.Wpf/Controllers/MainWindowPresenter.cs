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

        ExpandExplorerToNode(target);
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

        if (node.Kind == ExplorerNodeKind.Companies)
            CollapseCompanyDetailNodes();
        else
            ExpandExplorerToNode(node);

        if (node.Kind == ExplorerNodeKind.SaveLocation && !string.IsNullOrWhiteSpace(node.EntityId))
        {
            var locationCompanies = GetCompaniesForSaveLocation(node.EntityId, _dashboard.Companies);
            SelectedDetail = new CompaniesDetailViewModel(locationCompanies);
            StatusText = $"Save location selected: {node.Name}";
            return;
        }

        if (node.Kind == ExplorerNodeKind.Companies && !string.IsNullOrWhiteSpace(node.EntityId))
        {
            var locationCompanies = GetCompaniesForSaveLocation(node.EntityId, _dashboard.Companies);
            SelectedDetail = new CompaniesDetailViewModel(locationCompanies);
            StatusText = $"Companies selected: {node.Name}";
            return;
        }

        if (node.Kind == ExplorerNodeKind.Companies)
        {
            SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);
            return;
        }

        var company = _dashboard.Companies.FirstOrDefault(item => Same(item.Id, node.CompanyId));
        if (company is null)
            return;

        SelectedDetail = node.Kind switch
        {
            ExplorerNodeKind.Company => new CompanyDetailViewModel(company),
            ExplorerNodeKind.SaveLocationCompany => new CompanyDetailViewModel(company),
            ExplorerNodeKind.Garages => new CompanyDetailViewModel(company, "Garages"),
            ExplorerNodeKind.Drivers => new CompanyDetailViewModel(company, "Drivers"),
            ExplorerNodeKind.Trucks => new CompanyDetailViewModel(company, "Trucks"),
            ExplorerNodeKind.Trailers => new CompanyDetailViewModel(company, "Trailers"),
            ExplorerNodeKind.Jobs => new CompanyDetailViewModel(company, "Jobs"),
            ExplorerNodeKind.Cities => new CompanyDetailViewModel(company, "Cities"),
            ExplorerNodeKind.Garage => company.Garages.FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } garage ? new GarageDetailViewModel(company, garage) : SelectedDetail,
            ExplorerNodeKind.Driver => company.Drivers.FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } driver ? new DriverDetailViewModel(company, driver) : SelectedDetail,
            ExplorerNodeKind.Truck => company.Trucks.FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } truck ? new TruckDetailViewModel(company, truck) : SelectedDetail,
            ExplorerNodeKind.Trailer => (company.Trailers ?? []).FirstOrDefault(item => Same(item.LicensePlate, node.EntityId) || Same(item.Id, node.EntityId)) is { } trailer ? new TrailerDetailViewModel(company, trailer) : SelectedDetail,
            ExplorerNodeKind.Job => company.Missions.FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } job ? new JobDetailViewModel(company, job) : SelectedDetail,
            ExplorerNodeKind.City => (company.Cities ?? []).FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } city ? new CityDetailViewModel(company, city) : SelectedDetail,
            _ => SelectedDetail
        };

        if (node.Kind == ExplorerNodeKind.SaveLocationCompany)
            StatusText = $"Company selected: {company.DisplayName}";

        _selectedNode = node;
    }

    private void BuildExplorer(IReadOnlyList<CompanyDto> companies)
    {
        var root = new ExplorerNodeViewModel("Games", ExplorerNodeKind.Games);
        root.IsExpanded = true;
        var unpartitionedCompanies = companies
            .Where(company => !GameSources.Any(gameSource => company.Id.StartsWith(gameSource.SourcePrefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        foreach (var gameSource in GameSources.OrderBy(source => source.GameName, StringComparer.CurrentCultureIgnoreCase))
        {
            var gameNode = new ExplorerNodeViewModel(gameSource.GameName, ExplorerNodeKind.GameSource);
            gameNode.IsExpanded = true;
            var savesNode = new ExplorerNodeViewModel("Save Locations", ExplorerNodeKind.GameSaves);
            savesNode.IsExpanded = true;
            foreach (var saveLocation in GameSaves
                .Where(save => save.GameKey == gameSource.GameKey)
                .Where(save => !string.IsNullOrWhiteSpace(save.SaveRootPath))
                .GroupBy(save => save.SaveRootPath, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                var locationNode = new ExplorerNodeViewModel(
                    saveLocation.Key ?? "Unknown save location",
                    ExplorerNodeKind.SaveLocation,
                    entityId: saveLocation.Key);
                locationNode.IsExpanded = true;
                var companiesNode = new ExplorerNodeViewModel("Companies", ExplorerNodeKind.Companies, entityId: saveLocation.Key);
                companiesNode.IsExpanded = true;
                var locationCompanies = GetCompaniesForSaveLocation(saveLocation.Key ?? string.Empty, companies)
                    .GroupBy(company => company.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase);
                foreach (var company in locationCompanies)
                {
                    companiesNode.Children.Add(BuildSaveLocationCompanyNode(
                        company.Key,
                        company,
                        saveLocation.Key ?? string.Empty));
                }

                locationNode.Children.Add(companiesNode);
                savesNode.Children.Add(locationNode);
            }
            gameNode.Children.Add(savesNode);
            root.Children.Add(gameNode);
        }

        if (unpartitionedCompanies.Count > 0)
        {
            var companiesNode = new ExplorerNodeViewModel("Companies", ExplorerNodeKind.Companies);
            companiesNode.IsExpanded = true;
            foreach (var company in unpartitionedCompanies.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
                companiesNode.Children.Add(BuildCompanyNode(company));
            root.Children.Add(companiesNode);
        }

        Explorer.Roots.Clear();
        Explorer.Roots.Add(root);
    }

    private static ExplorerNodeViewModel BuildCompanyNode(CompanyDto company)
    {
        var companyNode = new ExplorerNodeViewModel(company.DisplayName, ExplorerNodeKind.Company, company.Id);
        AddCompanyCollections(companyNode, company);
        return companyNode;
    }

    private static ExplorerNodeViewModel BuildSaveLocationCompanyNode(
        string displayName,
        IEnumerable<CompanyDto> companies,
        string saveRootPath)
    {
        var company = companies.First();
        var companyNode = new ExplorerNodeViewModel(
            displayName,
            ExplorerNodeKind.SaveLocationCompany,
            company.Id,
            saveRootPath);
        AddCompanyCollections(companyNode, company);
        return companyNode;
    }

    private void ExpandExplorerToNode(RowNavigationTargetViewModel target) =>
        ExpandExplorerToNode(new ExplorerNodeViewModel(
            string.Empty,
            target.Kind,
            target.CompanyId,
            target.EntityId));

    private void ExpandExplorerToNode(ExplorerNodeViewModel target)
    {
        var matching = Explorer.Roots
            .SelectMany(root => FindExplorerMatches(root, target, []))
            .FirstOrDefault();
        if (matching is null)
            return;

        ExpandAncestorPath(matching.Ancestors);
        if (ShouldExpandMatchedNode(target.Kind))
            matching.Node.IsExpanded = true;
    }

    private static void ExpandAncestorPath(IEnumerable<ExplorerNodeViewModel> ancestors)
    {
        foreach (var ancestor in ancestors)
            ancestor.IsExpanded = true;
    }

    private void CollapseCompanyDetailNodes()
    {
        foreach (var root in Explorer.Roots)
            CollapseCompanyDetailNodes(root);
    }

    private static void CollapseCompanyDetailNodes(ExplorerNodeViewModel node)
    {
        if (IsCompanyDetailNode(node.Kind))
            node.IsExpanded = false;

        foreach (var child in node.Children)
            CollapseCompanyDetailNodes(child);
    }

    private static IEnumerable<ExplorerMatch> FindExplorerMatches(
        ExplorerNodeViewModel node,
        ExplorerNodeViewModel target,
        IReadOnlyList<ExplorerNodeViewModel> ancestors)
    {
        if (MatchesExplorerNode(node, target))
            yield return new ExplorerMatch(node, ancestors);

        var nextAncestors = ancestors.Append(node).ToArray();
        foreach (var child in node.Children)
        {
            foreach (var match in FindExplorerMatches(child, target, nextAncestors))
                yield return match;
        }
    }

    private static bool MatchesExplorerNode(ExplorerNodeViewModel node, ExplorerNodeViewModel target) =>
        target.Kind switch
        {
            ExplorerNodeKind.Companies =>
                node.Kind == ExplorerNodeKind.Companies &&
                (string.IsNullOrWhiteSpace(target.EntityId) || Same(node.EntityId, target.EntityId)),
            ExplorerNodeKind.Company =>
                (node.Kind == ExplorerNodeKind.Company || node.Kind == ExplorerNodeKind.SaveLocationCompany) &&
                Same(node.CompanyId, target.CompanyId),
            ExplorerNodeKind.SaveLocationCompany =>
                node.Kind == ExplorerNodeKind.SaveLocationCompany &&
                Same(node.CompanyId, target.CompanyId) &&
                (string.IsNullOrWhiteSpace(target.EntityId) || Same(node.EntityId, target.EntityId)),
            ExplorerNodeKind.Garages or ExplorerNodeKind.Drivers or ExplorerNodeKind.Trucks or
                ExplorerNodeKind.Trailers or ExplorerNodeKind.Jobs or ExplorerNodeKind.Cities =>
                node.Kind == target.Kind && Same(node.CompanyId, target.CompanyId),
            ExplorerNodeKind.Garage or ExplorerNodeKind.Driver or ExplorerNodeKind.Truck or
                ExplorerNodeKind.Trailer or ExplorerNodeKind.Job or ExplorerNodeKind.City =>
                node.Kind == target.Kind && Same(node.CompanyId, target.CompanyId) && Same(node.EntityId, target.EntityId),
            _ => false
        };

    private static bool ShouldExpandMatchedNode(ExplorerNodeKind kind) =>
        kind is ExplorerNodeKind.Company or ExplorerNodeKind.SaveLocationCompany or
            ExplorerNodeKind.Garages or ExplorerNodeKind.Drivers or ExplorerNodeKind.Trucks or
            ExplorerNodeKind.Trailers or ExplorerNodeKind.Jobs or ExplorerNodeKind.Cities;

    private static bool IsCompanyDetailNode(ExplorerNodeKind kind) =>
        kind is ExplorerNodeKind.Company or ExplorerNodeKind.SaveLocationCompany or
            ExplorerNodeKind.Garages or ExplorerNodeKind.Drivers or ExplorerNodeKind.Trucks or
            ExplorerNodeKind.Trailers or ExplorerNodeKind.Jobs or ExplorerNodeKind.Cities;

    private static void AddCompanyCollections(ExplorerNodeViewModel companyNode, CompanyDto company)
    {
        AddCollection(companyNode, "Garages", ExplorerNodeKind.Garages, company.Id, company.Garages.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Garage, company.Id, item.Id)));
        AddCollection(companyNode, "Drivers", ExplorerNodeKind.Drivers, company.Id, company.Drivers.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Driver, company.Id, item.Id)));
        AddCollection(companyNode, "Trucks", ExplorerNodeKind.Trucks, company.Id, company.Trucks.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Truck, company.Id, item.Id)));
        AddCollection(companyNode, "Trailers", ExplorerNodeKind.Trailers, company.Id, (company.Trailers ?? []).Select(item => new ExplorerNodeViewModel(item.LicensePlate ?? item.Id, ExplorerNodeKind.Trailer, company.Id, item.LicensePlate ?? item.Id)));
        AddCollection(companyNode, "Jobs", ExplorerNodeKind.Jobs, company.Id, company.Missions.Take(250).Select(item => new ExplorerNodeViewModel(string.IsNullOrWhiteSpace(item.Cargo) ? item.Id : item.Cargo!, ExplorerNodeKind.Job, company.Id, item.Id)));
        AddCollection(companyNode, "Cities", ExplorerNodeKind.Cities, company.Id, (company.Cities ?? []).Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.City, company.Id, item.Id)));
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

    private static void AddCollection(
        ExplorerNodeViewModel companyNode,
        string title,
        ExplorerNodeKind kind,
        string companyId,
        IEnumerable<ExplorerNodeViewModel> children)
    {
        var collection = new ExplorerNodeViewModel(title, kind, companyId);
        foreach (var child in children.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            collection.Children.Add(child);
        companyNode.Children.Add(collection);
    }

    private static string NormalizeSourceKey(string sourceKey)
    {
        var normalized = new string(sourceKey
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? "default" : normalized;
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

    private IReadOnlyList<CompanyDto> GetCompaniesForSaveLocation(string saveRootPath, IReadOnlyList<CompanyDto> companies)
    {
        var sourcePrefixes = GameSaves
            .Where(save => string.Equals(save.SaveRootPath, saveRootPath, StringComparison.OrdinalIgnoreCase))
            .Select(save => $"{NormalizeSourceKey(save.SourceKey)}:")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return companies
            .Where(company => sourcePrefixes.Any(prefix => company.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(company => company.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
