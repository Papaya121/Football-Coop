using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FootballLocalization
{
    public const string ResourceName = "FootballLocalization";
    private const string LanguagePreference = "Football.Language";
    private static FootballLocalizationData _data;
    private static readonly Dictionary<string, FootballLocalizationEntry> ByKey = new Dictionary<string, FootballLocalizationEntry>();
    private static readonly Dictionary<string, FootballLocalizationEntry> BySource = new Dictionary<string, FootballLocalizationEntry>();
    private static string _language;

    public static event Action LanguageChanged;
    public static string Language { get { EnsureLoaded(); return _language; } }
    public static IReadOnlyList<string> Languages { get { EnsureLoaded(); return _data.languages.AsReadOnly(); } }

#if UNITY_EDITOR
    public static string EditorLanguage
    {
        get => UnityEditor.EditorPrefs.GetString("Football.LocalizationPreview." + Application.dataPath, "");
        set
        {
            UnityEditor.EditorPrefs.SetString("Football.LocalizationPreview." + Application.dataPath, value ?? "");
            Reload();
        }
    }
#endif

    public static void SetLanguage(string language, bool persist = true)
    {
        EnsureLoaded();
        string supported = GetSupportedLanguage(language);
        if (supported == null)
            throw new ArgumentException("Unsupported language: " + language, nameof(language));
        language = supported;
        if (persist)
        {
            PlayerPrefs.SetString(LanguagePreference, language);
            PlayerPrefs.Save();
        }
        if (_language == language)
            return;
        _language = language;
        LanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        EnsureLoaded();
        return ByKey.TryGetValue(key ?? "", out var entry) ? Resolve(entry) : key ?? "";
    }

    public static string Localize(string source)
    {
        EnsureLoaded();
        return BySource.TryGetValue(source ?? "", out var entry) ? Resolve(entry) : source ?? "";
    }

    public static string Format(string source, params object[] arguments)
    {
        string value = Localize(source);
        return arguments == null || arguments.Length == 0
            ? value : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }

    public static void SetText(TMP_Text target, string source, params object[] arguments)
    {
        if (target == null)
            return;
        var label = target.GetComponent<FootballLocalizedText>();
        if (label == null)
            label = target.gameObject.AddComponent<FootballLocalizedText>();
        label.SetSource(source, arguments);
    }

    public static void BindHierarchy(GameObject root)
    {
        EnsureLoaded();
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            var input = label.GetComponentInParent<TMP_InputField>();
            if (input != null && input.textComponent == label)
                continue;
            if (label.GetComponent<FootballLocalizedText>() == null && BySource.ContainsKey(label.text ?? ""))
                SetText(label, label.text);
        }
    }

    public static void Reload()
    {
        _data = null;
        EnsureLoaded();
        LanguageChanged?.Invoke();
    }

    private static string Resolve(FootballLocalizationEntry entry)
    {
        return _data.GetTranslation(entry, _language);
    }

    private static void EnsureLoaded()
    {
        if (_data != null)
            return;
        var asset = Resources.Load<TextAsset>(ResourceName);
        _data = asset != null ? JsonUtility.FromJson<FootballLocalizationData>(asset.text) : new FootballLocalizationData();
        ByKey.Clear();
        BySource.Clear();
        foreach (var entry in _data.entries)
        {
            ByKey[entry.key] = entry;
            if (!string.IsNullOrEmpty(entry.source))
                BySource[entry.source] = entry;
        }
        string requested = PlayerPrefs.GetString(LanguagePreference, "");
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(EditorLanguage))
            requested = EditorLanguage;
#endif
        if (string.IsNullOrEmpty(requested))
            requested = GetSystemLanguageCode();
        _language = GetSupportedLanguage(requested) ?? _data.defaultLanguage;
    }

    private static string GetSupportedLanguage(string requested)
    {
        if (string.IsNullOrEmpty(requested))
            return null;
        string exact = _data.languages.Find(code => string.Equals(code, requested, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact;
        int separator = requested.IndexOf('-');
        string primary = separator >= 0 ? requested.Substring(0, separator) : requested;
        return _data.languages.Find(code => string.Equals(code, primary, StringComparison.OrdinalIgnoreCase)) ??
            _data.languages.Find(code => code.StartsWith(primary + "-", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetSystemLanguageCode()
    {
        // Additional language columns are also available to automatic language selection.
        return Application.systemLanguage switch
        {
            SystemLanguage.Russian => "ru",
            SystemLanguage.English => "en",
            SystemLanguage.German => "de",
            SystemLanguage.French => "fr",
            SystemLanguage.Spanish => "es",
            SystemLanguage.Italian => "it",
            SystemLanguage.Portuguese => "pt",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Korean => "ko",
            SystemLanguage.Chinese => "zh",
            SystemLanguage.ChineseSimplified => "zh-CN",
            SystemLanguage.ChineseTraditional => "zh-TW",
            SystemLanguage.Turkish => "tr",
            SystemLanguage.Arabic => "ar",
            SystemLanguage.Polish => "pl",
            SystemLanguage.Ukrainian => "uk",
            SystemLanguage.Dutch => "nl",
            SystemLanguage.Swedish => "sv",
            SystemLanguage.Finnish => "fi",
            SystemLanguage.Danish => "da",
            SystemLanguage.Norwegian => "no",
            SystemLanguage.Czech => "cs",
            SystemLanguage.Hungarian => "hu",
            SystemLanguage.Romanian => "ro",
            SystemLanguage.Greek => "el",
            SystemLanguage.Hebrew => "he",
            SystemLanguage.Indonesian => "id",
            SystemLanguage.Thai => "th",
            SystemLanguage.Vietnamese => "vi",
            _ => _data.defaultLanguage
        };
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            BindHierarchy(root);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _data = null;
        _language = null;
        ByKey.Clear();
        BySource.Clear();
        LanguageChanged = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
}
