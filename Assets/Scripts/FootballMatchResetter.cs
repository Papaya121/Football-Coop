using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballMatchResetter : MonoBehaviour
{
    private const int PlayerCapacity = 2;

    [SerializeField] private FootballPlayerController[] _players = new FootballPlayerController[PlayerCapacity];
    [SerializeField] private FootballBall _ball;
    [SerializeField] private bool _clearBallTrails = true;

    private TransformSnapshot[] _playerStartSnapshots;
    private TransformSnapshot _ballStartSnapshot;
    private bool _hasBallStartSnapshot;

    private void Awake()
    {
        CaptureStartPositions();
    }

    private void OnValidate()
    {
        if (_players == null || _players.Length != PlayerCapacity)
            Array.Resize(ref _players, PlayerCapacity);
    }

    public void ResetToStartPositions()
    {
        ResetPlayers();
        ResetBall();
    }

    private void CaptureStartPositions()
    {
        _playerStartSnapshots = new TransformSnapshot[_players.Length];

        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i] != null)
                _playerStartSnapshots[i] = TransformSnapshot.From(_players[i].transform);
        }

        if (_ball == null)
            return;

        _ballStartSnapshot = TransformSnapshot.From(_ball.transform);
        _hasBallStartSnapshot = true;
    }

    private void ResetPlayers()
    {
        if (_players == null || _playerStartSnapshots == null)
            return;

        int count = Mathf.Min(_players.Length, _playerStartSnapshots.Length);

        for (int i = 0; i < count; i++)
        {
            if (_players[i] == null || !_playerStartSnapshots[i].HasValue)
                continue;

            _players[i].Respawn(_playerStartSnapshots[i].Position, _playerStartSnapshots[i].Rotation);
        }
    }

    private void ResetBall()
    {
        if (_ball == null || !_hasBallStartSnapshot)
            return;

        _ball.Respawn(_ballStartSnapshot.Position, _ballStartSnapshot.Rotation);

        if (!_clearBallTrails)
            return;

        foreach (TrailRenderer trail in _ball.GetComponentsInChildren<TrailRenderer>())
            trail.Clear();
    }

    private readonly struct TransformSnapshot
    {
        public TransformSnapshot(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
            HasValue = true;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool HasValue { get; }

        public static TransformSnapshot From(Transform transform)
        {
            return new TransformSnapshot(transform.position, transform.rotation);
        }
    }
}
