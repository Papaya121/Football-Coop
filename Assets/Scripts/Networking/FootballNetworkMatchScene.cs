using System;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class FootballNetworkMatchScene : MonoBehaviour
{
    private const int PlayerCount = 2;

    [SerializeField] private FootballPlayerController[] _localPlayers = new FootballPlayerController[PlayerCount];
    [SerializeField] private FootballBall _localBall;
    [SerializeField] private FootballPlayerJoinManager _localJoinManager;
    [SerializeField] private FootballMatchController _localMatchController;
    [SerializeField] private FootballScoreController _localScoreController;
    [SerializeField] private FootballMatchResetter _localMatchResetter;
    [SerializeField] private FootballGoalZone[] _goalZones = Array.Empty<FootballGoalZone>();
    [SerializeField] private FootballMatchHudView[] _matchHudViews = Array.Empty<FootballMatchHudView>();
    [SerializeField] private FootballScoreHudView[] _scoreHudViews = Array.Empty<FootballScoreHudView>();
    [SerializeField] private FootballGameplayCamera[] _gameplayCameras = Array.Empty<FootballGameplayCamera>();
    [SerializeField] private FootballNetworkMatchExitButton[] _exitButtons = Array.Empty<FootballNetworkMatchExitButton>();

    private SpawnPoint[] _spawnPoints;
    private SpawnPoint _ballSpawnPoint;
    private bool _hasBallSpawnPoint;
    private uint _lastEventSequence;
    private PhysicsScene _serverPhysicsScene;
    private bool _simulateServerPhysics;
    private ulong _simulationStepCount;

    public FootballGoalZone[] GoalZones => _goalZones;
    public bool IsServerPhysicsSimulationRunning => _simulateServerPhysics;
    public ulong SimulationStepCount => _simulationStepCount;

    private void Awake()
    {
        CaptureSpawnPoints();
        DisableLegacyPhysicsSimulator();

        if (NetworkServer.active || NetworkClient.active)
            PrepareForNetwork();
    }

    [Server]
    public void StartServerPhysicsSimulation()
    {
        _serverPhysicsScene = gameObject.scene.GetPhysicsScene();
        _simulateServerPhysics = _serverPhysicsScene.IsValid() &&
            _serverPhysicsScene != Physics.defaultPhysicsScene;

        FootballNetworkDiagnostics.Write(
            "PHYSICS",
            $"Activation requested. scene={gameObject.scene.name}; handle={gameObject.scene.handle}; " +
            $"valid={_serverPhysicsScene.IsValid()}; isDefault={_serverPhysicsScene == Physics.defaultPhysicsScene}; " +
            $"enabled={enabled}; activeInHierarchy={gameObject.activeInHierarchy}"
        );

        if (!_simulateServerPhysics)
            Debug.LogError($"Match scene '{gameObject.scene.name}' has no isolated 3D PhysicsScene.", this);
    }

    [ServerCallback]
    private void FixedUpdate()
    {
        if (!_simulateServerPhysics)
            return;

        _serverPhysicsScene.Simulate(Time.fixedDeltaTime);
        _simulationStepCount++;

        if (_simulationStepCount <= 3)
            FootballNetworkDiagnostics.Write(
                "PHYSICS",
                $"Simulated step={_simulationStepCount}; fixedDeltaTime={Time.fixedDeltaTime:F4}"
            );
    }

    public SpawnPoint GetPlayerSpawnPoint(int index)
    {
        if (_spawnPoints == null || index < 0 || index >= _spawnPoints.Length || !_spawnPoints[index].IsValid)
            return new SpawnPoint(Vector3.zero, Quaternion.identity);

        return _spawnPoints[index];
    }

    public SpawnPoint GetBallSpawnPoint()
    {
        return _hasBallSpawnPoint
            ? _ballSpawnPoint
            : new SpawnPoint(new Vector3(0f, 4f, 0f), Quaternion.identity);
    }

    public void PrepareForNetwork()
    {
        SetEnabled(_localJoinManager, false);
        SetEnabled(_localMatchController, false);
        SetEnabled(_localScoreController, false);
        SetEnabled(_localMatchResetter, false);

        if (_localPlayers != null)
        {
            foreach (FootballPlayerController player in _localPlayers)
            {
                if (player != null)
                    player.gameObject.SetActive(false);
            }
        }

        if (_localBall != null)
            _localBall.gameObject.SetActive(false);

        if (NetworkServer.active && !NetworkClient.active)
            SetServerPresentationEnabled(false);
    }

    public void BindNetworkBall(Transform ball)
    {
        if (_gameplayCameras == null)
            return;

        foreach (FootballGameplayCamera gameplayCamera in _gameplayCameras)
        {
            if (gameplayCamera != null)
                gameplayCamera.SetTarget(ball);
        }
    }

    [Client]
    public void ApplySnapshot(FootballMatchStateMessage snapshot, FootballTeamSide localSide)
    {
        if (_exitButtons != null)
        {
            foreach (FootballNetworkMatchExitButton exitButton in _exitButtons)
            {
                if (exitButton != null)
                    exitButton.ApplyMatchState(snapshot.State);
            }
        }

        if (_scoreHudViews != null)
        {
            foreach (FootballScoreHudView scoreHud in _scoreHudViews)
            {
                if (scoreHud != null)
                    scoreHud.ShowScore(snapshot.LeftScore, snapshot.RightScore);
            }
        }

        if (_matchHudViews != null)
        {
            foreach (FootballMatchHudView matchHud in _matchHudViews)
            {
                if (matchHud == null)
                    continue;

                switch (snapshot.State)
                {
                    case FootballMatchState.WaitingForPlayers:
                        matchHud.ShowWaiting(2, snapshot.MatchDurationSeconds);
                        break;
                    case FootballMatchState.Countdown:
                        matchHud.ShowCountdown(snapshot.CountdownValue, snapshot.MatchDurationSeconds);
                        break;
                    case FootballMatchState.Running:
                        matchHud.ShowRunning(snapshot.RemainingSeconds);
                        break;
                    case FootballMatchState.GoalPause:
                        matchHud.ShowGoal(snapshot.LastScoringSide, snapshot.RemainingSeconds);
                        break;
                    case FootballMatchState.Finished:
                        matchHud.ShowNetworkFinished(snapshot.RemainingSeconds, snapshot.Result, localSide);
                        break;
                }
            }
        }

        if (snapshot.EventSequence == 0 || snapshot.EventSequence == _lastEventSequence)
            return;

        _lastEventSequence = snapshot.EventSequence;

        if (snapshot.Event == FootballMatchEvent.Whistle)
            FootballSoundPlayer.TryPlay(FootballSoundIds.Whistle, Vector3.zero);
        else if (snapshot.Event == FootballMatchEvent.Goal)
            FootballSoundPlayer.TryPlay(FootballSoundIds.Goal, Vector3.zero);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        FootballPlayerController[] localPlayers,
        FootballBall localBall,
        FootballPlayerJoinManager joinManager,
        FootballMatchController matchController,
        FootballScoreController scoreController,
        FootballMatchResetter matchResetter,
        FootballGoalZone[] goalZones,
        FootballMatchHudView[] matchHudViews,
        FootballScoreHudView[] scoreHudViews,
        FootballGameplayCamera[] cameras,
        FootballNetworkMatchExitButton[] exitButtons)
    {
        _localPlayers = localPlayers;
        _localBall = localBall;
        _localJoinManager = joinManager;
        _localMatchController = matchController;
        _localScoreController = scoreController;
        _localMatchResetter = matchResetter;
        _goalZones = goalZones;
        _matchHudViews = matchHudViews;
        _scoreHudViews = scoreHudViews;
        _gameplayCameras = cameras;
        _exitButtons = exitButtons;
    }
#endif

    private void CaptureSpawnPoints()
    {
        _spawnPoints = new SpawnPoint[_localPlayers != null ? _localPlayers.Length : 0];

        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            FootballPlayerController player = _localPlayers[i];

            if (player != null)
                _spawnPoints[i] = new SpawnPoint(player.transform.position, player.transform.rotation);
        }

        if (_localBall == null)
            return;

        _ballSpawnPoint = new SpawnPoint(_localBall.transform.position, _localBall.transform.rotation);
        _hasBallSpawnPoint = true;
    }

    private void SetServerPresentationEnabled(bool enabled)
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (Camera sceneCamera in root.GetComponentsInChildren<Camera>(true))
                sceneCamera.enabled = enabled;

            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = enabled;

            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.enabled = enabled;
        }
    }

    private void DisableLegacyPhysicsSimulator()
    {
        PhysicsSimulator legacySimulator = GetComponent<PhysicsSimulator>();

        if (legacySimulator != null)
            legacySimulator.enabled = false;
    }

    private static void SetEnabled(Behaviour behaviour, bool enabled)
    {
        if (behaviour != null)
            behaviour.enabled = enabled;
    }

    private static FootballMatchResult GetResult(int leftScore, int rightScore)
    {
        if (leftScore > rightScore)
            return FootballMatchResult.LeftWon;

        if (rightScore > leftScore)
            return FootballMatchResult.RightWon;

        return FootballMatchResult.Draw;
    }

    public readonly struct SpawnPoint
    {
        public SpawnPoint(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
            IsValid = true;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool IsValid { get; }
    }
}
