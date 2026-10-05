using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static void Reject(Action action, string message)
    {
        try { action(); } catch (FormatException) { _checks++; return; }
        throw new Exception(message);
    }

    private static void Main()
    {
        var sample = new FootballLocalizationData();
        sample.entries.Add(new FootballLocalizationEntry
        {
            key = "sample", source = "Hello, \"world\";\nПривет {0}!",
            values = new List<string> { "Hello, \"world\";\nПривет {0}!", "Hello, \"world\";\nHello {0}!" }
        });
        string csv = FootballLocalizationCsv.Export(sample);
        var restored = FootballLocalizationCsv.Import("\uFEFF" + csv, new FootballLocalizationData());
        Check(restored.entries[0].source == sample.entries[0].source, "Unicode, quoting, commas and multiline source round trip");
        Check(restored.entries[0].values[1] == sample.entries[0].values[1], "Multiline translation round trip");
        Check(FootballLocalizationCsv.Parse("a,b,\r\n\"\",\"a\"\"b\",\"\"")[1][1] == "a\"b", "Escaped quotes and trailing empty fields");
        Check(FootballLocalizationCsv.Parse("key;source;ru\r\na;hello;test")[1][2] == "test", "Regional semicolon CSV");
        var added = FootballLocalizationCsv.Import("key,source,de\nnew,new,neu\n", sample);
        Check(added.languages.Count == 3 && added.entries.Count == 2, "New languages and keys merge");
        Check(added.entries[0].values[1] == sample.entries[0].values[1], "Missing rows and languages preserved");
        Check(sample.languages.Count == 2 && sample.entries.Count == 1, "Import leaves original untouched");
        var cleared = FootballLocalizationCsv.Import("key,source,en\nsample,\"Hello, \"\"world\"\";\nПривет {0}!\",\"\"", sample);
        Check(cleared.entries[0].values[1] == "", "Empty translation clears to fallback");
        Check(cleared.GetTranslation(cleared.entries[0], "en") == sample.entries[0].source, "Empty translation uses default language");
        Check(sample.GetTranslation(sample.entries[0], "de") == sample.entries[0].source, "Unsupported language uses default language");
        cleared.entries[0].values[0] = "";
        Check(cleared.GetTranslation(cleared.entries[0], "en") == sample.entries[0].source, "Empty default translation uses original source");
        Reject(() => FootballLocalizationCsv.Parse("\"open"), "Unterminated quote accepted");
        Reject(() => FootballLocalizationCsv.Parse("\"closed\"suffix"), "Text after closing quote accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\na,a,A\na,a,B", sample), "Duplicate keys accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en,en\na,a,A,B", sample), "Duplicate language accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\na,a", sample), "Missing column accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\na,a,A,extra", sample), "Extra column accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\n,x,X", sample), "Empty key accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\nsample,changed,Changed", sample), "Stable source modified");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\nnumber,Value {0},Value", sample), "Dropped numeric placeholder accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\nnamed,Press {jump},Press {kick}", sample), "Changed named placeholder accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\nnamed,Press {jump},Press {jump broken}", sample), "Malformed named placeholder accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\nnumber,Value {0},Value {0} {", sample), "Unmatched brace accepted");
        Reject(() => FootballLocalizationCsv.Import("key,source,en\na,same,A\nb,same,B", sample), "Duplicate source accepted");
        var table = JsonSerializer.Deserialize<FootballLocalizationData>(
            File.ReadAllText("Assets/Resources/FootballLocalization.json"), new JsonSerializerOptions { IncludeFields = true });
        FootballLocalizationCsv.Validate(table);
        var emptyTable = new FootballLocalizationData { defaultLanguage = table.defaultLanguage, languages = new List<string>(table.languages) };
        var roundTrip = FootballLocalizationCsv.Import(FootballLocalizationCsv.Export(table), emptyTable);
        Check(JsonSerializer.Serialize(roundTrip, new JsonSerializerOptions { IncludeFields = true }) ==
            JsonSerializer.Serialize(table, new JsonSerializerOptions { IncludeFields = true }), "Full project table round trip");
        var rating = table.entries.Find(entry => entry.key == "result.rating");
        Check(string.Format(table.GetTranslation(rating, "en"), 1200, 1210) == "Rating: 1200 → 1210", "Numeric runtime parameters retain values in English");
        var tutorial = table.entries.Find(entry => entry.key == "tutorial.double");
        Check(table.GetTranslation(tutorial, "en").Replace("{jump}", "SPACE") ==
            "While airborne, press SPACE again to jump higher.", "Named tutorial controls retain values in English");
        Console.WriteLine($"Passed {_checks} localization checks; {table.entries.Count} entries validated.");
    }
}
