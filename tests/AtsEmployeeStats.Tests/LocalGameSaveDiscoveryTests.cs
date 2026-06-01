using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Infrastructure.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class LocalGameSaveDiscoveryTests
{
    [Fact]
    public async Task FindCandidateRootsAsync_returns_ats_document_and_steam_candidates()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Steam\\userdata",
            "C:\\Users\\James\\Documents\\American Truck Simulator\\profiles",
            "C:\\Steam\\userdata\\123\\270880\\remote\\steam_profiles"
        };
        var discovery = new LocalGameSaveDiscovery(
            documentsPath: "C:\\Users\\James\\Documents",
            steamPath: "C:\\Steam",
            directoryExists: existing.Contains,
            enumerateDirectories: path => path == "C:\\Steam\\userdata" ? ["C:\\Steam\\userdata\\123"] : []);

        var roots = await discovery.FindCandidateRootsAsync(GameSaveKind.AmericanTruckSimulator, CancellationToken.None);

        Assert.Contains(roots, root => root.Path == "C:\\Users\\James\\Documents\\American Truck Simulator\\profiles" && root.Exists);
        Assert.Contains(roots, root => root.Path == "C:\\Steam\\userdata\\123\\270880\\remote\\steam_profiles" && root.Exists);
    }

    [Fact]
    public async Task FindCandidateRootsAsync_returns_ets2_candidates()
    {
        var discovery = new LocalGameSaveDiscovery(
            documentsPath: "C:\\Users\\James\\Documents",
            steamPath: null,
            directoryExists: path => path == "C:\\Users\\James\\Documents\\Euro Truck Simulator 2\\profiles",
            enumerateDirectories: _ => []);

        var roots = await discovery.FindCandidateRootsAsync(GameSaveKind.EuroTruckSimulator2, CancellationToken.None);

        Assert.Contains(roots, root => root.Path == "C:\\Users\\James\\Documents\\Euro Truck Simulator 2\\profiles" && root.Exists);
    }
}
