namespace AtsEmployeeStats.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState) =>
        new(_mainPage)
        {
            Title = "ATS Employee Stats"
        };
}
