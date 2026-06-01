namespace AtsEmployeeStats.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState) =>
        new(_shell)
        {
            Title = "ATS Employee Stats"
        };
}
