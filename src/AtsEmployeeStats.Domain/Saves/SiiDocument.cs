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
    IReadOnlyDictionary<string, IReadOnlyList<string>> Arrays)
{
    public string? GetValue(string key) =>
        Values.TryGetValue(key, out var value) ? value : null;

    public IReadOnlyList<string> GetArray(string key) =>
        Arrays.TryGetValue(key, out var value) ? value : [];
}

public sealed record SaveSnapshot(
    string Name,
    DateTimeOffset LastWritten,
    SiiDocument Document);
