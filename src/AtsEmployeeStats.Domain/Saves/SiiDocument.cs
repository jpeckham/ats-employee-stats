namespace AtsEmployeeStats.Domain.Saves;

public sealed record SiiDocument(IReadOnlyList<SiiUnit> Units)
{
    public IEnumerable<SiiUnit> UnitsOfType(string type) =>
        Units.Where(unit => StringComparer.OrdinalIgnoreCase.Equals(unit.Type, type));
}

public sealed record SiiUnit(
    string Type,
    string Id,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Arrays)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyArray =
        new Dictionary<string, string>();

    public string? GetValue(string key) =>
        Values.TryGetValue(key, out var value) ? value : null;

    public IReadOnlyDictionary<string, string> GetArray(string key) =>
        Arrays.TryGetValue(key, out var value) ? value : EmptyArray;
}

public sealed record SaveSnapshot(
    string Name,
    DateTimeOffset LastWritten,
    SiiDocument Document,
    string? SourceKey = null);
