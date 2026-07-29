using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FootballServerMatch : IDisposable
{
    private const float SnapshotInterval = 0.1f;
    private const float ScoreLockSeconds = 0.5f;

    private readonly NetworkConnectionToClient[] _connections;
    private readonly FootballTeamSide[] _sides = { FootballTeamSide.Left, FootballTeamSide.Right };
    private readonly bool[] _clientSceneReady = new bool[2];
    private readonly bool[] _participantActive = { true, true };
    private readonly GameObject[] _players = new GameObject[2];
    private readonly FootballMatchClock _clock;
    private readonly float _resultPresentationSeconds;

    private GameObject _ballObject;
    private FootballBall _ball;
    private float _nextSnapshotTime;
    private float _nextDiagnosticsTime;
    private float _scoreLockedUntil;
    private int _leftScore;
    private int _rightScore;
    private FootballTeamSide _lastScoringSide;
    private FootballMatchResult _result = FootballMatchResult.Draw;
    private uint _eventSequence;
    private FootballMatchEvent _pendingEvent;
    private bool _started;
    private bool _disposed;
    private float _finishedAt = float.PositiveInfinity;

    public FootballServerMatch(
        uint matchId,
        Scene scene,
        FootballNetworkMatchScene sceneContext,
        NetworkConnectionToClient first,
        NetworkConnectionToClient second,
        float matchDurationSeconds,
        int countdownSeconds,
        float resultPresentationSeconds)
    {
        MatchId = matchId;
        Scene = scene;
        SceneContext = sceneContext;
        _connections = new[] { first, second };
        _clock = new FootballMatchClock(matchDurationSeconds, countdownSeconds);
        _resultPresentationSeconds = Mathf.Max(0f, resultPresentationSeconds);

        SubscribeToGoals();
    }

    public uint MatchId { get; }
    public Scene Scene { get; }
    public FootballNetworkMatchScene SceneContext { get; }
    public bool IsStarted => _started;
    public FootballMatchState State => _clock.State;
    public bool HasActiveParticipants => _participantActive[0] || _participantActive[1];
    public bool ShouldClose => _clock.State == FootballMatchState.Finished &&
        Time.unscaledTime >= _finishedAt + _resultPresentationSeconds;

    public IEnumerable<NetworkConnectionToClient> Connections => _connections;

    public IEnumerable<NetworkConnectionToClient> ActiveConnections
    {
        get
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                if (_participantActive[i] && _connections[i] != null)
                    yield return _connections[i];
            }
        }
    }

    public bool Contains(NetworkConnectionToClient connection)
    {
        return connection != null && (_connections[0] == connection || _connections[1] == connection);
    }

    public FootballTeamSide GetSide(NetworkConnectionToClient connection)
    {
        return _connections[0] == connection ? _sides[0] : _sides[1];
    }

    public bool IsParticipantActive(NetworkConnectionToClient connection)
    {
        int index = GetParticipantIndex(connection);
        return index >= 0 && _participantActive[index];
    }

    public bool TryGiveUp(NetworkConnectionToClient connection)
    {
        int index = GetParticipantIndex(connection);

        if (index < 0 || !_participantActive[index] || !_started || _clock.State == FootballMatchState.Finished)
            return false;

        FootballTeamSide winnerSide = _sides[index].Opposite();
        FinishMatch(winnerSide == FootballTeamSide.Left
            ? FootballMatchResult.LeftWon
            : FootballMatchResult.RightWon);

        FootballNetworkDiagnostics.Write(
            "MATCH",
            $"Match {MatchId}: connection={connection.connectionId} gave up; winner={winnerSide}."
        );
        return true;
    }

    public void DetachParticipant(NetworkConnectionToClient connection)
    {
        int index = GetParticipantIndex(connection);

        if (index < 0)
            return;

        _participantActive[index] = false;
        _players[index] = null;
    }

    public bool MarkClientSceneReady(NetworkConnectionToClient connection)
    {
        for (int i = 0; i < _connections.Length; i++)
        {
            if (_connections[i] != connection)
                continue;

            _clientSceneReady[i] = true;
            return _clientSceneReady[0] && _clientSceneReady[1];
        }

        return false;
    }

    public void Start(GameObject playerPrefab, GameObject ballPrefab)
    {
        if (_started || _disposed)
            return;

        SceneContext.StartServerPhysicsSimulation();

        for (int i = 0; i < _connections.Length; i++)
        {
            FootballNetworkMatchScene.SpawnPoint spawnPoint = SceneContext.GetPlayerSpawnPoint(i);
            GameObject player = UnityEngine.Object.Instantiate(playerPrefab, spawnPoint.Position, spawnPoint.Rotation);
            player.name = $"Network Player [{MatchId}:{_sides[i]}]";
            player.GetComponent<FootballNetworkPlayer>()?.ServerInitialize(_sides[i]);
            SceneManager.MoveGameObjectToScene(player, Scene);
            NetworkServer.AddPlayerForConnection(_connections[i], player);
            player.GetComponent<FootballNetworkPlayer>()?.ServerSetGameplayEnabled(false);
            _players[i] = player;
        }

        FootballNetworkMatchScene.SpawnPoint ballSpawn = SceneContext.GetBallSpawnPoint();
        _ballObject = UnityEngine.Object.Instantiate(ballPrefab, ballSpawn.Position, ballSpawn.Rotation);
        _ballObject.name = $"Network Ball [{MatchId}]";
        SceneManager.MoveGameObjectToScene(_ballObject, Scene);
        NetworkServer.Spawn(_ballObject);
        _ball = _ballObject.GetComponent<FootballBall>();

        FootballNetworkDiagnostics.Write(
            "MATCH",
            $"Match {MatchId} started. scene={Scene.name}; handle={Scene.handle.GetRawData()}; " +
            $"physicsRunning={SceneContext.IsServerPhysicsSimulationRunning}; players={DescribeEntity(_players[0])} | " +
            $"{DescribeEntity(_players[1])}; ball={DescribeEntity(_ballObject)}"
        );

        _started = true;
        _clock.StartCountdown();
        RaiseEvent(FootballMatchEvent.Whistle);
        SendSnapshot();
    }

    public void Tick(float deltaTime)
    {
        if (!_started || _disposed)
            return;

        FootballMatchState previousState = _clock.State;
        _clock.Tick(deltaTime);

        if (previousState != FootballMatchState.Running && _clock.State == FootballMatchState.Running)
        {
            ResetEntities();
            SetPlayerGameplayEnabled(true);
            RaiseEvent(FootballMatchEvent.Whistle);
        }
        else if (previousState != FootballMatchState.Finished && _clock.State == FootballMatchState.Finished)
        {
            FinishMatch(GetScoreResult());
        }

        if (Time.unscaledTime >= _nextSnapshotTime || _pendingEvent != FootballMatchEvent.None)
            SendSnapshot();

        if (Time.unscaledTime >= _nextDiagnosticsTime)
        {
            _nextDiagnosticsTime = Time.unscaledTime + 1f;
            FootballNetworkDiagnostics.Write(
                "MATCH",
                $"Heartbeat match={MatchId}; state={_clock.State}; physicsRunning={SceneContext.IsServerPhysicsSimulationRunning}; " +
                $"physicsSteps={SceneContext.SimulationStepCount}; left={DescribeEntity(_players[0])}; " +
                $"right={DescribeEntity(_players[1])}; ball={DescribeEntity(_ballObject)}"
            );
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        UnsubscribeFromGoals();

        if (_ballObject != null)
            NetworkServer.Destroy(_ballObject);

        foreach (GameObject player in _players)
        {
            if (player != null)
                NetworkServer.Destroy(player);
        }
    }

    private void OnBallEnteredGoalZone(FootballGoalZone goalZone, FootballBall ball)
    {
        if (!_started || _clock.State != FootballMatchState.Running || goalZone == null || ball != _ball)
            return;

        if (Time.time < _scoreLockedUntil)
            return;

        FootballTeamSide scoringSide = goalZone.DefendingSide.Opposite();
        _lastScoringSide = scoringSide;

        if (scoringSide == FootballTeamSide.Left)
            _leftScore++;
        else
            _rightScore++;

        _scoreLockedUntil = Time.time + ScoreLockSeconds;
        _clock.StartGoalPause(2f);
        SetPlayerGameplayEnabled(false);
        RaiseEvent(FootballMatchEvent.Goal);
        SendSnapshot();
    }

    private void ResetEntities()
    {
        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i] == null || !_players[i].TryGetComponent(out FootballNetworkPlayer player))
                continue;

            FootballNetworkMatchScene.SpawnPoint spawnPoint = SceneContext.GetPlayerSpawnPoint(i);
            player.ServerRespawn(spawnPoint.Position, spawnPoint.Rotation);
        }

        if (_ball != null)
        {
            FootballNetworkMatchScene.SpawnPoint ballSpawn = SceneContext.GetBallSpawnPoint();
            _ball.Respawn(ballSpawn.Position, ballSpawn.Rotation);
        }
    }

    private void SendSnapshot()
    {
        FootballMatchStateMessage message = new FootballMatchStateMessage
        {
            MatchId = MatchId,
            State = _clock.State,
            MatchDurationSeconds = _clock.MatchDurationSeconds,
            RemainingSeconds = _clock.State == FootballMatchState.Finished
                ? GetResultPresentationRemainingSeconds()
                : _clock.MatchRemainingSeconds,
            CountdownValue = _clock.CountdownValue,
            LeftScore = _leftScore,
            RightScore = _rightScore,
            LastScoringSide = _lastScoringSide,
            Result = _result,
            EventSequence = _eventSequence,
            Event = _pendingEvent
        };

        foreach (NetworkConnectionToClient connection in ActiveConnections)
        {
            if (connection != null && connection.isAuthenticated)
                connection.Send(message);
        }

        _pendingEvent = FootballMatchEvent.None;
        _nextSnapshotTime = Time.unscaledTime + SnapshotInterval;
    }

    private void RaiseEvent(FootballMatchEvent matchEvent)
    {
        _eventSequence++;
        _pendingEvent = matchEvent;
    }

    private void FinishMatch(FootballMatchResult result)
    {
        _result = result;
        _clock.Finish();
        SetPlayerGameplayEnabled(false);
        _finishedAt = Time.unscaledTime;
        RaiseEvent(FootballMatchEvent.Whistle);
        SendSnapshot();
    }

    private FootballMatchResult GetScoreResult()
    {
        if (_leftScore > _rightScore)
            return FootballMatchResult.LeftWon;

        if (_rightScore > _leftScore)
            return FootballMatchResult.RightWon;

        return FootballMatchResult.Draw;
    }

    private float GetResultPresentationRemainingSeconds()
    {
        if (_clock.State != FootballMatchState.Finished || float.IsPositiveInfinity(_finishedAt))
            return 0f;

        return Mathf.Max(0f, _finishedAt + _resultPresentationSeconds - Time.unscaledTime);
    }

    private int GetParticipantIndex(NetworkConnectionToClient connection)
    {
        for (int i = 0; i < _connections.Length; i++)
        {
            if (_connections[i] == connection)
                return i;
        }

        return -1;
    }

    private void SetPlayerGameplayEnabled(bool enabled)
    {
        foreach (GameObject player in _players)
        {
            if (player != null)
                player.GetComponent<FootballNetworkPlayer>()?.ServerSetGameplayEnabled(enabled);
        }
    }

    private void SubscribeToGoals()
    {
        if (SceneContext == null || SceneContext.GoalZones == null)
            return;

        foreach (FootballGoalZone goalZone in SceneContext.GoalZones)
        {
            if (goalZone != null)
                goalZone.BallEntered += OnBallEnteredGoalZone;
        }
    }

    private void UnsubscribeFromGoals()
    {
        if (SceneContext == null || SceneContext.GoalZones == null)
            return;

        foreach (FootballGoalZone goalZone in SceneContext.GoalZones)
        {
            if (goalZone != null)
                goalZone.BallEntered -= OnBallEnteredGoalZone;
        }
    }

    private static string DescribeEntity(GameObject entity)
    {
        if (entity == null)
            return "null";

        Rigidbody body = entity.GetComponent<Rigidbody>();
        FootballPlayerController player = entity.GetComponent<FootballPlayerController>();
        string input = player != null ? $", input={player.MoveInput}" : string.Empty;

        return body == null
            ? $"{entity.name}(active={entity.activeInHierarchy}, rb=missing{input})"
            : $"{entity.name}(active={entity.activeInHierarchy}, enabled={body.detectCollisions}, " +
              $"kinematic={body.isKinematic}, sleeping={body.IsSleeping()}, pos={body.position}, " +
              $"velocity={body.linearVelocity}{input})";
    }
}
