namespace AtsEmployeeStats.Infrastructure.Saves;

internal static class LocalPath
{
    public static string Normalize(string path)
    {
        var normalized = path.Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(normalized)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullPath.Length >= 2 && fullPath[1] == ':')
            fullPath = char.ToUpperInvariant(fullPath[0]) + fullPath[1..];

        return fullPath;
    }

    public static string? NormalizeOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Normalize(path);
}
