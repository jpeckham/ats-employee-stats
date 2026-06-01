using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Maui.Presentation;

namespace AtsEmployeeStats.Maui.Controllers;

public sealed class DetailNavigationController(IStatisticsDashboardUseCases dashboardUseCases)
{
    internal Task LoadCompanyAsync(
        IDetailPresentationTarget target,
        string companyId,
        CancellationToken cancellationToken) =>
        ExecuteCompanyAsync(target, companyId, new CompanyDetailPresenter(target), cancellationToken);

    internal Task LoadGarageAsync(
        IDetailPresentationTarget target,
        string companyId,
        string garageId,
        CancellationToken cancellationToken) =>
        ExecuteCompanyAsync(target, companyId, new GarageDetailPresenter(target, garageId), cancellationToken);

    internal Task LoadDriverAsync(
        IDetailPresentationTarget target,
        string companyId,
        string driverId,
        CancellationToken cancellationToken) =>
        ExecuteCompanyAsync(target, companyId, new DriverDetailPresenter(target, driverId), cancellationToken);

    internal Task LoadTruckAsync(
        IDetailPresentationTarget target,
        string companyId,
        string truckId,
        CancellationToken cancellationToken) =>
        ExecuteCompanyAsync(target, companyId, new TruckDetailPresenter(target, truckId), cancellationToken);

    internal Task LoadTrailerAsync(
        IDetailPresentationTarget target,
        string companyId,
        string licensePlate,
        CancellationToken cancellationToken) =>
        ExecuteCompanyAsync(target, companyId, new TrailerDetailPresenter(target, licensePlate), cancellationToken);

    internal Task LoadJobAsync(
        IDetailPresentationTarget target,
        string companyId,
        string jobId,
        CancellationToken cancellationToken) =>
        ExecuteCompanyAsync(target, companyId, new JobDetailPresenter(target, jobId), cancellationToken);

    internal Task LoadCityAsync(
        IDetailPresentationTarget target,
        string companyId,
        string cityId,
        CancellationToken cancellationToken) =>
        ExecuteCompanyAsync(target, companyId, new CityDetailPresenter(target, cityId), cancellationToken);

    public Task GoToCompanyAsync(string companyId) =>
        Shell.Current.GoToAsync($"company?companyId={Uri.EscapeDataString(companyId)}");

    public Task GoToGarageAsync(string companyId, string garageId) =>
        Shell.Current.GoToAsync($"garage?companyId={Uri.EscapeDataString(companyId)}&garageId={Uri.EscapeDataString(garageId)}");

    public Task GoToDriverAsync(string companyId, string driverId) =>
        Shell.Current.GoToAsync($"driver?companyId={Uri.EscapeDataString(companyId)}&driverId={Uri.EscapeDataString(driverId)}");

    public Task GoToTruckAsync(string companyId, string truckId) =>
        Shell.Current.GoToAsync($"truck?companyId={Uri.EscapeDataString(companyId)}&truckId={Uri.EscapeDataString(truckId)}");

    public Task GoToTrailerAsync(string companyId, string licensePlate) =>
        Shell.Current.GoToAsync($"trailer?companyId={Uri.EscapeDataString(companyId)}&licensePlate={Uri.EscapeDataString(licensePlate)}");

    public Task GoToJobAsync(string companyId, string jobId) =>
        Shell.Current.GoToAsync($"job?companyId={Uri.EscapeDataString(companyId)}&jobId={Uri.EscapeDataString(jobId)}");

    public Task GoToCityAsync(string companyId, string cityId) =>
        Shell.Current.GoToAsync($"city?companyId={Uri.EscapeDataString(companyId)}&cityId={Uri.EscapeDataString(cityId)}");

    public Task GoToRouteAsync(string route) =>
        string.IsNullOrWhiteSpace(route)
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(route);

    private async Task ExecuteCompanyAsync(
        IDetailPresentationTarget target,
        string companyId,
        IOutputBoundaryAdapter<CompanyDto?> presenter,
        CancellationToken cancellationToken)
    {
        target.ShowLoading("Loading");
        try
        {
            await dashboardUseCases.ExecuteCompanyAsync(
                presenter,
                new CompanyInputData(companyId, new DashboardQueryRequest()),
                progress: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            target.ShowError("Unable to load detail", ex.Message);
        }
    }
}
