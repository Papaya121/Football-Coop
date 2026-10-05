using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class FootballLocalizationWindow : EditorWindow
{
    private const string AssetPath = "Assets/Resources/FootballLocalization.json";
    [SerializeField] private FootballLocalizationData _data;
    private Vector2 _scroll;
    private string _search = "";
    private string _newKey = "";
    private string _newSource = "";
    private string _newLanguage = "";
    private string _error;
    [SerializeField] private bool _dirty;

    [MenuItem("Tools/Football/Localization")]
    private static void Open() => GetWindow<FootballLocalizationWindow>("Localization");

    private void OnEnable()
    {
        minSize = new Vector2(850, 450);
        if (_data == null || !_dirty)
            Load();
        else
            hasUnsavedChanges = true;
    }

    private void Load()
    {
        try
        {
            _data = File.Exists(AssetPath)
                ? JsonUtility.FromJson<FootballLocalizationData>(File.ReadAllText(AssetPath))
                : new FootballLocalizationData();
            FootballLocalizationCsv.Validate(_data);
            _dirty = false;
            hasUnsavedChanges = false;
            saveChangesMessage = "Save localization changes?";
            _error = null;
        }
        catch (Exception exception) { _data = null; _error = exception.Message; }
    }

    public override void SaveChanges()
    {
        if (Save())
            base.SaveChanges();
    }

    public override void DiscardChanges()
    {
        Load();
        base.DiscardChanges();
    }

    private bool Save()
    {
        try
        {
            FootballLocalizationCsv.Validate(_data);
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            File.WriteAllText(AssetPath, JsonUtility.ToJson(_data, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            FootballLocalization.Reload();
            _dirty = false;
            hasUnsavedChanges = false;
            _error = null;
            return true;
        }
        catch (Exception exception) { _error = exception.Message; return false; }
    }

    private void Changed()
    {
        _dirty = true;
        hasUnsavedChanges = true;
    }

    private void OnGUI()
    {
        if (_data == null)
        {
            EditorGUILayout.HelpBox(_error ?? "Unable to load localization table.", MessageType.Error);
            if (GUILayout.Button("Reload")) Load();
            return;
        }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button(_dirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(70))) Save();
            if (GUILayout.Button("Import CSV", EditorStyles.toolbarButton, GUILayout.Width(90))) Import();
            if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton, GUILayout.Width(90))) Export();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Play Mode language", GUILayout.Width(115));
            string[] choices = new[] { "System / saved" }.Concat(_data.languages).ToArray();
            int selected = Array.IndexOf(choices, FootballLocalization.EditorLanguage);
            int next = EditorGUILayout.Popup(Mathf.Max(0, selected), choices, GUILayout.Width(125));
            if (next != Mathf.Max(0, selected)) FootballLocalization.EditorLanguage = next == 0 ? "" : choices[next];
        }
        EditorGUILayout.HelpBox("CSV: key,source,ru,en,...  Source is stable; edit the language columns. Import merges by key. Empty translations use the default language. Save applies changes, including in Play Mode.", MessageType.Info);
        if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
        EditorGUI.BeginChangeCheck();
        int defaultIndex = Mathf.Max(0, _data.languages.IndexOf(_data.defaultLanguage));
        defaultIndex = EditorGUILayout.Popup("Default language", defaultIndex, _data.languages.ToArray());
        if (EditorGUI.EndChangeCheck()) { _data.defaultLanguage = _data.languages[defaultIndex]; Changed(); }
        _search = EditorGUILayout.TextField("Search", _search);
        using (new EditorGUILayout.HorizontalScope())
        {
            _newLanguage = EditorGUILayout.TextField("New language code", _newLanguage);
            if (GUILayout.Button("Add language", GUILayout.Width(110)))
            {
                string code = _newLanguage.Trim().ToLowerInvariant();
                if (System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z]{2,3}(-[a-z0-9]{2,8})*$") && !_data.languages.Contains(code))
                {
                    _data.languages.Add(code);
                    foreach (var entry in _data.entries) entry.values.Add("");
                    _newLanguage = "";
                    Changed();
                }
                else _error = "Use a unique language code, e.g. de, fr, pt-br.";
            }
        }
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Key / source", EditorStyles.boldLabel, GUILayout.Width(260));
            foreach (string language in _data.languages) GUILayout.Label(language, EditorStyles.boldLabel, GUILayout.Width(250));
        }
        for (int i = 0; i < _data.entries.Count; i++)
        {
            var entry = _data.entries[i];
            if (!string.IsNullOrEmpty(_search) && (entry.key + entry.source + string.Join(" ", entry.values))
                .IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(260)))
                {
                    GUILayout.Label(entry.key, EditorStyles.boldLabel);
                    GUILayout.Label(entry.source, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(260));
                }
                for (int language = 0; language < _data.languages.Count; language++)
                {
                    EditorGUI.BeginChangeCheck();
                    string value = EditorGUILayout.TextArea(entry.values[language], GUILayout.Width(250), GUILayout.MinHeight(46));
                    if (EditorGUI.EndChangeCheck()) { entry.values[language] = value; Changed(); }
                }
                if (GUILayout.Button("×", GUILayout.Width(24))) { _data.entries.RemoveAt(i--); Changed(); }
            }
        }
        EditorGUILayout.EndScrollView();
        using (new EditorGUILayout.HorizontalScope())
        {
            _newKey = EditorGUILayout.TextField(_newKey, GUILayout.Width(200));
            _newSource = EditorGUILayout.TextField(_newSource);
            if (GUILayout.Button("Add entry", GUILayout.Width(100)))
            {
                if (!string.IsNullOrWhiteSpace(_newKey) && !_data.entries.Any(entry => entry.key == _newKey.Trim()) &&
                    (string.IsNullOrEmpty(_newSource) || !_data.entries.Any(entry => entry.source == _newSource)))
                {
                    var entry = new FootballLocalizationEntry { key = _newKey.Trim(), source = _newSource };
                    entry.values = _data.languages.Select(language => language == _data.defaultLanguage ? _newSource : "").ToList();
                    _data.entries.Add(entry);
                    _newKey = _newSource = "";
                    Changed();
                }
                else _error = "Key and source must be unique; the key cannot be empty.";
            }
        }
        GUILayout.Label($"{_data.entries.Count} entries · {_data.languages.Count} languages · Active: {FootballLocalization.Language}", EditorStyles.miniLabel);
    }

    private void Import()
    {
        string path = EditorUtility.OpenFilePanel("Import localization CSV", "", "csv");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            _data = FootballLocalizationCsv.Import(File.ReadAllText(path, Encoding.UTF8), _data);
            _error = null;
            Changed();
        }
        catch (Exception exception) { _error = exception.Message; }
    }

    private void Export()
    {
        string path = EditorUtility.SaveFilePanel("Export localization CSV", "", "FootballLocalization.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;
        try { File.WriteAllText(path, FootballLocalizationCsv.Export(_data), new UTF8Encoding(true)); _error = null; }
        catch (Exception exception) { _error = exception.Message; }
    }
}
