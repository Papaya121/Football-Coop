using System;
using System.Collections.Generic;

[Serializable]
public sealed class FootballLocalizationData
{
    public string defaultLanguage = "ru";
    public List<string> languages = new List<string> { "ru", "en" };
    public List<FootballLocalizationEntry> entries = new List<FootballLocalizationEntry>();

    public string GetTranslation(FootballLocalizationEntry entry, string language)
    {
        int index = languages.IndexOf(language);
        if (index >= 0 && index < entry.values.Count && !string.IsNullOrEmpty(entry.values[index]))
            return entry.values[index];
        index = languages.IndexOf(defaultLanguage);
        return index >= 0 && index < entry.values.Count && !string.IsNullOrEmpty(entry.values[index])
            ? entry.values[index] : entry.source;
    }
}

[Serializable]
public sealed class FootballLocalizationEntry
{
    public string key;
    // Stable source text connects existing scene labels and scripted messages to the table.
    public string source;
    public List<string> values = new List<string>();
}
