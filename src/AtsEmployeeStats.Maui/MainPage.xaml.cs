using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Maui.Presentation;

namespace AtsEmployeeStats.Maui;

public partial class MainPage : ContentPage
{
    private bool _loaded;

    public MainPage(
        IStatisticsIngestUseCase ingestUseCase,
        IStatisticsDashboardUseCases dashboardUseCases,
        IRecommendNextGarageCityUseCase recommendNextGarageCityUseCase,
        IRecommendTrailersForGarageUseCase recommendTrailersForGarageUseCase,
        IRecommendDriverSkillsUseCase recommendDriverSkillsUseCase,
        IDiagnoseUnderperformersUseCase diagnoseUnderperformersUseCase)
    {
        InitializeComponent();
        BindingContext = new DashboardPageModel(
            ingestUseCase,
            dashboardUseCases,
            recommendNextGarageCityUseCase,
            recommendTrailersForGarageUseCase,
            recommendDriverSkillsUseCase,
            diagnoseUnderperformersUseCase);
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
    string AssetsText,
    ICommand ViewCommand);

public sealed record DetailMetricItem(string Label, string Value);

public sealed record DetailRowItem(
    string Name,
    string PrimaryText,
    string SecondaryText,
    string MetaText);

internal sealed class DashboardPageModel : INotifyPropertyChanged, IDashboardPresentationTarget
{
    private readonly IStatisticsIngestUseCase _ingestUseCase;
    private readonly IStatisticsDashboardUseCases _dashboardUseCases;
    private readonly IRecommendNextGarageCityUseCase _recommendNextGarageCityUseCase;
    private readonly IRecommendTrailersForGarageUseCase _recommendTrailersForGarageUseCase;
    private readonly IRecommendDriverSkillsUseCase _recommendDriverSkillsUseCase;
    private readonly IDiagnoseUnderperformersUseCase _diagnoseUnderperformersUseCase;
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
    private bool _hasSelectedCompany;
    private string _selectedCompanyTitle = "No company selected";
    private string _selectedCompanySubtitle = string.Empty;
    private string _selectedCompanyProfitText = "$0";
    private string _lastDashboardStatusText = "Refreshed statistics";

    public DashboardPageModel(
        IStatisticsIngestUseCase ingestUseCase,
        IStatisticsDashboardUseCases dashboardUseCases,
        IRecommendNextGarageCityUseCase recommendNextGarageCityUseCase,
        IRecommendTrailersForGarageUseCase recommendTrailersForGarageUseCase,
        IRecommendDriverSkillsUseCase recommendDriverSkillsUseCase,
        IDiagnoseUnderperformersUseCase diagnoseUnderperformersUseCase)
    {
        _ingestUseCase = ingestUseCase;
        _dashboardUseCases = dashboardUseCases;
        _recommendNextGarageCityUseCase = recommendNextGarageCityUseCase;
        _recommendTrailersForGarageUseCase = recommendTrailersForGarageUseCase;
        _recommendDriverSkillsUseCase = recommendDriverSkillsUseCase;
        _diagnoseUnderperformersUseCase = diagnoseUnderperformersUseCase;
        RefreshCommand = new Command(
            execute: async () => await RefreshAsync(),
            canExecute: () => !IsBusy);
        OpenCompanyCommand = new Command<string?>(OpenCompany);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CompanySummaryItem> Companies { get; } = [];

    public ObservableCollection<DetailMetricItem> SelectedCompanyMetrics { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedGarages { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedDrivers { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedTrucks { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedTrailers { get; } = [];

    public ObservableCollection<DetailRowItem> SelectedRecentJobs { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand OpenCompanyCommand { get; }

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
            var progress = new MauiProgressPresenter(this);
            var dashboardPresenter = new MauiDashboardPresenter(this);
            await _ingestUseCase.IngestAsync(CancellationToken.None, progress.AsProgress(CancellationToken.None), force: false);
            await _dashboardUseCases.ExecuteDashboardAsync(dashboardPresenter, request, progress, CancellationToken.None);
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
                company.AssetsText,
                OpenCompanyCommand));
        }

        CompanyCountText = presentation.CompanyCountText;
        TotalProfitText = presentation.TotalProfitText;
        DriverCountText = presentation.DriverCountText;
        _lastDashboardStatusText = presentation.RefreshedStatusText;

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
}
