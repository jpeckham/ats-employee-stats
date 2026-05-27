using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

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

public sealed class ScsReferenceDataIngestor(
    AtsReferenceDataOptions options,
    IScsExtractorDownloader? downloader = null,
    IScsArchiveExtractor? archiveExtractor = null)
{
    private readonly ScsExtractorBootstrapper _bootstrapper = new(downloader);
    private readonly IScsArchiveExtractor _archiveExtractor = archiveExtractor ?? new ProcessScsArchiveExtractor();

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
            var extractorPath = await _bootstrapper.EnsureExtractorAsync(cacheRoot, cancellationToken);
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
