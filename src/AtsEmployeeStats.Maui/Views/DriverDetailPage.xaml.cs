using AtsEmployeeStats.Maui.Controllers;
using AtsEmployeeStats.Maui.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtsEmployeeStats.Maui.Views;

[QueryProperty(nameof(CompanyId), "companyId")]
[QueryProperty(nameof(DriverId), "driverId")]
public partial class DriverDetailPage : ContentPage
{
    private DetailNavigationController? _controller;
    private readonly DetailPageModel _model = new();
    private bool _loaded;

    public DriverDetailPage()
        : this(controller: null)
    {
    }

    public DriverDetailPage(DetailNavigationController? controller)
    {
        _controller = controller;
        InitializeComponent();
        Content = DetailPageLayout.Create(route => Controller.GoToRouteAsync(route));
        BindingContext = _model;
    }

    public string CompanyId { get; set; } = string.Empty;

    public string DriverId { get; set; } = string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        _loaded = true;
        await Controller.LoadDriverAsync(_model, CompanyId, DriverId, CancellationToken.None);
    }

    private DetailNavigationController Controller =>
        _controller ??= Handler?.MauiContext?.Services.GetRequiredService<DetailNavigationController>()
            ?? throw new InvalidOperationException("Detail navigation controller is unavailable.");
}
