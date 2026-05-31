using System.Text;
using System.Text.RegularExpressions;
using AtsEmployeeStats.Domain.Saves;


namespace AtsEmployeeStats.Infrastructure.Saves;

public static partial class SiiSaveParser
{
    public static SiiDocument Parse(string content, IProgress<int>? unitProgress = null)
    {
        var units = new List<SiiUnit>();
        var lines = content.Replace("\r\n", "\n").Split('\n');

        string? currentType = null;
        string? currentId = null;
        Dictionary<string, string>? values = null;
        Dictionary<string, SortedDictionary<int, string>>? arrays = null;

        foreach (var rawLine in lines)
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0 || line is "SiiNunit" or "{")
            {
                continue;
            }

            if (line == "}")
            {
                if (currentType is not null && currentId is not null && values is not null && arrays is not null)
                {
                    units.Add(new SiiUnit(
                        currentType,
                        currentId,
                        values,
                        arrays.ToDictionary(
                            pair => pair.Key,
                            pair => (IReadOnlyList<string>)ToIndexedList(pair.Value),
                            StringComparer.OrdinalIgnoreCase)));

                    currentType = null;
                    currentId = null;
                    values = null;
                    arrays = null;
                    unitProgress?.Report(units.Count);
                }

                continue;
            }

            var unitMatch = UnitStartRegex().Match(line);
            if (unitMatch.Success)
            {
                currentType = unitMatch.Groups["type"].Value;
                currentId = unitMatch.Groups["id"].Value;
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                arrays = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (currentType is null || values is null || arrays is null)
            {
                continue;
            }

            var fieldMatch = FieldRegex().Match(line);
            if (!fieldMatch.Success)
            {
                continue;
            }

            var key = fieldMatch.Groups["key"].Value;
            var value = Unquote(fieldMatch.Groups["value"].Value.Trim());
            var indexGroup = fieldMatch.Groups["index"];

            if (indexGroup.Success)
            {
                if (!arrays.TryGetValue(key, out var array))
                {
                    array = [];
                    arrays[key] = array;
                }

                var index = string.IsNullOrEmpty(indexGroup.Value)
                    ? array.Count   // append syntax key[]: uses next sequential slot
                    : int.Parse(indexGroup.Value);
                array[index] = value;
            }
            else
            {
                values[key] = value;
            }
        }

        return new SiiDocument(units);
    }

    public static int CountUnits(string content)
    {
        var count = 0;
        var lines = content.Replace("\r\n", "\n").Split('\n');
        foreach (var rawLine in lines)
        {
            var line = StripComment(rawLine).Trim();
            if (UnitStartRegex().IsMatch(line))
            {
                count++;
            }
        }

        return count;
    }

    private static string StripComment(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && line[i] == '/' && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static List<string> ToIndexedList(SortedDictionary<int, string> values)
    {
        if (values.Count == 0)
        {
            return [];
        }

        var indexed = Enumerable.Repeat(string.Empty, values.Keys.Max() + 1).ToList();
        foreach (var (index, value) in values)
        {
            indexed[index] = value;
        }

        return indexed;
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        var builder = new StringBuilder(value.Length - 2);
        for (var i = 1; i < value.Length - 1; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length - 1)
            {
                i++;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Normalizes SII content where the unit-opening brace appears on its own line
    /// (common in locale files) into the inline form the parser expects.
    /// <c>localization_db : .x\n{</c> → <c>localization_db : .x {</c>
    /// </summary>
    public static string NormalizeSeparateBrace(string content) =>
        SeparateBraceRegex().Replace(content, "$1 {");

    [GeneratedRegex(@"(?m)^([ \t]*[A-Za-z0-9_\.]+[ \t]*:[ \t]*[^\s{]+)[ \t]*\r?\n[ \t]*\{[ \t]*$")]
    private static partial Regex SeparateBraceRegex();

    [GeneratedRegex(@"^(?<type>[A-Za-z0-9_\.]+)\s*:\s*(?<id>[^\s{]+)\s*\{\s*$")]
    private static partial Regex UnitStartRegex();

    [GeneratedRegex(@"^(?<key>[A-Za-z0-9_]+)(?:\[(?<index>\d*)\])?\s*:\s*(?<value>.+?)\s*$")]
    private static partial Regex FieldRegex();
}
