using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using TruckLib.HashFs;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed record AtsReferenceDataOptions(
    bool Enabled = false,
    string? GameInstallRoot = null,
    string? CacheRoot = null);

public interface IScsExtractorDownloader
{
    Task DownloadAsync(Uri downloadUri, string destinationPath, CancellationToken cancellationToken);
}

public interface IScsArchiveExtractor
{
    Task ExtractAsync(string extractorPath, string archivePath, string outputDirectory, CancellationToken cancellationToken);
}

public sealed class ScsExtractorBootstrapper(IScsExtractorDownloader? downloader = null)
{
    public static Uri DefaultDownloadUri { get; } = new("https://download.eurotrucksimulator2.com/scs_extractor_1_55.zip");
    private readonly IScsExtractorDownloader _downloader = downloader ?? new HttpScsExtractorDownloader();

    public async Task<string> EnsureExtractorAsync(string cacheRoot, CancellationToken cancellationToken)
    {
        var toolsDirectory = Path.Combine(cacheRoot, "tools");
        Directory.CreateDirectory(toolsDirectory);
        var extractorPath = Path.Combine(toolsDirectory, "scs_extractor.exe");
        if (File.Exists(extractorPath))
        {
            return extractorPath;
        }

        var zipPath = Path.Combine(toolsDirectory, "scs_extractor.zip");
        await _downloader.DownloadAsync(DefaultDownloadUri, zipPath, cancellationToken);

        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(entry =>
            StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(entry.FullName), "scs_extractor.exe"));
        if (entry is null)
        {
            throw new InvalidOperationException("Downloaded SCS extractor archive did not contain scs_extractor.exe.");
        }

        entry.ExtractToFile(extractorPath, overwrite: true);
        return extractorPath;
    }
}

public sealed class HttpScsExtractorDownloader : IScsExtractorDownloader
{
    public async Task DownloadAsync(Uri downloadUri, string destinationPath, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        await using var source = await client.GetStreamAsync(downloadUri, cancellationToken);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }
}

public sealed class ProcessScsArchiveExtractor : IScsArchiveExtractor
{
    public async Task ExtractAsync(string extractorPath, string archivePath, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = extractorPath,
            ArgumentList = { archivePath, outputDirectory },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Could not start scs_extractor.exe.");
        }

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"scs_extractor.exe failed with exit code {process.ExitCode}: {error}");
        }
    }
}

/// <summary>
/// Extracts a HashFS v1/v2 .scs archive directly in-process using TruckLib.HashFs.
/// Does not require scs_extractor.exe, so it handles locale.scs (HashFS v2/CITY).
/// </summary>
public sealed class ScsHashFsExtractor : IScsArchiveExtractor
{
    public Task ExtractAsync(string extractorPath, string archivePath, string outputDirectory, CancellationToken cancellationToken)
    {
        return Task.Run(() => ExtractAll(archivePath, outputDirectory, cancellationToken), cancellationToken);
    }

    // SCS archives can omit the root "/" entry. These are the well-known top-level
    // directories we fall back to when "/" is absent (e.g. locale.scs uses "locale").
    private static readonly string[] KnownTopLevelDirs =
        ["locale", "def", "vehicle", "sounds", "map", "material"];

    private static void ExtractAll(string archivePath, string outputDirectory, CancellationToken cancellationToken)
    {
        using var reader = HashFsReader.Open(archivePath);
        if (reader.EntryExists("/") == EntryType.Directory)
        {
            ExtractDirectory(reader, "/", outputDirectory, cancellationToken);
            return;
        }

        bool found = false;
        foreach (var name in KnownTopLevelDirs)
        {
            if (reader.EntryExists("/" + name) == EntryType.Directory)
            {
                found = true;
                ExtractDirectory(reader, "/" + name, outputDirectory, cancellationToken);
            }
        }
        if (!found)
            throw new InvalidOperationException(
                $"Cannot traverse '{Path.GetFileName(archivePath)}': no root '/' and no known top-level directory found.");
    }

    private static void ExtractDirectory(IHashFsReader reader, string dirPath, string outputRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var listing = reader.GetDirectoryListing(dirPath, returnAbsolute: true);
        foreach (var file in listing.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = file.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var outputPath = Path.Combine(outputRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            reader.ExtractToFile(file, outputPath);
        }
        foreach (var subdir in listing.Subdirectories)
        {
            ExtractDirectory(reader, subdir, outputRoot, cancellationToken);
        }
    }
}

public sealed class ScsReferenceDataIngestor(
    AtsReferenceDataOptions options,
    IScsExtractorDownloader? downloader = null,
    IScsArchiveExtractor? archiveExtractor = null)
{
    private readonly ScsExtractorBootstrapper _bootstrapper = new(downloader);
    private readonly IScsArchiveExtractor _archiveExtractor = archiveExtractor ?? new ScsHashFsExtractor();

    public async Task<ExtractedReferenceData?> ExtractLocaleAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.GameInstallRoot))
        {
            return null;
        }

        var localeArchivePath = Path.Combine(options.GameInstallRoot, "locale.scs");
        if (!File.Exists(localeArchivePath))
        {
            return null;
        }

        var cacheRoot = options.CacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsEmployeeStats",
            "reference-cache");
        var archiveHash = await HashFileAsync(localeArchivePath, cancellationToken);
        var outputDirectory = Path.Combine(cacheRoot, "locale", archiveHash);
        var markerPath = Path.Combine(outputDirectory, ".extracted");

        if (!File.Exists(markerPath))
        {
            var extractorPath = _archiveExtractor is ProcessScsArchiveExtractor
                ? await _bootstrapper.EnsureExtractorAsync(cacheRoot, cancellationToken)
                : string.Empty;
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }

            Directory.CreateDirectory(outputDirectory);
            await _archiveExtractor.ExtractAsync(extractorPath, localeArchivePath, outputDirectory, cancellationToken);
            await File.WriteAllTextAsync(markerPath, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
        }

        return new ExtractedReferenceData(localeArchivePath, archiveHash, outputDirectory);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record ExtractedReferenceData(string ArchivePath, string ArchiveHash, string OutputDirectory);
