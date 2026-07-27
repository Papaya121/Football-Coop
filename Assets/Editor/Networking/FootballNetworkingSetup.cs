using System;
using System.Linq;
using Mirror;
using kcp2k;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FootballNetworkingSetup
{
    private const string MenuScenePath = "Assets/Scenes/Menu.unity";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string LocalPlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string LocalBallPrefabPath = "Assets/Prefabs/Ball.prefab";
    private const string NetworkPlayerPrefabPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
    private const string NetworkBallPrefabPath = "Assets/Prefabs/Networking/NetworkBall.prefab";
    private const string LeftTeamMaterialPath = "Assets/Models/Player/Materials/Color_L.mat";
    private const string RightTeamMaterialPath = "Assets/Models/Player/Materials/Color_R.mat";
    private const string EditorServerModeMenuPath = "Football/Networking/Editor Runs As Silent Server";

    [MenuItem(EditorServerModeMenuPath, priority = 1)]
    public static void ToggleEditorServerMode()
    {
        SetEditorServerMode(!IsEditorServerModeEnabled());
    }

    [MenuItem(EditorServerModeMenuPath, true)]
    public static bool ValidateEditorServerMode()
    {
        Menu.SetChecked(EditorServerModeMenuPath, IsEditorServerModeEnabled());
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Football/Networking/Start Silent Server Now", priority = 2)]
    public static void StartSilentServerNow()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        SetEditorServerMode(true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Football/Networking/Start Silent Server Now", true)]
    public static bool ValidateStartSilentServerNow()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Football/Networking/Open Network Logs Folder", priority = 20)]
    public static void OpenNetworkLogsFolder()
    {
        System.IO.Directory.CreateDirectory(FootballNetworkDiagnostics.DirectoryPath);
        EditorUtility.RevealInFinder(FootballNetworkDiagnostics.DirectoryPath);
    }

    [MenuItem("Football/Networking/Configure Matchmaking")]
    public static void Configure()
    {
        try
        {
            EnsureFolder("Assets/Prefabs/Networking");
            GameObject networkPlayerPrefab = ConfigureNetworkPlayerPrefab();
            GameObject networkBallPrefab = ConfigureNetworkBallPrefab();
            ConfigureGameplayScene();
            ConfigureMenuScene(networkPlayerPrefab, networkBallPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Football matchmaking configuration completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static GameObject ConfigureNetworkPlayerPrefab()
    {
        CopyPrefabIfMissing(LocalPlayerPrefabPath, NetworkPlayerPrefabPath);
        GameObject root = PrefabUtility.LoadPrefabContents(NetworkPlayerPrefabPath);

        try
        {
            GetOrAdd<NetworkIdentity>(root);
            ConfigureNetworkRigidbody(GetOrAdd<NetworkRigidbodyUnreliable>(root), root.transform);
            FootballNetworkPlayer networkPlayer = GetOrAdd<FootballNetworkPlayer>(root);
            networkPlayer.EditorConfigureTeamVisuals(
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                LoadRequiredAsset<Material>(LeftTeamMaterialPath),
                LoadRequiredAsset<Material>(RightTeamMaterialPath)
            );
            PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssignNetworkAssetId(NetworkPlayerPrefabPath);

        return AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPrefabPath);
    }

    private static GameObject ConfigureNetworkBallPrefab()
    {
        CopyPrefabIfMissing(LocalBallPrefabPath, NetworkBallPrefabPath);
        GameObject root = PrefabUtility.LoadPrefabContents(NetworkBallPrefabPath);

        try
        {
            GetOrAdd<NetworkIdentity>(root);
            ConfigureNetworkRigidbody(GetOrAdd<NetworkRigidbodyUnreliable>(root), root.transform);
            GetOrAdd<FootballNetworkBall>(root);
            PrefabUtility.SaveAsPrefabAsset(root, NetworkBallPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssignNetworkAssetId(NetworkBallPrefabPath);

        return AssetDatabase.LoadAssetAtPath<GameObject>(NetworkBallPrefabPath);
    }

    private static void ConfigureGameplayScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        FootballPlayerJoinManager joinManager = FindInScene<FootballPlayerJoinManager>(scene);

        if (joinManager == null)
            throw new InvalidOperationException("FootballPlayerJoinManager was not found in Gameplay scene.");

        GameObject controllerObject = joinManager.gameObject;
        FootballNetworkMatchScene matchScene = GetOrAdd<FootballNetworkMatchScene>(controllerObject);
        PhysicsSimulator legacySimulator = controllerObject.GetComponent<PhysicsSimulator>();

        if (legacySimulator != null)
            UnityEngine.Object.DestroyImmediate(legacySimulator, true);

        FootballPlayerController[] players = FindAllInScene<FootballPlayerController>(scene)
            .OrderBy(player => player.transform.position.x)
            .ToArray();

        Button giveUpButton = FindAllInScene<Button>(scene)
            .FirstOrDefault(button => button.name == "GiveUp Button");
        FootballNetworkMatchExitButton exitButton = null;

        if (giveUpButton != null)
        {
            exitButton = GetOrAdd<FootballNetworkMatchExitButton>(giveUpButton.gameObject);
            exitButton.EditorConfigure(
                giveUpButton,
                giveUpButton.GetComponentInChildren<TMP_Text>(true)
            );
            EditorUtility.SetDirty(exitButton);
        }

        matchScene.EditorConfigure(
            players,
            FindInScene<FootballBall>(scene),
            joinManager,
            FindInScene<FootballMatchController>(scene),
            FindInScene<FootballScoreController>(scene),
            FindInScene<FootballMatchResetter>(scene),
            FindAllInScene<FootballGoalZone>(scene),
            FindAllInScene<FootballMatchHudView>(scene),
            FindAllInScene<FootballScoreHudView>(scene),
            FindAllInScene<FootballGameplayCamera>(scene),
            exitButton != null
                ? new[] { exitButton }
                : Array.Empty<FootballNetworkMatchExitButton>()
        );

        EditorUtility.SetDirty(matchScene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureMenuScene(GameObject playerPrefab, GameObject ballPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject networkObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "[Networking]");

        if (networkObject == null)
            networkObject = new GameObject("[Networking]");

        KcpTransport transport = GetOrAdd<KcpTransport>(networkObject);
        FootballNetworkManager manager = GetOrAdd<FootballNetworkManager>(networkObject);
        GetOrAdd<SceneInterestManagement>(networkObject);

        transport.port = 7777;
        manager.transport = transport;
        manager.networkAddress = "localhost";
        manager.maxConnections = 100;
        manager.sendRate = 60;
        manager.dontDestroyOnLoad = true;
        manager.runInBackground = true;
        manager.headlessStartMode = HeadlessStartOptions.AutoStartServer;
        manager.offlineScene = string.Empty;
        manager.onlineScene = string.Empty;
        manager.EditorConfigure(GameplayScenePath, playerPrefab, ballPrefab);

        EditorUtility.SetDirty(networkObject);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureNetworkRigidbody(NetworkRigidbodyUnreliable networkRigidbody, Transform target)
    {
        networkRigidbody.target = target;
        networkRigidbody.syncDirection = SyncDirection.ServerToClient;
        // NetworkRigidbodyUnreliable has its own FixedUpdate for authority/kinematic state.
        // Applying NetworkTransform snapshots in Update avoids hiding its base FixedUpdate
        // interpolation callback while the authoritative physics still runs in FixedUpdate.
        networkRigidbody.updateMethod = UpdateMethod.Update;
        networkRigidbody.coordinateSpace = CoordinateSpace.World;
        networkRigidbody.syncPosition = true;
        networkRigidbody.syncRotation = true;
        networkRigidbody.syncScale = false;
        networkRigidbody.syncInterval = 0f;
    }

    private static void CopyPrefabIfMissing(string sourcePath, string destinationPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) != null)
            return;

        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            throw new InvalidOperationException($"Could not copy prefab from {sourcePath} to {destinationPath}.");

        AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void AssignNetworkAssetId(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        NetworkIdentity identity = prefab != null ? prefab.GetComponent<NetworkIdentity>() : null;

        if (identity == null)
            throw new InvalidOperationException($"NetworkIdentity is missing on {prefabPath}.");

        string guidText = AssetDatabase.AssetPathToGUID(prefabPath);
        uint assetId = NetworkIdentity.AssetGuidToUint(new Guid(guidText));
        SerializedObject serializedIdentity = new SerializedObject(identity);
        serializedIdentity.FindProperty("_assetId").uintValue = assetId;
        serializedIdentity.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(identity);
        AssetDatabase.SaveAssetIfDirty(identity);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
            throw new InvalidOperationException($"Required asset was not found at {path}.");

        return asset;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        return FindAllInScene<T>(scene).FirstOrDefault();
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];

        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);

            current = next;
        }
    }

    private static bool IsEditorServerModeEnabled()
    {
        return EditorPrefs.GetBool(FootballNetworkManager.EditorServerModePreference, false);
    }

    private static void SetEditorServerMode(bool enabled)
    {
        EditorPrefs.SetBool(FootballNetworkManager.EditorServerModePreference, enabled);
        Menu.SetChecked(EditorServerModeMenuPath, enabled);
        Debug.Log(enabled
            ? "Football Editor Server mode enabled. Play Mode will start Server Only."
            : "Football Editor Server mode disabled. Play Mode will run normally.");
    }
}
