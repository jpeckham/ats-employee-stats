using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Maui.Controllers;
using AtsEmployeeStats.Maui.Presentation;

namespace AtsEmployeeStats.Maui;

public partial class MainPage : ContentPage
{
    private bool _loaded;

    public MainPage(
        IRecommendNextGarageCityUseCase recommendNextGarageCityUseCase,
        IRecommendTrailersForGarageUseCase recommendTrailersForGarageUseCase,
        IRecommendDriverSkillsUseCase recommendDriverSkillsUseCase,
        IDiagnoseUnderperformersUseCase diagnoseUnderperformersUseCase,
        DashboardController dashboardController,
        DetailNavigationController navigationController)
    {
        InitializeComponent();
        BindingContext = new DashboardPageModel(
            recommendNextGarageCityUseCase,
            recommendTrailersForGarageUseCase,
            recommendDriverSkillsUseCase,
            diagnoseUnderperformersUseCase,
            dashboardController,
            navigationController);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded || BindingContext is not DashboardPageModel model)
            return;

        _loaded = true;
        model.RefreshCommand.Execute(null);
    }
}

public sealed record CompanySummaryItem(
    string Id,
    string DisplayName,
    string ProfitText,
    string DriverCountText,
    string AssetsText);

public sealed record DetailMetricItem(string Label, string Value);

public sealed record DetailRowItem(
    string Name,
    string PrimaryText,
    string SecondaryText,
    string MetaText);

public sealed record ExplorerNavigationItem(
    string Id,
    string DisplayName,
    string Kind,
    int Depth,
    ICommand SelectCommand);

public sealed record DetailTabItem(
    string Id,
    string Title,
    bool IsSelected,
    ICommand SelectCommand);

public sealed record SortableExplorerRowItem(
    string Name,
    string PrimaryText,
    string SecondaryText,
    string MetaText,
    string SparklineText,
    string? ActionRoute,
    ICommand OpenCommand);

internal sealed class DashboardPageModel : INotifyPropertyChanged, IDashboardPresentationTarget
{
    private readonly IRecommendNextGarageCityUseCase _recommendNextGarageCityUseCase;
    private readonly IRecommendTrailersForGarageUseCase _recommendTrailersForGarageUseCase;
    private readonly IRecommendDriverSkillsUseCase _recommendDriverSkillsUseCase;
    private readonly IDiagnoseUnderperformersUseCase _diagnoseUnderperformersUseCase;
    private readonly DashboardController _dashboardController;
    private readonly DetailNavigationController _navigationController;
    private readonly Dictionary<string, CompanyDto> _companyDetails = new(StringComparer.OrdinalIgnoreCase);
    private bool _isBusy;
    private string _statusText = "Ready";
    private string _companyCountText = "0 companies";
    private string _totalProfitText = "$0";
    private string _driverCountText = "0 drivers";
    private string _recommendationText = "No expansion recommendation loaded.";
    private string _trailerRecommendationText = "No trailer recommendation loaded.";
    private string _diagnosisText = "No underperformer diagnosis loaded.";
    private string _driverSkillRecommendationText = "No driver skill recommendation loaded.";
    private bool _isProgressVisible;
    private double _overallProgress;
    private double _currentFileProgress;
    private string _overallProgressText = "Save files: 0 of 0";
    private string _currentFileProgressText = "Current save: 0 of 0 units";
    private string? _selectedCompanyId;
    private string _selectedExplorerId = string.Empty;
    private string _activeDetailTab = "overview";
    private string _activeRowsTitle = "Overview";
    private string _sortColumn = "profit";
    private bool _sortDescending = true;
    private bool _hasSelectedCompany;
    private string _selectedCompanyTitle = "No company selected";
    private string _selectedCompanySubtitle = string.Empty;
    private string _selectedCompanyProfitText = "$0";
    private string _lastDashboardStatusText = "Refreshed statistics";

