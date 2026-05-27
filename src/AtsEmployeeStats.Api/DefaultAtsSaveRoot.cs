namespace AtsEmployeeStats.Api;

internal static class DefaultAtsSaveRoot
{
    private const string AtsAppId = "270880";

    public static string? Find()
    {
        var candidates = new List<string>();
        var steamPath = FindSteamPath();
        if (steamPath is not null)
        {
            var userdataRoot = Path.Combine(steamPath, "userdata");
            if (Directory.Exists(userdataRoot))
            {
                foreach (var userDir in Directory.EnumerateDirectories(userdataRoot))
                {
                    var remote = Path.Combine(userDir, AtsAppId, "remote");
                    candidates.Add(Path.Combine(remote, "profiles"));
                    candidates.Add(Path.Combine(remote, "steam_profiles"));
                    candidates.Add(remote);
                }
            }
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            var atsRoot = Path.Combine(documents, "American Truck Simulator");
            candidates.Add(atsRoot);
            candidates.Add(Path.Combine(atsRoot, "profiles"));
            candidates.Add(Path.Combine(atsRoot, "steam_profiles"));
        }

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? FindSteamPath()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (key?.GetValue("SteamPath") is string regPath &&
                    !string.IsNullOrWhiteSpace(regPath) &&
                    Directory.Exists(regPath))
                {
                    return regPath;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
            }
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam");
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }
}
