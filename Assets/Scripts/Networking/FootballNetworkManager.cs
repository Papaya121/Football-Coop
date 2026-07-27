using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class FootballNetworkManager : NetworkManager
{
    private const string DefaultServerAddress = "localhost";

#if UNITY_EDITOR
    public const string EditorServerModePreference = "FootballCoop.Networking.EditorServerMode";
#endif

    [Header("Football Matchmaking")]
    [Scene, SerializeField] private string _gameplayScene = "Assets/Scenes/Gameplay.unity";
    [SerializeField] private GameObject _networkBallPrefab;
    [SerializeField, Min(1)] private int _maxConcurrentMatches = 32;
    [SerializeField, Min(1f)] private float _matchDurationSeconds = 180f;
    [SerializeField, Min(1)] private int _countdownSeconds = 3;
    [SerializeField, Min(0f)] private float _resultPresentationSeconds = 10f;

    private readonly FootballMatchmakingQueue _queue = new FootballMatchmakingQueue();
    private readonly Dictionary<uint, FootballServerMatch> _matches = new Dictionary<uint, FootballServerMatch>();
    private readonly Dictionary<NetworkConnectionToClient, FootballServerMatch> _connectionMatches =
        new Dictionary<NetworkConnectionToClient, FootballServerMatch>();
    private readonly HashSet<NetworkConnectionToClient> _formingConnections = new HashSet<NetworkConnectionToClient>();

    private uint _nextMatchId = 1;
    private bool _searchRequested;
    private uint _clientMatchId;
    private FootballTeamSide _clientSide;
    private FootballMatchState _clientMatchState = FootballMatchState.WaitingForPlayers;
    private FootballNetworkMatchScene _clientMatchScene;
    private SceneOperation _lastClientSceneOperation;
    private bool _serverStopping;
    private bool _clientSceneReadySent;
    private bool _returningToMenu;

    public static FootballNetworkManager Instance => singleton as FootballNetworkManager;

    public event Action<string> MatchmakingStatusChanged;
    public event Action MatchLoading;
    public event Action ReturnedToMenu;

    public override void Awake()
    {
        autoCreatePlayer = false;
        base.Awake();
        ApplyCommandLineConfiguration();
        FootballNetworkDiagnostics.Write(
            "MANAGER",
            $"Awake. address={networkAddress}; port={GetConfiguredPort()}; log={FootballNetworkDiagnostics.LogPath}"
        );
    }

    public override void Start()
    {
        base.Start();

#if UNITY_EDITOR
        if (!isNetworkActive && UnityEditor.EditorPrefs.GetBool(EditorServerModePreference, false))
        {
            Debug.Log("Football Editor Server: starting silent Server Only mode on port " + GetConfiguredPort() + ".", this);
            StartServer();
        }
#endif
    }

    public override void Update()
    {
        base.Update();

        if (!NetworkServer.active)
            return;

        foreach (FootballServerMatch match in _matches.Values.ToArray())
        {
            match.Tick(Time.deltaTime);

            if (match.ShouldClose)
                CompleteMatch(match);
        }
    }

    public void FindMatch()
    {
        FootballNetworkDiagnostics.Write(
            "CLIENT",
            $"FindMatch requested. clientActive={NetworkClient.active}; connected={NetworkClient.isConnected}; serverActive={NetworkServer.active}"
        );

        if (NetworkServer.active && !NetworkClient.active)
        {
            SetClientStatus("Сервер запущен. Подключите отдельный клиент.");
            return;
        }

        _searchRequested = true;

        if (NetworkClient.isConnected)
        {
            NetworkClient.Send(new FootballFindMatchMessage());
            SetClientStatus("Поиск матча…");
            return;
        }

        if (!NetworkClient.active)
        {
            string fallbackAddress = string.IsNullOrWhiteSpace(networkAddress)
                ? DefaultServerAddress
                : networkAddress;
            networkAddress = GetCommandLineValue("-networkAddress", fallbackAddress);
            SetClientStatus($"Подключение к {networkAddress}…");
            StartClient();
        }
    }

    public void CancelMatchmaking()
    {
        _searchRequested = false;

        if (NetworkClient.isConnected)
            NetworkClient.Send(new FootballCancelSearchMessage());

        if (NetworkClient.active)
            StopClient();

        SetClientStatus("Поиск отменён");
    }

    public void BindClientBall(Transform ball)
    {
        _clientMatchScene?.BindNetworkBall(ball);
    }

    public void RequestMatchExit(bool matchFinished)
    {
        if (!NetworkClient.isConnected || _clientMatchId == 0 || _returningToMenu)
            return;

        if (matchFinished || _clientMatchState == FootballMatchState.Finished)
            NetworkClient.Send(new FootballLeaveMatchMessage { MatchId = _clientMatchId });
        else
            NetworkClient.Send(new FootballGiveUpMessage { MatchId = _clientMatchId });
    }

    public override void OnStartServer()
    {
        _serverStopping = false;
        FootballNetworkDiagnostics.Write("SERVER", $"Started. port={GetConfiguredPort()}");
        NetworkServer.RegisterHandler<FootballFindMatchMessage>(OnServerFindMatch);
        NetworkServer.RegisterHandler<FootballCancelSearchMessage>(OnServerCancelSearch);
        NetworkServer.RegisterHandler<FootballMatchSceneReadyMessage>(OnServerMatchSceneReady);
        NetworkServer.RegisterHandler<FootballGiveUpMessage>(OnServerGiveUp);
        NetworkServer.RegisterHandler<FootballLeaveMatchMessage>(OnServerLeaveMatch);
    }

    public override void OnStopServer()
    {
        _serverStopping = true;
        FootballNetworkDiagnostics.Write("SERVER", $"Stopping. matches={_matches.Count}; queued={_queue.Count}");

        Scene[] matchScenes = _matches.Values
            .Select(match => match.Scene)
            .Where(scene => scene.IsValid())
            .ToArray();

        foreach (FootballServerMatch match in _matches.Values)
            match.Dispose();

        _matches.Clear();
        _connectionMatches.Clear();
        _formingConnections.Clear();
        _queue.Clear();

        foreach (Scene scene in matchScenes)
            StartCoroutine(UnloadServerMatchScene(scene));
    }

    public override void OnStartClient()
    {
        FootballNetworkDiagnostics.Write("CLIENT", $"Started. address={networkAddress}; port={GetConfiguredPort()}");
        NetworkClient.RegisterHandler<FootballQueueStatusMessage>(OnClientQueueStatus);
        NetworkClient.RegisterHandler<FootballMatchFoundMessage>(OnClientMatchFound);
        NetworkClient.RegisterHandler<FootballMatchStateMessage>(OnClientMatchState);
        NetworkClient.RegisterHandler<FootballReturnToMenuMessage>(OnClientReturnToMenu);
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        FootballNetworkDiagnostics.Write("CLIENT", $"Connected. connectionPresent={NetworkClient.connection != null}");

        if (_searchRequested)
        {
            NetworkClient.Send(new FootballFindMatchMessage());
            SetClientStatus("Поиск матча…");
        }
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        FootballNetworkDiagnostics.Write(
            "CLIENT",
            $"Scene changed. operation={_lastClientSceneOperation}; matchId={_clientMatchId}; sceneCount={SceneManager.sceneCount}"
        );

        if (_lastClientSceneOperation != SceneOperation.LoadAdditive || _clientMatchId == 0)
            return;

        _clientMatchScene = FindClientMatchScene();
        SetMenuPresentationEnabled(false);
        StartCoroutine(NotifyServerWhenClientMatchSceneReady(_clientMatchId));
    }

    public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
    {
        _lastClientSceneOperation = sceneOperation;
        base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
    }

    public override void OnClientDisconnect()
    {
        FootballNetworkDiagnostics.Write("CLIENT", "Disconnected.");
        base.OnClientDisconnect();
        _searchRequested = false;
        _clientMatchId = 0;
        _clientSceneReadySent = false;
        _returningToMenu = false;
        _clientMatchScene = null;
        StartCoroutine(UnloadClientGameplayScenes());
        SetClientStatus("Соединение с сервером закрыто");
        ReturnedToMenu?.Invoke();
    }

    public override void OnClientError(TransportError error, string reason)
    {
        FootballNetworkDiagnostics.Write("CLIENT", $"Transport error={error}; reason={reason}");
        SetClientStatus($"Ошибка сети: {reason}");
    }

    public override void OnServerConnect(NetworkConnectionToClient connection)
    {
        FootballNetworkDiagnostics.Write(
            "SERVER",
            $"Client connected. connectionId={connection?.connectionId}; authenticated={connection?.isAuthenticated}"
        );
        base.OnServerConnect(connection);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient connection)
    {
        FootballNetworkDiagnostics.Write("SERVER", $"Client disconnecting. connectionId={connection?.connectionId}");
        _queue.Remove(connection);
        _formingConnections.Remove(connection);

        if (_connectionMatches.TryGetValue(connection, out FootballServerMatch match))
            HandleParticipantDisconnect(match, connection);

        base.OnServerDisconnect(connection);
        BroadcastQueueStatus();
    }

#if UNITY_EDITOR
    public void EditorConfigure(string gameplayScene, GameObject configuredPlayerPrefab, GameObject ballPrefab)
    {
        _gameplayScene = gameplayScene;
        playerPrefab = configuredPlayerPrefab;
        _networkBallPrefab = ballPrefab;
        spawnPrefabs.Clear();
        spawnPrefabs.Add(ballPrefab);
        autoCreatePlayer = false;
    }
#endif

    private void OnServerFindMatch(NetworkConnectionToClient connection, FootballFindMatchMessage message)
    {
        FootballNetworkDiagnostics.Write(
            "SERVER",
            $"FindMatch received. connectionId={connection?.connectionId}; hasIdentity={connection?.identity != null}"
        );

        if (connection == null || connection.identity != null ||
            _connectionMatches.ContainsKey(connection) || _formingConnections.Contains(connection))
            return;

        if (_queue.Enqueue(connection))
            BroadcastQueueStatus();

        TryCreateMatches();
    }

    private void OnServerCancelSearch(NetworkConnectionToClient connection, FootballCancelSearchMessage message)
    {
        if (_queue.Remove(connection))
            BroadcastQueueStatus();
    }

    private void OnServerMatchSceneReady(NetworkConnectionToClient connection, FootballMatchSceneReadyMessage message)
    {
        FootballNetworkDiagnostics.Write(
            "SERVER",
            $"SceneReady received. connectionId={connection?.connectionId}; matchId={message.MatchId}"
        );

        if (!_matches.TryGetValue(message.MatchId, out FootballServerMatch match) || !match.Contains(connection))
            return;

        if (match.MarkClientSceneReady(connection) && !match.IsStarted)
            match.Start(playerPrefab, _networkBallPrefab);
    }

    private void OnServerGiveUp(NetworkConnectionToClient connection, FootballGiveUpMessage message)
    {
        if (!TryGetClientMatch(connection, message.MatchId, out FootballServerMatch match))
            return;

        // A fast client can finish loading while its opponent is still loading.
        // Leaving in that short window cancels the unstarted match for both clients.
        if (!match.IsStarted)
        {
            foreach (NetworkConnectionToClient participant in match.ActiveConnections.ToArray())
                ReturnParticipantToMenu(match, participant);

            CloseMatch(match);
            return;
        }

        // Accept a stale "give up" click that raced with the final snapshot as
        // a normal exit instead of leaving the client's button disabled.
        if (match.State == FootballMatchState.Finished)
        {
            ReturnParticipantToMenu(match, connection);

            if (!match.HasActiveParticipants)
                CloseMatch(match);
            return;
        }

        if (!match.TryGiveUp(connection))
            return;

        ReturnParticipantToMenu(match, connection);

        if (!match.HasActiveParticipants)
            CloseMatch(match);
    }

    private void OnServerLeaveMatch(NetworkConnectionToClient connection, FootballLeaveMatchMessage message)
    {
        if (!TryGetClientMatch(connection, message.MatchId, out FootballServerMatch match))
            return;

        if (match.State != FootballMatchState.Finished)
        {
            OnServerGiveUp(connection, new FootballGiveUpMessage { MatchId = message.MatchId });
            return;
        }

        ReturnParticipantToMenu(match, connection);

        if (!match.HasActiveParticipants)
            CloseMatch(match);
    }

    private void TryCreateMatches()
    {
        if (_serverStopping || !NetworkServer.active)
            return;

        int formingMatchCount = _formingConnections.Count / 2;

        while (_matches.Count + formingMatchCount < _maxConcurrentMatches &&
               _queue.TryDequeuePair(out NetworkConnectionToClient first, out NetworkConnectionToClient second))
        {
            _formingConnections.Add(first);
            _formingConnections.Add(second);
            StartCoroutine(CreateMatch(first, second));
            formingMatchCount++;
        }

        BroadcastQueueStatus();
    }

    private IEnumerator CreateMatch(NetworkConnectionToClient first, NetworkConnectionToClient second)
    {
        FootballNetworkDiagnostics.Write(
            "SERVER",
            $"Creating match scene for connections {first?.connectionId} and {second?.connectionId}."
        );

        HashSet<ulong> existingSceneHandles = new HashSet<ulong>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
            existingSceneHandles.Add(SceneManager.GetSceneAt(i).handle.GetRawData());

        AsyncOperation operation = SceneManager.LoadSceneAsync(
            _gameplayScene,
            new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics3D)
        );

        if (operation == null)
        {
            HandleMatchCreationFailure(first, second, "Не удалось загрузить сцену матча.");
            yield break;
        }

        yield return operation;

        Scene matchScene = FindNewScene(existingSceneHandles);
        FootballNetworkMatchScene sceneContext = FindMatchSceneContext(matchScene);

        PhysicsScene physicsScene = matchScene.IsValid() ? matchScene.GetPhysicsScene() : default;
        FootballNetworkDiagnostics.Write(
            "SERVER",
            $"Match scene loaded. valid={matchScene.IsValid()}; loaded={matchScene.isLoaded}; " +
            $"handle={matchScene.handle.GetRawData()}; physicsValid={physicsScene.IsValid()}; " +
            $"isDefaultPhysics={physicsScene == Physics.defaultPhysicsScene}; context={sceneContext != null}"
        );

        _formingConnections.Remove(first);
        _formingConnections.Remove(second);

        if (!IsConnectionActive(first) || !IsConnectionActive(second) || sceneContext == null)
        {
            if (IsConnectionActive(first))
                _queue.Enqueue(first);
            if (IsConnectionActive(second))
                _queue.Enqueue(second);

            if (matchScene.IsValid())
                yield return SceneManager.UnloadSceneAsync(matchScene);

            TryCreateMatches();
            yield break;
        }

        uint matchId = _nextMatchId++;
        FootballNetworkDiagnostics.Write(
            "SERVER",
            $"Match {matchId} allocated. leftConnection={first.connectionId}; rightConnection={second.connectionId}"
        );
        FootballServerMatch match = new FootballServerMatch(
            matchId,
            matchScene,
            sceneContext,
            first,
            second,
            _matchDurationSeconds,
            _countdownSeconds,
            _resultPresentationSeconds
        );

        _matches.Add(matchId, match);
        _connectionMatches.Add(first, match);
        _connectionMatches.Add(second, match);

        first.Send(new FootballMatchFoundMessage { MatchId = matchId, Side = FootballTeamSide.Left });
        second.Send(new FootballMatchFoundMessage { MatchId = matchId, Side = FootballTeamSide.Right });

        SceneMessage sceneMessage = new SceneMessage
        {
            sceneName = _gameplayScene,
            sceneOperation = SceneOperation.LoadAdditive
        };

        first.Send(sceneMessage);
        second.Send(sceneMessage);
    }

    private void HandleMatchCreationFailure(
        NetworkConnectionToClient first,
        NetworkConnectionToClient second,
        string reason)
    {
        Debug.LogError(reason, this);
        _formingConnections.Remove(first);
        _formingConnections.Remove(second);

        if (IsConnectionActive(first))
            _queue.Enqueue(first);
        if (IsConnectionActive(second))
            _queue.Enqueue(second);

        BroadcastQueueStatus();
    }

    private void CompleteMatch(FootballServerMatch match)
    {
        if (match == null || !_matches.ContainsKey(match.MatchId))
            return;

        foreach (NetworkConnectionToClient connection in match.ActiveConnections.ToArray())
            ReturnParticipantToMenu(match, connection);

        CloseMatch(match);
    }

    private void ReturnParticipantToMenu(FootballServerMatch match, NetworkConnectionToClient connection)
    {
        if (match == null || connection == null || !match.IsParticipantActive(connection))
            return;

        if (IsConnectionActive(connection))
            connection.Send(new FootballReturnToMenuMessage { MatchId = match.MatchId });

        if (connection.identity != null)
            NetworkServer.RemovePlayerForConnection(connection, RemovePlayerOptions.Destroy);

        match.DetachParticipant(connection);
        _connectionMatches.Remove(connection);
    }

    private void HandleParticipantDisconnect(FootballServerMatch match, NetworkConnectionToClient connection)
    {
        _connectionMatches.Remove(connection);

        if (!match.IsStarted)
        {
            match.DetachParticipant(connection);

            foreach (NetworkConnectionToClient remaining in match.ActiveConnections.ToArray())
                ReturnParticipantToMenu(match, remaining);

            CloseMatch(match);
            return;
        }

        if (match.State != FootballMatchState.Finished)
            match.TryGiveUp(connection);

        match.DetachParticipant(connection);

        if (!match.HasActiveParticipants)
            CloseMatch(match);
    }

    private void CloseMatch(FootballServerMatch match)
    {
        if (match == null || !_matches.Remove(match.MatchId))
            return;

        foreach (NetworkConnectionToClient connection in match.Connections)
            _connectionMatches.Remove(connection);

        match.Dispose();

        if (match.Scene.IsValid())
            StartCoroutine(UnloadServerMatchScene(match.Scene));

        TryCreateMatches();
    }

    private IEnumerator UnloadServerMatchScene(Scene scene)
    {
        yield return null;

        if (scene.IsValid() && scene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(scene);
    }

    private void BroadcastQueueStatus()
    {
        FootballQueueStatusMessage message = new FootballQueueStatusMessage
        {
            WaitingPlayerCount = _queue.Count
        };

        foreach (NetworkConnectionToClient connection in _queue.GetConnections())
            connection.Send(message);
    }

    private void OnClientQueueStatus(FootballQueueStatusMessage message)
    {
        SetClientStatus($"В поиске: {message.WaitingPlayerCount} игрок(а)");
    }

    private void OnClientMatchFound(FootballMatchFoundMessage message)
    {
        _clientMatchId = message.MatchId;
        _clientSide = message.Side;
        _clientMatchState = FootballMatchState.WaitingForPlayers;
        _clientSceneReadySent = false;
        _returningToMenu = false;
        FootballNetworkDiagnostics.Write("CLIENT", $"Match found. matchId={message.MatchId}; side={message.Side}");
        SetClientStatus("Матч найден. Загрузка…");
        MatchLoading?.Invoke();
    }

    private void OnClientMatchState(FootballMatchStateMessage message)
    {
        if (message.MatchId == _clientMatchId)
        {
            _clientMatchState = message.State;
            _clientMatchScene?.ApplySnapshot(message, _clientSide);
        }
    }

    private void OnClientReturnToMenu(FootballReturnToMenuMessage message)
    {
        if (message.MatchId != _clientMatchId || _returningToMenu)
            return;

        StartCoroutine(ReturnClientToMenu());
    }

    private void SetClientStatus(string status)
    {
        MatchmakingStatusChanged?.Invoke(status);
    }

    private void ApplyCommandLineConfiguration()
    {
        networkAddress = GetCommandLineValue("-networkAddress", networkAddress);
        string portValue = GetCommandLineValue("-port", null);

        if (!ushort.TryParse(portValue, out ushort configuredPort))
            return;

        if (transport is PortTransport portTransport)
            portTransport.Port = configuredPort;
    }

    private ushort GetConfiguredPort()
    {
        return transport is PortTransport portTransport ? portTransport.Port : (ushort)0;
    }

    private static string GetCommandLineValue(string key, string fallback)
    {
        string[] arguments = Environment.GetCommandLineArgs();

        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase))
                return arguments[i + 1];
        }

        return fallback;
    }

    private static bool IsConnectionActive(NetworkConnectionToClient connection)
    {
        return connection != null && connection.isAuthenticated &&
            NetworkServer.connections.TryGetValue(connection.connectionId, out NetworkConnectionToClient current) &&
            current == connection;
    }

    private bool TryGetClientMatch(
        NetworkConnectionToClient connection,
        uint matchId,
        out FootballServerMatch match)
    {
        if (connection != null &&
            _connectionMatches.TryGetValue(connection, out match) &&
            match.MatchId == matchId &&
            match.IsParticipantActive(connection))
            return true;

        match = null;
        return false;
    }

    private static Scene FindNewScene(HashSet<ulong> existingHandles)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!existingHandles.Contains(scene.handle.GetRawData()))
                return scene;
        }

        return default;
    }

    private static FootballNetworkMatchScene FindMatchSceneContext(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            FootballNetworkMatchScene context = root.GetComponentInChildren<FootballNetworkMatchScene>(true);

            if (context != null)
                return context;
        }

        return null;
    }

    private FootballNetworkMatchScene FindClientMatchScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.path == _gameplayScene || scene.name == System.IO.Path.GetFileNameWithoutExtension(_gameplayScene))
            {
                FootballNetworkMatchScene context = FindMatchSceneContext(scene);

                if (context != null)
                    return context;
            }
        }

        return null;
    }

    private IEnumerator NotifyServerWhenClientMatchSceneReady(uint matchId)
    {
        // OnClientSceneChanged means Unity finished loading the additive scene.
        // One extra frame lets its Awake/Start UI and camera setup complete before
        // the server is allowed to create players and start the shared countdown.
        yield return null;

        if (_clientSceneReadySent || _returningToMenu ||
            matchId == 0 || matchId != _clientMatchId ||
            _clientMatchScene == null || !NetworkClient.isConnected)
            yield break;

        _clientSceneReadySent = true;
        FootballNetworkDiagnostics.Write("CLIENT", $"Fully loaded match {matchId}; sending SceneReady.");
        NetworkClient.Send(new FootballMatchSceneReadyMessage { MatchId = matchId });
    }

    private IEnumerator ReturnClientToMenu()
    {
        _returningToMenu = true;
        _searchRequested = false;
        _clientSceneReadySent = false;
        _clientMatchId = 0;
        _clientMatchState = FootballMatchState.WaitingForPlayers;
        _clientMatchScene = null;

        yield return UnloadClientGameplayScenes();

        _returningToMenu = false;
        SetClientStatus("Матч завершён");
        ReturnedToMenu?.Invoke();
    }

    private IEnumerator UnloadClientGameplayScenes()
    {
        List<Scene> scenesToUnload = new List<Scene>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.path == _gameplayScene || scene.name == System.IO.Path.GetFileNameWithoutExtension(_gameplayScene))
                scenesToUnload.Add(scene);
        }

        foreach (Scene scene in scenesToUnload)
        {
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        SetMenuPresentationEnabled(true);
    }

    private static void SetMenuPresentationEnabled(bool enabled)
    {
        Scene menuScene = SceneManager.GetSceneByName("Menu");

        if (!menuScene.IsValid())
            return;

        foreach (GameObject root in menuScene.GetRootGameObjects())
        {
            foreach (Camera sceneCamera in root.GetComponentsInChildren<Camera>(true))
                sceneCamera.enabled = enabled;

            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = enabled;

            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.enabled = enabled;
        }
    }
}
