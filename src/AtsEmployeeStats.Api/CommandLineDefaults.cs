namespace AtsEmployeeStats.Api;

internal static class CommandLineDefaults
{
    public static string DefaultDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsEmployeeStats");
}
