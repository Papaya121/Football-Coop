using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Editor-only setup for the football arena. No runtime probe generation or extra per-frame work.
[InitializeOnLoad]
internal static class FootballLightProbeSetup
{
    private const string ScenePath = "Assets/Scenes/Gameplay.unity";
    private const string GroupName = "Gameplay Light Probes (Mobile WebGL)";
    private const string ReportPath = "Library/FootballLightProbeSetup.txt";

    static FootballLightProbeSetup()
    {
        Lightmapping.bakeCompleted += ReportBake;
    }

    [MenuItem("Tools/Football/Lighting/Set Up and Bake Mobile-WebGL Light Probes")]
    private static void ConfigureAndBake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Exit Play mode and wait for the current lighting bake first.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("Open Gameplay as the only loaded scene before baking its light probes.");

        // The active editor pipeline must use the same probe system as the mobile build.
        QualitySettings.SetQualityLevel(0, true);
        foreach (string path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            var settings = new SerializedObject(asset);
            settings.FindProperty("m_LightProbeSystem").intValue = 0;
            settings.ApplyModifiedProperties();
        }

        LightProbeGroup group = null;
        var groups = new List<LightProbeGroup>();
        BoxCollider ground = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            groups.AddRange(root.GetComponentsInChildren<LightProbeGroup>(true));
            foreach (var collider in root.GetComponentsInChildren<BoxCollider>(true))
                if (collider.name == "Ground" && collider.enabled && collider.gameObject.activeInHierarchy &&
                    !collider.isTrigger && collider.bounds.size.x > 1f &&
                    (ground == null || collider.bounds.size.x > ground.bounds.size.x))
                    ground = collider;
            foreach (var volume in root.GetComponentsInChildren<ProbeVolume>(true))
            {
                Undo.RecordObject(volume, "Use ordinary light probes");
                volume.enabled = false;
            }
        }
        if (ground == null)
            throw new InvalidOperationException("The arena Ground collider was not found.");
        foreach (var existing in groups)
            if (existing.name == GroupName) group = existing;
        // Reuse a newly added default group rather than leaving overlapping default probes.
        if (group == null && groups.Count == 1 && groups[0].probePositions.Length <= 8)
            group = groups[0];
        if (group == null)
        {
            var probeObject = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(probeObject, "Create arena light probes");
            SceneManager.MoveGameObjectToScene(probeObject, scene);
            group = Undo.AddComponent<LightProbeGroup>(probeObject);
        }
        Undo.RecordObject(group, "Place arena light probes");
        Undo.RecordObject(group.gameObject, "Name arena light probes");
        group.name = GroupName;
        group.enabled = true;
        group.dering = true;

        var bounds = ground.bounds;
        float minX = bounds.min.x - 1f;
        float maxX = bounds.max.x + 1f;
        int columns = Mathf.CeilToInt((maxX - minX) / 2f) + 1;
        float[] heights = { 0.2f, 1.2f, 2.4f, 4f, 6f, 9f };
        float[] depths = { -1.25f, 0f, 1.25f };
        var probes = new List<Vector3>();
        for (int x = 0; x < columns; x++)
            foreach (float height in heights)
                foreach (float depth in depths)
                {
                    var worldPosition = new Vector3(Mathf.Lerp(minX, maxX, x / (float)(columns - 1)),
                        bounds.max.y + height, bounds.center.z + depth);
                    probes.Add(group.transform.InverseTransformPoint(worldPosition));
                }
        group.probePositions = probes.ToArray();
        EditorUtility.SetDirty(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Gameplay could not be saved.");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = group.gameObject;
        File.WriteAllText(ReportPath, "Configured " + probes.Count + " ordinary light probes. Starting lighting bake.");
        if (!Lightmapping.BakeAsync())
            File.AppendAllText(ReportPath, "\nBake did not start. Use Window > Rendering > Lighting > Generate Lighting.");
    }

    private static void ReportBake()
    {
        if (!File.Exists(ReportPath)) return;
        int count = LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0;
        File.AppendAllText(ReportPath, "\nBake completed. Loaded light probes: " + count);
        AssetDatabase.SaveAssets();
        Debug.Log("Football lighting bake completed; loaded light probes: " + count);
    }
}
