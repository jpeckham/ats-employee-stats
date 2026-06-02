using System.Reflection;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Statistics;
using AtsEmployeeStats.Infrastructure.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void Domain_has_no_outward_project_references()
    {
        var references = ProjectReferences(typeof(AtsStatistics).Assembly);

        Assert.DoesNotContain(references, name => name.StartsWith("AtsEmployeeStats.Application", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("AtsEmployeeStats.Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("AtsEmployeeStats.Maui", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_reference_delivery_or_infrastructure_projects()
    {
        var references = ProjectReferences(typeof(StatisticsService).Assembly);

        Assert.DoesNotContain(references, name => name.StartsWith("AtsEmployeeStats.Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("AtsEmployeeStats.Maui", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_depends_inward_on_application_and_domain_only()
    {
        var references = ProjectReferences(typeof(SqliteMedallionSaveSnapshotSource).Assembly);

        Assert.Contains(references, name => name == "AtsEmployeeStats.Application");
        Assert.Contains(references, name => name == "AtsEmployeeStats.Domain");
        Assert.DoesNotContain(references, name => name.StartsWith("AtsEmployeeStats.Maui", StringComparison.Ordinal));
    }

    private static IReadOnlySet<string> ProjectReferences(Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name)
            .Where(name => name is not null && name.StartsWith("AtsEmployeeStats.", StringComparison.Ordinal))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
}
