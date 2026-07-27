using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(FootballSoundCatalog))]
public sealed class FootballSoundCatalogEditor : Editor
{
    private static readonly SoundPreset[] RequiredSounds =
    {
        new SoundPreset(FootballSoundIds.Kick, "Kick", "Regular ball kick."),
        new SoundPreset(FootballSoundIds.StrongKick, "Strong Kick", "Hard kick and bicycle kick."),
        new SoundPreset(FootballSoundIds.Crossbar, "Crossbar", "Ball hit on posts or crossbar."),
        new SoundPreset(FootballSoundIds.Goal, "Goal", "Confirmed goal."),
        new SoundPreset(FootballSoundIds.NetTouch, "Net Touch", "Ball contact with the net.")
    };

    private SerializedProperty _entriesProperty;
    private ReorderableList _entriesList;
    private GUIStyle _headerStyle;
    private GUIStyle _subtleLabelStyle;
    private GUIStyle _pillStyle;

    private void OnEnable()
    {
        _entriesProperty = serializedObject.FindProperty("_entries");

        _entriesList = new ReorderableList(serializedObject, _entriesProperty, true, true, true, true)
        {
            drawHeaderCallback = DrawListHeader,
            drawElementCallback = DrawEntry,
            elementHeightCallback = GetEntryHeight,
            onAddCallback = AddEntry
        };
    }

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        DrawHeader();
        EditorGUILayout.Space(8f);
        DrawRequiredSoundsPanel();
        EditorGUILayout.Space(10f);

        _entriesList.DoLayoutList();

