using AtsEmployeeStats.Maui.Controllers;
using AtsEmployeeStats.Maui.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtsEmployeeStats.Maui.Views;

[QueryProperty(nameof(CompanyId), "companyId")]
[QueryProperty(nameof(TruckId), "truckId")]
public partial class TruckDetailPage : ContentPage
{
    private DetailNavigationController? _controller;
    private readonly DetailPageModel _model = new();
    private bool _loaded;

    public TruckDetailPage()
        : this(controller: null)
    {
    }

    public TruckDetailPage(DetailNavigationController? controller)
    {
        _controller = controller;
        InitializeComponent();
        Content = DetailPageLayout.Create(route => Controller.GoToRouteAsync(route));
        BindingContext = _model;
    }

    public string CompanyId { get; set; } = string.Empty;

    public string TruckId { get; set; } = string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        _loaded = true;
        await Controller.LoadTruckAsync(_model, CompanyId, TruckId, CancellationToken.None);
    }

    private DetailNavigationController Controller =>
        _controller ??= Handler?.MauiContext?.Services.GetRequiredService<DetailNavigationController>()
            ?? throw new InvalidOperationException("Detail navigation controller is unavailable.");
}
