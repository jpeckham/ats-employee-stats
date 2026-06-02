using System.Collections.ObjectModel;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static AtsEmployeeStats.Wpf.ViewModels.DetailHelpers;

namespace AtsEmployeeStats.Wpf.ViewModels;

public sealed partial class MainWindowViewModel(
    IStatisticsDashboardUseCases dashboardUseCases,
    IStatisticsReloadUseCase reloadUseCase) : ObservableObject
{
    private readonly DashboardQueryRequest _query = new();
    private DashboardStatisticsDto? _dashboard;

    [ObservableProperty]
    private CompanyExplorerViewModel explorer = new();

    [ObservableProperty]
    private EntityDetailViewModel? selectedDetail;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool isBusy;

    public async Task LoadAsync()
    {
        if (_dashboard is null)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusText = "Loading local statistics...";
            _dashboard = await dashboardUseCases.GetDashboardAsync(_query.ToOptions(), CancellationToken.None);
            BuildExplorer(_dashboard.Companies);
            SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);
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

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusText = "Reloading local save statistics...";
            _dashboard = await reloadUseCase.ReloadAsync(_query.ToOptions(), CancellationToken.None);
            BuildExplorer(_dashboard.Companies);
            SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);
            StatusText = $"Reloaded {_dashboard.Companies.Count:N0} companies";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to reload statistics: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenRow(GridRowViewModel? row)
    {
        if (row?.Target is not { } target)
            return;

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
    }

    private void BuildExplorer(IReadOnlyList<CompanyDto> companies)
    {
        var root = new ExplorerNodeViewModel("Companies", ExplorerNodeKind.Companies);
        foreach (var company in companies.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var companyNode = new ExplorerNodeViewModel(company.DisplayName, ExplorerNodeKind.Company, company.Id);
            AddCollection(companyNode, "Garages", ExplorerNodeKind.Garages, company.Id, company.Garages.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Garage, company.Id, item.Id)));
            AddCollection(companyNode, "Drivers", ExplorerNodeKind.Drivers, company.Id, company.Drivers.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Driver, company.Id, item.Id)));
            AddCollection(companyNode, "Trucks", ExplorerNodeKind.Trucks, company.Id, company.Trucks.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Truck, company.Id, item.Id)));
            AddCollection(companyNode, "Trailers", ExplorerNodeKind.Trailers, company.Id, (company.Trailers ?? []).Select(item => new ExplorerNodeViewModel(item.LicensePlate ?? item.Id, ExplorerNodeKind.Trailer, company.Id, item.LicensePlate ?? item.Id)));
            AddCollection(companyNode, "Jobs", ExplorerNodeKind.Jobs, company.Id, company.Missions.Take(250).Select(item => new ExplorerNodeViewModel(string.IsNullOrWhiteSpace(item.Cargo) ? item.Id : item.Cargo!, ExplorerNodeKind.Job, company.Id, item.Id)));
            AddCollection(companyNode, "Cities", ExplorerNodeKind.Cities, company.Id, (company.Cities ?? []).Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.City, company.Id, item.Id)));
            root.Children.Add(companyNode);
        }

        Explorer.Roots.Clear();
        Explorer.Roots.Add(root);
    }

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
}

public sealed class CompaniesDetailViewModel : EntityDetailViewModel
{
    public CompaniesDetailViewModel(IReadOnlyList<CompanyDto> companies)
        : base("Companies", "All trucking companies", RowFormatting.Money(companies.Sum(company => company.Profit)))
    {
        Metrics.Add(new("Companies", RowFormatting.Count(companies.Count)));
        Metrics.Add(new("Profit", RowFormatting.Money(companies.Sum(company => company.Profit))));
        Metrics.Add(new("Drivers", RowFormatting.Count(companies.Sum(company => company.Drivers.Count))));
        Metrics.Add(new("Trucks", RowFormatting.Count(companies.Sum(company => company.Trucks.Count))));
        Tabs.Add(new("Companies", companies.Select(company => new GridRowViewModel(
            company.DisplayName,
            RowFormatting.Money(company.Profit),
            $"{company.Garages.Count:N0} garages / {company.Drivers.Count:N0} drivers / {company.Trucks.Count:N0} trucks",
            $"{company.Missions.Count:N0} jobs",
            RowFormatting.Trend(company.ProfitTrend),
            company)
        {
            Target = new(ExplorerNodeKind.Company, company.Id),
            ProfitSort = company.Profit
        }), TableColumns.Companies));
    }
}