        EditorGUILayout.Space(8f);
        DrawTools();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 54f);
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.16f, 0.20f));

        Rect titleRect = new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 22f);
        Rect subtitleRect = new Rect(rect.x + 14f, rect.y + 30f, rect.width - 28f, 18f);

        EditorGUI.LabelField(titleRect, "Football Sound Catalog", _headerStyle);
        EditorGUI.LabelField(subtitleRect, "Map gameplay sound ids to one or more AudioClips.", _subtleLabelStyle);
    }

    private void DrawRequiredSoundsPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Gameplay Events", EditorStyles.boldLabel);

            HashSet<string> existingIds = GetExistingIds();

            for (int i = 0; i < RequiredSounds.Length; i++)
            {
                SoundPreset preset = RequiredSounds[i];
                bool exists = existingIds.Contains(preset.Id);

                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect statusRect = GUILayoutUtility.GetRect(74f, 20f, GUILayout.Width(74f));
                    EditorGUI.LabelField(statusRect, exists ? "READY" : "MISSING", _pillStyle);

                    EditorGUILayout.LabelField(preset.Label, GUILayout.Width(92f));
                    EditorGUILayout.SelectableLabel(preset.Id, EditorStyles.miniLabel, GUILayout.Height(18f), GUILayout.Width(110f));
                    EditorGUILayout.LabelField(preset.Description, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add Missing Events", GUILayout.Height(24f), GUILayout.Width(150f)))
                    AddMissingEntries();
            }
        }
    }

    private void DrawListHeader(Rect rect)
    {
        EditorGUI.LabelField(rect, "Sound Entries", EditorStyles.boldLabel);
    }

    private void DrawEntry(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
        SerializedProperty id = entry.FindPropertyRelative("_id");
        SerializedProperty clips = entry.FindPropertyRelative("_clips");
        SerializedProperty volume = entry.FindPropertyRelative("_volume");

        rect.y += 4f;
        rect.height -= 8f;

        Rect background = rect;
        background.x += 1f;
        background.width -= 2f;
        EditorGUI.DrawRect(background, isActive ? new Color(0.20f, 0.28f, 0.36f, 0.35f) : new Color(0.12f, 0.12f, 0.12f, 0.16f));

        float lineY = rect.y + 6f;
        Rect idLabelRect = new Rect(rect.x + 8f, lineY, 22f, EditorGUIUtility.singleLineHeight);
        Rect idRect = new Rect(rect.x + 34f, lineY, Mathf.Max(120f, rect.width - 212f), EditorGUIUtility.singleLineHeight);
        Rect volumeLabelRect = new Rect(rect.xMax - 206f, lineY, 42f, EditorGUIUtility.singleLineHeight);
        Rect volumeRect = new Rect(rect.xMax - 164f, lineY, 156f, EditorGUIUtility.singleLineHeight);

        EditorGUI.LabelField(idLabelRect, "Id", EditorStyles.miniBoldLabel);
        id.stringValue = EditorGUI.TextField(idRect, id.stringValue);
        EditorGUI.LabelField(volumeLabelRect, "Vol", EditorStyles.miniBoldLabel);
        EditorGUI.PropertyField(volumeRect, volume, GUIContent.none);

        Rect clipsRect = new Rect(rect.x + 8f, lineY + 24f, rect.width - 16f, EditorGUI.GetPropertyHeight(clips, true));
        EditorGUI.PropertyField(clipsRect, clips, new GUIContent("Clips"), true);

        if (clips.arraySize == 0)
        {
            Rect warningRect = new Rect(rect.x + 8f, clipsRect.yMax + 3f, rect.width - 16f, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(warningRect, "No clips assigned.", EditorStyles.miniLabel);
        }
    }

    private float GetEntryHeight(int index)
    {
        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
        SerializedProperty clips = entry.FindPropertyRelative("_clips");
        float warningHeight = clips.arraySize == 0 ? EditorGUIUtility.singleLineHeight + 4f : 0f;
        return 38f + EditorGUI.GetPropertyHeight(clips, true) + warningHeight;
    }

    private void AddEntry(ReorderableList list)
    {
        int index = _entriesProperty.arraySize;
        _entriesProperty.InsertArrayElementAtIndex(index);

        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("_id").stringValue = GetFirstMissingId();
        entry.FindPropertyRelative("_volume").floatValue = 1f;
        entry.FindPropertyRelative("_clips").arraySize = 0;
    }

    private void DrawTools()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sort By Id", GUILayout.Height(24f)))
                SortEntriesById();

            if (GUILayout.Button("Remove Empty Duplicates", GUILayout.Height(24f)))
                RemoveEmptyDuplicates();
        }
    }

    private void AddMissingEntries()
    {
        HashSet<string> existingIds = GetExistingIds();

        for (int i = 0; i < RequiredSounds.Length; i++)
        {
            if (existingIds.Contains(RequiredSounds[i].Id))
                continue;

            int index = _entriesProperty.arraySize;
            _entriesProperty.InsertArrayElementAtIndex(index);

            SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("_id").stringValue = RequiredSounds[i].Id;
            entry.FindPropertyRelative("_volume").floatValue = 1f;
            entry.FindPropertyRelative("_clips").arraySize = 0;
        }
    }

    private void SortEntriesById()
    {
        List<EntrySnapshot> entries = new List<EntrySnapshot>();

        for (int i = 0; i < _entriesProperty.arraySize; i++)
            entries.Add(EntrySnapshot.FromProperty(_entriesProperty.GetArrayElementAtIndex(i)));

        entries.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));

        for (int i = 0; i < entries.Count; i++)
            entries[i].ApplyTo(_entriesProperty.GetArrayElementAtIndex(i));
    }

    private void RemoveEmptyDuplicates()
    {
        HashSet<string> ids = new HashSet<string>();

        for (int i = _entriesProperty.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("_id").stringValue;
            SerializedProperty clips = entry.FindPropertyRelative("_clips");

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (ids.Add(id))
                continue;

            if (clips.arraySize == 0)
                _entriesProperty.DeleteArrayElementAtIndex(i);
        }
    }

    private HashSet<string> GetExistingIds()
    {
        HashSet<string> ids = new HashSet<string>();

        for (int i = 0; i < _entriesProperty.arraySize; i++)
        {
            string id = _entriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue;

            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }

        return ids;
    }

    private string GetFirstMissingId()
    {
        HashSet<string> existingIds = GetExistingIds();

        for (int i = 0; i < RequiredSounds.Length; i++)
        {
            if (!existingIds.Contains(RequiredSounds[i].Id))
                return RequiredSounds[i].Id;
        }

        return "new_sound";
    }

    private void EnsureStyles()
    {
        _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 17,
            normal = { textColor = Color.white }
        };

        _subtleLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.76f, 0.82f, 0.88f) }
        };

        _pillStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
    }

    private readonly struct SoundPreset
    {
        public SoundPreset(string id, string label, string description)
        {
            Id = id;
            Label = label;
            Description = description;
        }

        public string Id { get; }
        public string Label { get; }
        public string Description { get; }
    }

    private sealed class EntrySnapshot
    {
        public string Id;
        public float Volume;
        public UnityEngine.Object[] Clips;

        public static EntrySnapshot FromProperty(SerializedProperty property)
        {
            SerializedProperty clipsProperty = property.FindPropertyRelative("_clips");
            UnityEngine.Object[] clips = new UnityEngine.Object[clipsProperty.arraySize];

            for (int i = 0; i < clips.Length; i++)
                clips[i] = clipsProperty.GetArrayElementAtIndex(i).objectReferenceValue;

            return new EntrySnapshot
            {
                Id = property.FindPropertyRelative("_id").stringValue,
                Volume = property.FindPropertyRelative("_volume").floatValue,
                Clips = clips
            };
        }

        public void ApplyTo(SerializedProperty property)
        {
            property.FindPropertyRelative("_id").stringValue = Id;
            property.FindPropertyRelative("_volume").floatValue = Volume;

            SerializedProperty clipsProperty = property.FindPropertyRelative("_clips");
            clipsProperty.arraySize = Clips.Length;

            for (int i = 0; i < Clips.Length; i++)
                clipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = Clips[i];
        }
    }
}
