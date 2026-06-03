namespace AtsEmployeeStats.Infrastructure.Saves;

public static class LocalSteamPath
{
    public static IReadOnlyList<string> FindLibraries()
    {
        var root = Find();
        return FindLibraries(root);
    }

    public static IReadOnlyList<string> FindLibraries(string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return [];

        root = LocalPath.Normalize(root);
        var libraries = new List<string> { root };
        var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile))
        {
            try
            {
                libraries.AddRange(ParseLibraryFolders(File.ReadAllText(libraryFile)));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
            }
        }

        return libraries
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(LocalPath.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? Find()
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
                    return LocalPath.Normalize(regPath);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
            }
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam");
        return Directory.Exists(defaultPath) ? LocalPath.Normalize(defaultPath) : null;
    }

    private static IEnumerable<string> ParseLibraryFolders(string content)
    {
        foreach (var line in content.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                yield return LocalPath.Normalize(parts[^1].Replace(@"\\", @"\"));
        }
    }
}
