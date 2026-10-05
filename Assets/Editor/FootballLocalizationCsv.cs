using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class FootballLocalizationCsv
{
    public static string Export(FootballLocalizationData data)
    {
        Validate(data);
        var result = new StringBuilder();
        WriteRow(result, new[] { "key", "source" }.Concat(data.languages));
        foreach (var entry in data.entries)
            WriteRow(result, new[] { entry.key, entry.source }.Concat(entry.values));
        return result.ToString();
    }

    // Merge on a copy: a rejected file never changes the current table.
    public static FootballLocalizationData Import(string csv, FootballLocalizationData current)
    {
        Validate(current);
        var rows = Parse(csv);
        if (rows.Count == 0 || rows[0].Count < 3 || rows[0][0] != "key" || rows[0][1] != "source")
            throw new FormatException("CSV header must be key,source,ru,en (or other language codes).");
        var languages = rows[0].Skip(2).ToList();
        ValidateLanguages(languages);
        var data = new FootballLocalizationData
        {
            defaultLanguage = current.defaultLanguage,
            languages = current.languages.Concat(languages).Distinct().ToList()
        };
        foreach (var entry in current.entries)
        {
            var copy = new FootballLocalizationEntry { key = entry.key, source = entry.source };
            for (int i = 0; i < data.languages.Count; i++)
                copy.values.Add(i < entry.values.Count ? entry.values[i] : "");
            data.entries.Add(copy);
        }
        var keys = new HashSet<string>();
        for (int row = 1; row < rows.Count; row++)
        {
            var cells = rows[row];
            if (cells.All(string.IsNullOrEmpty))
                continue;
            if (cells.Count != rows[0].Count)
                throw new FormatException($"CSV row {row + 1}: expected {rows[0].Count} columns, got {cells.Count}.");
            string key = cells[0].Trim();
            if (string.IsNullOrEmpty(key) || !keys.Add(key))
                throw new FormatException($"CSV row {row + 1}: empty or duplicate key '{key}'.");
            var entry = data.entries.Find(item => item.key == key);
            if (entry == null)
            {
                entry = new FootballLocalizationEntry { key = key, source = cells[1] };
                entry.values = data.languages.Select(_ => "").ToList();
                data.entries.Add(entry);
            }
            else if (entry.source != cells[1])
                throw new FormatException($"CSV row {row + 1}: source for '{key}' is immutable; edit language columns instead.");
            for (int column = 0; column < languages.Count; column++)
                entry.values[data.languages.IndexOf(languages[column])] = cells[column + 2];
        }
        Validate(data);
        return data;
    }

    public static void Validate(FootballLocalizationData data)
    {
        if (data == null || data.languages == null || data.entries == null)
            throw new FormatException("Invalid localization table.");
        ValidateLanguages(data.languages);
        if (!data.languages.Contains(data.defaultLanguage))
            throw new FormatException("Default language must exist in the table.");
        var keys = new HashSet<string>();
        var sources = new HashSet<string>();
        foreach (var entry in data.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || !keys.Add(entry.key))
                throw new FormatException("Empty or duplicate localization key.");
            if (!string.IsNullOrEmpty(entry.source) && !sources.Add(entry.source))
                throw new FormatException("Duplicate source: " + entry.source);
            if (entry.values == null || entry.values.Count != data.languages.Count)
                throw new FormatException("Translation column count does not match for " + entry.key);
            var expected = Placeholders(entry.source);
            for (int i = 0; i < entry.values.Count; i++)
            {
                if (!string.IsNullOrEmpty(entry.values[i]) && !expected.SetEquals(Placeholders(entry.values[i])))
                    throw new FormatException($"{entry.key} ({data.languages[i]}): placeholders must match the source text.");
            }
        }
    }

    private static HashSet<string> Placeholders(string text)
    {
        string unescaped = (text ?? "").Replace("{{", "").Replace("}}", "");
        const string pattern = @"\{(?:(?<name>[A-Za-z_][A-Za-z0-9_]*)|(?<index>\d+)(?:,\s*[+-]?\d+\s*)?(?::[^{}]+)?)\}";
        string remainder = Regex.Replace(unescaped, pattern, "");
        if (remainder.Contains("{") || remainder.Contains("}"))
            throw new FormatException("Invalid format placeholder: " + text);
        return new HashSet<string>(Regex.Matches(unescaped, pattern).Cast<Match>()
            .Select(match => match.Groups["name"].Success ? match.Groups["name"].Value : match.Groups["index"].Value));
    }

    private static void ValidateLanguages(List<string> languages)
    {
        if (languages.Count == 0 || languages.Distinct(StringComparer.OrdinalIgnoreCase).Count() != languages.Count ||
            languages.Any(language => !Regex.IsMatch(language ?? "", @"^[a-z]{2,3}(?:-[a-zA-Z0-9]{2,8})*$")))
            throw new FormatException("Language columns must have unique codes, e.g. ru, en, en-US.");
    }

    private static void WriteRow(StringBuilder result, IEnumerable<string> cells)
    {
        result.AppendLine(string.Join(",", cells.Select(cell => "\"" + (cell ?? "").Replace("\"", "\"\"") + "\"")));
    }

    public static List<List<string>> Parse(string csv)
    {
        csv = (csv ?? "").TrimStart('\uFEFF');
        // Excel may use semicolons depending on the system's regional settings.
        string header = csv.Split('\n')[0];
        char separator = header.Contains(";") && !header.Contains(",") ? ';' : ',';
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        bool closedQuote = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char character = csv[i];
            if (quoted)
            {
                if (character != '"')
                    cell.Append(character);
                else if (i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                    closedQuote = true;
                }
                continue;
            }
            if (character == separator || character == '\r' || character == '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                closedQuote = false;
                if (character != separator)
                {
                    rows.Add(row);
                    row = new List<string>();
                    if (character == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                        i++;
                }
            }
            else if (character == '"' && cell.Length == 0 && !closedQuote)
                quoted = true;
            else if (closedQuote || character == '"')
                throw new FormatException("Invalid CSV quoting near character " + i);
            else
                cell.Append(character);
        }
        if (quoted)
            throw new FormatException("CSV contains an unterminated quoted field.");
        if (cell.Length > 0 || row.Count > 0 || closedQuote)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
