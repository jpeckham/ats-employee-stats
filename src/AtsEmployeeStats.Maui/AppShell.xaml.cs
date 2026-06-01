namespace AtsEmployeeStats.Maui;

public partial class AppShell : Shell
{
	public AppShell(MainPage mainPage)
	{
		InitializeComponent();
        MainShellContent.Content = mainPage;
        Routing.RegisterRoute("company", typeof(Views.CompanyDetailPage));
        Routing.RegisterRoute("garage", typeof(Views.GarageDetailPage));
        Routing.RegisterRoute("driver", typeof(Views.DriverDetailPage));
        Routing.RegisterRoute("truck", typeof(Views.TruckDetailPage));
        Routing.RegisterRoute("trailer", typeof(Views.TrailerDetailPage));
        Routing.RegisterRoute("job", typeof(Views.JobDetailPage));
        Routing.RegisterRoute("city", typeof(Views.CityDetailPage));
	}
}