    public DashboardPageModel(
        IRecommendNextGarageCityUseCase recommendNextGarageCityUseCase,
        IRecommendTrailersForGarageUseCase recommendTrailersForGarageUseCase,
        IRecommendDriverSkillsUseCase recommendDriverSkillsUseCase,
        IDiagnoseUnderperformersUseCase diagnoseUnderperformersUseCase,
        DashboardController dashboardController,
        DetailNavigationController navigationController)
    {
        _recommendNextGarageCityUseCase = recommendNextGarageCityUseCase;
        _recommendTrailersForGarageUseCase = recommendTrailersForGarageUseCase;
        _recommendDriverSkillsUseCase = recommendDriverSkillsUseCase;
        _diagnoseUnderperformersUseCase = diagnoseUnderperformersUseCase;
        _dashboardController = dashboardController;
        _navigationController = navigationController;
        RefreshCommand = new Command(
            execute: async () => await RefreshAsync(),
            canExecute: () => !IsBusy);
        OpenCompanyCommand = new Command<string?>(OpenCompany);
        OpenCompanyDetailCommand = new Command<string?>(async companyId => await OpenCompanyDetailAsync(companyId));
        SelectExplorerItemCommand = new Command<string?>(SelectExplorerItem);
        SelectDetailTabCommand = new Command<string?>(SelectDetailTab);
        SortActiveRowsCommand = new Command<string?>(SortActiveRows);
        OpenRowCommand = new Command<string?>(async route => await OpenRowAsync(route));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CompanySummaryItem> Companies { get; } = [];

    public ObservableCollection<ExplorerNavigationItem> ExplorerNavigationItems { get; } = [];

    public ObservableCollection<DetailTabItem> DetailTabs { get; } = [];

    public ObservableCollection<SortableExplorerRowItem> ActiveDetailRows { get; } = [];

    public ObservableCollection<DetailMetricItem> SelectedCompanyMetrics { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedGarages { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedDrivers { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedTrucks { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedTrailers { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedRecentJobs { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand OpenCompanyCommand { get; }

    public ICommand OpenCompanyDetailCommand { get; }

    public ICommand SelectExplorerItemCommand { get; }

    public ICommand SelectDetailTabCommand { get; }

    public ICommand SortActiveRowsCommand { get; }

    public ICommand OpenRowCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                ((Command)RefreshCommand).ChangeCanExecute();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string CompanyCountText
    {
        get => _companyCountText;
        private set => SetField(ref _companyCountText, value);
    }

    public string TotalProfitText
    {
        get => _totalProfitText;
        private set => SetField(ref _totalProfitText, value);
    }

    public string DriverCountText
    {
        get => _driverCountText;
        private set => SetField(ref _driverCountText, value);
    }

    public string RecommendationText
    {
        get => _recommendationText;
        private set => SetField(ref _recommendationText, value);
    }

    public string TrailerRecommendationText
    {
        get => _trailerRecommendationText;
        private set => SetField(ref _trailerRecommendationText, value);
    }

    public string DiagnosisText
    {
        get => _diagnosisText;
        private set => SetField(ref _diagnosisText, value);
    }

    public string DriverSkillRecommendationText
    {
        get => _driverSkillRecommendationText;
        private set => SetField(ref _driverSkillRecommendationText, value);
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        private set => SetField(ref _isProgressVisible, value);
    }

    public double OverallProgress
    {
        get => _overallProgress;
        private set => SetField(ref _overallProgress, value);
    }

    public double CurrentFileProgress
    {
        get => _currentFileProgress;
        private set => SetField(ref _currentFileProgress, value);
    }

    public string OverallProgressText
    {
        get => _overallProgressText;
        private set => SetField(ref _overallProgressText, value);
    }

    public string CurrentFileProgressText
    {
        get => _currentFileProgressText;
        private set => SetField(ref _currentFileProgressText, value);
    }

    public bool HasSelectedCompany
    {
        get => _hasSelectedCompany;
        private set
        {
            if (SetField(ref _hasSelectedCompany, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoSelectedCompany)));
        }
    }

    public bool HasNoSelectedCompany => !HasSelectedCompany;

    public string SelectedCompanyTitle
    {
        get => _selectedCompanyTitle;
        private set => SetField(ref _selectedCompanyTitle, value);
    }

    public string SelectedCompanySubtitle
    {
        get => _selectedCompanySubtitle;
        private set => SetField(ref _selectedCompanySubtitle, value);
    }

    public string SelectedCompanyProfitText
    {
        get => _selectedCompanyProfitText;
        private set => SetField(ref _selectedCompanyProfitText, value);
    }

    public string ActiveRowsTitle
    {
        get => _activeRowsTitle;
        private set => SetField(ref _activeRowsTitle, value);
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        IsProgressVisible = true;
        OverallProgress = 0;
        CurrentFileProgress = 0;
        OverallProgressText = "Save files: 0 of 0";
        CurrentFileProgressText = "Current save: 0 of 0 units";
        StatusText = "Loading local save statistics...";

        try
        {
            var request = new DashboardQueryRequest();
            var options = request.ToOptions();
            await _dashboardController.RefreshAsync(this, request, CancellationToken.None);
            await ApplyRecommendationsAsync(_companyDetails.Values.ToList(), options);
            StatusText = _lastDashboardStatusText;
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to load statistics: {ex.Message}";
        }
        finally
        {
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    public void ShowProgress(DashboardProgressPresentation presentation)
    {
        StatusText = presentation.StatusText;
        OverallProgress = presentation.OverallProgress;
        CurrentFileProgress = presentation.CurrentFileProgress;
        OverallProgressText = presentation.OverallProgressText;
        CurrentFileProgressText = presentation.CurrentFileProgressText;
    }

    public void ShowDashboard(DashboardPresentation presentation)
    {
        Companies.Clear();
        _companyDetails.Clear();
        foreach (var company in presentation.CompanyDetails)
            _companyDetails[company.Key] = company.Value;

        foreach (var company in presentation.Companies)
        {
            Companies.Add(new CompanySummaryItem(
                company.Id,
                company.DisplayName,
                company.ProfitText,
                company.DriverCountText,
                company.AssetsText));
        }

        CompanyCountText = presentation.CompanyCountText;
        TotalProfitText = presentation.TotalProfitText;
        DriverCountText = presentation.DriverCountText;
        _lastDashboardStatusText = presentation.RefreshedStatusText;
        BuildExplorerNavigation();

        if (_selectedCompanyId is not null && _companyDetails.ContainsKey(_selectedCompanyId))
        {
            OpenCompany(_selectedCompanyId);
        }
        else
        {
            ClearSelectedCompany();
        }
    }

    private void OpenCompany(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId) ||
            !_companyDetails.TryGetValue(companyId, out var company))
        {
            return;
        }

        _selectedCompanyId = company.Id;
        _selectedExplorerId = $"company:{company.Id}";
        _activeDetailTab = "overview";
        HasSelectedCompany = true;
        SelectedCompanyTitle = company.DisplayName;
        SelectedCompanySubtitle = string.IsNullOrWhiteSpace(company.OwnerName)
            ? company.Id
            : $"{company.OwnerName} - {company.Id}";
        SelectedCompanyProfitText = FormatMoney(company.Profit);

        ReplaceItems(SelectedCompanyMetrics,
        [
            new DetailMetricItem("Garages", company.Garages.Count.ToString("N0", CultureInfo.CurrentCulture)),
            new DetailMetricItem("Drivers", company.Drivers.Count.ToString("N0", CultureInfo.CurrentCulture)),
            new DetailMetricItem("Trucks", company.Trucks.Count.ToString("N0", CultureInfo.CurrentCulture)),
            new DetailMetricItem("Trailers", (company.Trailers?.Count ?? 0).ToString("N0", CultureInfo.CurrentCulture)),
            new DetailMetricItem("Jobs", company.Missions.Count.ToString("N0", CultureInfo.CurrentCulture))
        ]);

        ReplaceItems(
            SelectedGarages,
            company.Garages
                .OrderByDescending(garage => garage.Profit)
                .ThenBy(garage => garage.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .Select(garage => new DetailRowItem(
                    garage.DisplayName,
                    FormatMoney(garage.Profit),
                    $"{garage.EmployeeCount:N0} employees / {garage.TruckCount:N0} trucks / {garage.TrailerCount:N0} trailers",
                    $"{FormatMoney(garage.ProfitPerDay)}/day")));

        ReplaceItems(
            SelectedDrivers,
            company.Drivers
                .OrderByDescending(driver => driver.Profit)
                .ThenBy(driver => driver.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .Select(driver => new DetailRowItem(
                    driver.DisplayName,
                    FormatMoney(driver.Profit),
                    $"{GetGarageDisplayName(company, driver.GarageId)} / {GetTruckDisplayName(company, driver.TruckId)}",
                    $"{driver.JobCount:N0} jobs")));

        ReplaceItems(
            SelectedTrucks,
            company.Trucks
                .OrderByDescending(truck => truck.Profit)
                .ThenBy(truck => truck.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .Select(truck => new DetailRowItem(
                    truck.DisplayName,
                    FormatMoney(truck.Profit),
                    $"{GetGarageDisplayName(company, truck.GarageId)} / {GetDriverDisplayName(company, truck.DriverId)}",
                    string.IsNullOrWhiteSpace(truck.LicensePlate) ? truck.Id : truck.LicensePlate!)));

        ReplaceItems(
            SelectedTrailers,
            (company.Trailers ?? [])
                .OrderByDescending(trailer => trailer.Profit)
                .ThenBy(trailer => trailer.TrailerType, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .Select(trailer => new DetailRowItem(
                    string.IsNullOrWhiteSpace(trailer.LicensePlate) ? trailer.Id : trailer.LicensePlate!,
                    FormatMoney(trailer.Profit),
                    $"{trailer.TrailerType} / {GetGarageDisplayName(company, trailer.GarageId)}",
                    $"{trailer.JobCount:N0} jobs")));

        ReplaceItems(
            SelectedRecentJobs,
            company.Missions
                .OrderByDescending(job => job.TimestampDay ?? int.MinValue)
                .ThenByDescending(job => job.Profit)
                .Take(10)
                .Select(job => new DetailRowItem(
                    string.IsNullOrWhiteSpace(job.Cargo) ? job.Id : job.Cargo!,
                    FormatMoney(job.Profit),
                    $"{FormatValue(job.SourceCity)} to {FormatValue(job.TargetCity)}",
                    FormatValue(job.TrailerType))));
        BuildDetailTabs();
        RefreshActiveRows();
    }

    private void SelectExplorerItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        var parts = itemId.Split(':', 3);
        if (parts.Length < 2)
            return;

        var companyId = parts[1];
        if (!_companyDetails.TryGetValue(companyId, out var company))
            return;

        _selectedCompanyId = company.Id;
        _selectedExplorerId = itemId;
        HasSelectedCompany = true;
        SelectedCompanyTitle = company.DisplayName;
        SelectedCompanySubtitle = string.IsNullOrWhiteSpace(company.OwnerName)
            ? company.Id
            : $"{company.OwnerName} - {company.Id}";
        SelectedCompanyProfitText = FormatMoney(company.Profit);

        _activeDetailTab = parts[0] switch
        {
            "garages" => "garages",
            "drivers" => "drivers",
            "trucks" => "trucks",
            "trailers" => "trailers",
            "jobs" => "jobs",
            "cities" => "cities",
            _ => "overview"
        };

        BuildDetailTabs();
        RefreshActiveRows();
    }

    private void SelectDetailTab(string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId) || _selectedCompanyId is null)
            return;

        _activeDetailTab = tabId;
        _selectedExplorerId = $"{tabId}:{_selectedCompanyId}";
        BuildDetailTabs();
        RefreshActiveRows();
    }

    private void SortActiveRows(string? column)
    {
        if (string.IsNullOrWhiteSpace(column))
            return;

        if (StringComparer.OrdinalIgnoreCase.Equals(_sortColumn, column))
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortColumn = column;
            _sortDescending = column is not "name";
        }

        RefreshActiveRows();
    }

    private async Task OpenRowAsync(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return;

        await _navigationController.GoToRouteAsync(route);
    }

    private void BuildExplorerNavigation()
    {
        ExplorerNavigationItems.Clear();
        foreach (var company in _companyDetails.Values.OrderBy(company => company.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            AddExplorerItem($"company:{company.Id}", company.DisplayName, "Company", 0);
            AddExplorerItem($"overview:{company.Id}", "Overview", "View", 1);
            AddExplorerItem($"garages:{company.Id}", $"Garages ({company.Garages.Count:N0})", "List", 1);
            AddExplorerItem($"drivers:{company.Id}", $"Drivers ({company.Drivers.Count:N0})", "List", 1);
            AddExplorerItem($"trucks:{company.Id}", $"Trucks ({company.Trucks.Count:N0})", "List", 1);
            AddExplorerItem($"trailers:{company.Id}", $"Trailers ({company.Trailers?.Count ?? 0:N0})", "List", 1);
            AddExplorerItem($"jobs:{company.Id}", $"Jobs ({company.Missions.Count:N0})", "List", 1);
            AddExplorerItem($"cities:{company.Id}", $"Cities ({company.Cities?.Count ?? 0:N0})", "List", 1);
        }
    }

    private void AddExplorerItem(string id, string displayName, string kind, int depth) =>
        ExplorerNavigationItems.Add(new ExplorerNavigationItem(id, displayName, kind, depth, SelectExplorerItemCommand));

    private void BuildDetailTabs()
    {
        DetailTabs.Clear();
        foreach (var tab in new[]
                 {
                     ("overview", "Overview"),
                     ("garages", "Garages"),
                     ("drivers", "Drivers"),
                     ("trucks", "Trucks"),
                     ("trailers", "Trailers"),
                     ("jobs", "Jobs"),
                     ("cities", "Cities")
                 })
        {
            DetailTabs.Add(new DetailTabItem(tab.Item1, tab.Item2, StringComparer.OrdinalIgnoreCase.Equals(tab.Item1, _activeDetailTab), SelectDetailTabCommand));
        }
    }

    private void RefreshActiveRows()
    {
        ActiveDetailRows.Clear();
        if (_selectedCompanyId is null || !_companyDetails.TryGetValue(_selectedCompanyId, out var company))
            return;

        var rows = _activeDetailTab switch
        {
            "garages" => company.Garages.Select(garage => Row(
                garage.DisplayName,
                FormatMoney(garage.Profit),
                $"{garage.EmployeeCount:N0} drivers / {garage.TruckCount:N0} trucks / {garage.TrailerCount:N0} trailers",
                $"{FormatMoney(garage.ProfitPerDay)}/day",
                SparklineText(garage.Trend),
                RouteToGarage(company.Id, garage.Id),
                garage.Profit,
                garage.ProfitPerDay,
                garage.EmployeeCount)),
            "drivers" => company.Drivers.Select(driver => Row(
                driver.DisplayName,
                FormatMoney(driver.Profit),
                $"{GetGarageDisplayName(company, driver.GarageId)} / {GetTruckDisplayName(company, driver.TruckId)}",
                $"{driver.JobCount:N0} jobs",
                SparklineText(driver.Trend),
                RouteToDriver(company.Id, driver.Id),
                driver.Profit,
                driver.ProfitPerDay,
                driver.JobCount)),
            "trucks" => company.Trucks.Select(truck => Row(
                truck.DisplayName,
                FormatMoney(truck.Profit),
                $"{GetGarageDisplayName(company, truck.GarageId)} / {GetDriverDisplayName(company, truck.DriverId)}",
                truck.LicensePlate ?? truck.Id,
                SparklineText(truck.Trend),
                RouteToTruck(company.Id, truck.Id),
                truck.Profit,
                truck.ProfitPerDay,
                0)),
            "trailers" => (company.Trailers ?? []).Select(trailer => Row(
                string.IsNullOrWhiteSpace(trailer.LicensePlate) ? trailer.Id : trailer.LicensePlate!,
                FormatMoney(trailer.Profit),
                $"{trailer.TrailerType} / {GetGarageDisplayName(company, trailer.GarageId)}",
                $"{trailer.JobCount:N0} jobs",
                SparklineText(trailer.Trend),
                RouteToTrailer(company.Id, trailer.LicensePlate ?? trailer.Id),
                trailer.Profit,
                trailer.ProfitPerDay,
                trailer.JobCount)),
            "jobs" => company.Missions.Select(job => Row(
                string.IsNullOrWhiteSpace(job.Cargo) ? job.Id : job.Cargo!,
                FormatMoney(job.Profit),
                $"{FormatValue(job.SourceCity)} to {FormatValue(job.TargetCity)}",
                job.TimestampDay?.ToString(CultureInfo.CurrentCulture) ?? "-",
                SparklineText(null),
                RouteToJob(company.Id, job.Id),
                job.Profit,
                job.TimestampDay ?? 0,
                0)),
            "cities" => (company.Cities ?? []).Select(city => Row(
                city.DisplayName,
                FormatMoney(city.BidirectionalProfit),
                city.HasOwnedGarage ? "Owned garage" : "No owned garage",
                $"Expansion {city.ExpansionScore:0.##}",
                $"Visits {city.VisitCount:N0}",
                RouteToCity(company.Id, city.Id),
                city.BidirectionalProfit,
                (long)city.ExpansionScore,
                city.VisitCount)),
            _ => OverviewRows(company)
        };

        ActiveRowsTitle = _activeDetailTab switch
        {
            "garages" => "Garages",
            "drivers" => "Drivers",
            "trucks" => "Trucks",
            "trailers" => "Trailers",
            "jobs" => "Jobs",
            "cities" => "Cities",
            _ => "Overview"
        };

        foreach (var row in SortRows(rows))
            ActiveDetailRows.Add(row.Item);
    }

    private IEnumerable<ExplorerRowSortEnvelope> OverviewRows(CompanyDto company) =>
    [
        Row("Profit trend", SelectedCompanyProfitText, "Company-wide profit movement", "Trend", SparklineText(company.ProfitTrend), RouteToCompany(company.Id), company.Profit, company.Profit, company.Missions.Count),
        Row("Garages", $"{company.Garages.Count:N0}", "Owned garages and their associated fleets", "Open list", string.Empty, null, company.Garages.Sum(garage => garage.Profit), 0, company.Garages.Count),
        Row("Drivers", $"{company.Drivers.Count:N0}", "Driver profitability, jobs, trucks, and garage history", "Open list", string.Empty, null, company.Drivers.Sum(driver => driver.Profit), 0, company.Drivers.Count),
        Row("Cities", $"{company.Cities?.Count ?? 0:N0}", "Expansion candidates, route volume, and inbound/outbound profit", "Open list", string.Empty, null, company.Cities?.Sum(city => city.BidirectionalProfit) ?? 0, 0, company.Cities?.Count ?? 0)
    ];

    private IEnumerable<ExplorerRowSortEnvelope> SortRows(IEnumerable<ExplorerRowSortEnvelope> rows)
    {
        var comparer = StringComparer.CurrentCultureIgnoreCase;
        return (_sortColumn, _sortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(row => row.Item.Name, comparer),
            ("name", false) => rows.OrderBy(row => row.Item.Name, comparer),
            ("meta", true) => rows.OrderByDescending(row => row.MetaValue).ThenBy(row => row.Item.Name, comparer),
            ("meta", false) => rows.OrderBy(row => row.MetaValue).ThenBy(row => row.Item.Name, comparer),
            (_, false) => rows.OrderBy(row => row.PrimaryValue).ThenBy(row => row.Item.Name, comparer),
            _ => rows.OrderByDescending(row => row.PrimaryValue).ThenBy(row => row.Item.Name, comparer)
        };
    }

    private ExplorerRowSortEnvelope Row(
        string name,
        string primary,
        string secondary,
        string meta,
        string sparklineText,
        string? route,
        long primaryValue,
        long secondaryValue,
        int metaValue) =>
        new(new SortableExplorerRowItem(name, primary, secondary, meta, sparklineText, route, OpenRowCommand), primaryValue, secondaryValue, metaValue);

    private async Task OpenCompanyDetailAsync(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return;

        await _navigationController.GoToCompanyAsync(companyId);
    }

    private void ClearSelectedCompany()
    {
        _selectedCompanyId = null;
        HasSelectedCompany = false;
        SelectedCompanyTitle = "No company selected";
        SelectedCompanySubtitle = string.Empty;
        SelectedCompanyProfitText = "$0";
        SelectedCompanyMetrics.Clear();
        SelectedGarages.Clear();
        SelectedDrivers.Clear();
        SelectedTrucks.Clear();
        SelectedTrailers.Clear();
        SelectedRecentJobs.Clear();
        DetailTabs.Clear();
        ActiveDetailRows.Clear();
        ActiveRowsTitle = "Overview";
    }

    private async Task ApplyRecommendationsAsync(IReadOnlyCollection<CompanyDto> companies, DashboardQueryOptions options)
    {
        var company = companies.FirstOrDefault();
        if (company is null)
        {
            RecommendationText = "No company loaded for expansion recommendation.";
            TrailerRecommendationText = "No company loaded for trailer recommendation.";
            DiagnosisText = "No company loaded for underperformer diagnosis.";
            DriverSkillRecommendationText = "No company loaded for driver skill recommendation.";
            return;
        }

        var recommendation = await _recommendNextGarageCityUseCase.RecommendAsync(
            company.Id,
            options,
            CancellationToken.None);
        RecommendationText = recommendation is null
            ? "No eligible unowned garage city found."
            : $"Next garage: {recommendation.DisplayName} ({recommendation.ExpansionScore:0.##} score, {FormatMoney(recommendation.BidirectionalProfit)} route profit).";

        var garage = company.Garages
            .OrderByDescending(garage => garage.Profit)
            .ThenBy(garage => garage.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
        if (garage is null)
        {
            TrailerRecommendationText = "No garage loaded for trailer recommendation.";
            return;
        }

        var trailerRecommendation = (await _recommendTrailersForGarageUseCase.RecommendAsync(
                company.Id,
                garage.Id,
                options,
                CancellationToken.None,
                count: 1))
            .FirstOrDefault();
        TrailerRecommendationText = trailerRecommendation is null
            ? $"No profitable trailer type found for {garage.DisplayName}."
            : $"Trailer fit: {trailerRecommendation.TrailerType} for {garage.DisplayName} ({FormatMoney(trailerRecommendation.Profit)} across {trailerRecommendation.JobCount} jobs).";

        var diagnosis = (await _diagnoseUnderperformersUseCase.DiagnoseAsync(
                company.Id,
                options,
                CancellationToken.None,
                count: 1))
            .FirstOrDefault();
        DiagnosisText = diagnosis is null
            ? "No underperforming active assets found."
            : $"Watch: {diagnosis.EntityKind} {diagnosis.DisplayName} ({FormatMoney(diagnosis.Profit)} across {diagnosis.JobCount} jobs).";

        var driverSkillRecommendation = (await _recommendDriverSkillsUseCase.RecommendAsync(
                company.Id,
                options,
                CancellationToken.None,
                count: 1))
            .FirstOrDefault();
        DriverSkillRecommendationText = driverSkillRecommendation is null
            ? "No driver skill recommendation found."
            : $"Driver skill: {driverSkillRecommendation.DriverName} -> {driverSkillRecommendation.SkillName}.";
    }

    private static string FormatMoney(long value) =>
        string.Create(CultureInfo.CurrentCulture, $"{value:C0}");

    private static string FormatValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

    private static string GetGarageDisplayName(CompanyDto company, string? garageId) =>
        string.IsNullOrWhiteSpace(garageId)
            ? "-"
            : company.Garages.FirstOrDefault(garage => IdEquals(garage.Id, garageId))?.DisplayName ?? garageId;

    private static string GetDriverDisplayName(CompanyDto company, string? driverId) =>
        string.IsNullOrWhiteSpace(driverId)
            ? "-"
            : company.Drivers.FirstOrDefault(driver => IdEquals(driver.Id, driverId))?.DisplayName ?? driverId;

    private static string GetTruckDisplayName(CompanyDto company, string? truckId) =>
        string.IsNullOrWhiteSpace(truckId)
            ? "-"
            : company.Trucks.FirstOrDefault(truck => IdEquals(truck.Id, truckId))?.DisplayName ?? truckId;

    private static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    private static string SparklineText(SparklineDto? sparkline)
    {
        var points = sparkline?.Points;
        if (points is null || points.Count == 0)
            return string.Empty;

        var first = points.First().Value;
        var last = points.Last().Value;
        var direction = last.CompareTo(first) switch
        {
            > 0 => "up",
            < 0 => "down",
            _ => "flat"
        };
        return $"{direction} {points.Count:N0} pts";
    }

    private static string RouteToCompany(string companyId) =>
        $"company?companyId={Uri.EscapeDataString(companyId)}";

    private static string RouteToGarage(string companyId, string garageId) =>
        $"garage?companyId={Uri.EscapeDataString(companyId)}&garageId={Uri.EscapeDataString(garageId)}";

    private static string RouteToDriver(string companyId, string driverId) =>
        $"driver?companyId={Uri.EscapeDataString(companyId)}&driverId={Uri.EscapeDataString(driverId)}";

    private static string RouteToTruck(string companyId, string truckId) =>
        $"truck?companyId={Uri.EscapeDataString(companyId)}&truckId={Uri.EscapeDataString(truckId)}";

    private static string RouteToTrailer(string companyId, string licensePlate) =>
        $"trailer?companyId={Uri.EscapeDataString(companyId)}&licensePlate={Uri.EscapeDataString(licensePlate)}";

    private static string RouteToJob(string companyId, string jobId) =>
        $"job?companyId={Uri.EscapeDataString(companyId)}&jobId={Uri.EscapeDataString(jobId)}";

    private static string RouteToCity(string companyId, string cityId) =>
        $"city?companyId={Uri.EscapeDataString(companyId)}&cityId={Uri.EscapeDataString(cityId)}";

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private sealed record ExplorerRowSortEnvelope(
        SortableExplorerRowItem Item,
        long PrimaryValue,
        long SecondaryValue,
        int MetaValue);
}
