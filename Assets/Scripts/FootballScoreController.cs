using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballScoreController : MonoBehaviour
{
    private const int GoalZoneCount = 2;

    [SerializeField] private FootballMatchController _matchController;
    [SerializeField] private FootballPlayerJoinManager _joinManager;
    [SerializeField] private FootballGoalZone[] _goalZones = new FootballGoalZone[GoalZoneCount];
    [SerializeField] private FootballScoreHudView _hudView;
    [SerializeField, Min(0f)] private float _scoreLockSeconds = 0.5f;

    private readonly FootballScore _score = new FootballScore();

    private float _scoreLockedUntil;

    public event Action<FootballTeamSide, int, int> GoalScored;

    public int LeftScore => _score.Left;
    public int RightScore => _score.Right;

    private void Awake()
    {
        if (_joinManager == null)
            _joinManager = FindAnyObjectByType<FootballPlayerJoinManager>();
    }

    private void OnEnable()
    {
        if (_goalZones == null)
            return;

        for (int i = 0; i < _goalZones.Length; i++)
        {
            if (_goalZones[i] != null)
                _goalZones[i].BallEntered += OnBallEnteredGoalZone;
        }
    }

    private void Start()
    {
        RefreshHud();
    }

    private void OnDisable()
    {
        if (_goalZones == null)
            return;

        for (int i = 0; i < _goalZones.Length; i++)
        {
            if (_goalZones[i] != null)
                _goalZones[i].BallEntered -= OnBallEnteredGoalZone;
        }
    }

    private void OnValidate()
    {
        if (_goalZones == null || _goalZones.Length != GoalZoneCount)
            Array.Resize(ref _goalZones, GoalZoneCount);
    }

    private void OnBallEnteredGoalZone(FootballGoalZone goalZone, FootballBall ball)
    {
        if (!CanRegisterGoal(goalZone, ball))
            return;

        FootballTeamSide scoringSide = goalZone.DefendingSide.Opposite();

        _score.AddGoal(scoringSide);
        _scoreLockedUntil = Time.time + _scoreLockSeconds;

        RefreshHud();
        FootballSoundPlayer.TryPlay(FootballSoundIds.Goal, ball.transform.position);
        GoalScored?.Invoke(scoringSide, _score.Left, _score.Right);

        if (!LocalPlayerSetupSession.IsTutorial)
        {
            bool ownGoal = TryGetLastTouchSide(ball, out FootballTeamSide touchSide) &&
                touchSide == goalZone.DefendingSide;
            FootballAnalytics.Goal(
                scoringSide == FootballTeamSide.Left,
                ball.LastTouchKickType,
                ownGoal
            );
        }
    }

    private bool CanRegisterGoal(FootballGoalZone goalZone, FootballBall ball)
    {
        if (goalZone == null || ball == null)
            return false;

        if (Time.time < _scoreLockedUntil)
            return false;

        return _matchController == null || _matchController.State == FootballMatchState.Running;
    }

    private void RefreshHud()
    {
        if (_hudView != null)
            _hudView.ShowScore(_score.Left, _score.Right);
    }

    private bool TryGetLastTouchSide(FootballBall ball, out FootballTeamSide side)
    {
        if (_joinManager != null && ball != null && ball.LastTouchPlayer != null)
        {
            if (_joinManager.GetPlayer(0) == ball.LastTouchPlayer)
            {
                side = FootballTeamSide.Left;
                return true;
            }

            if (_joinManager.GetPlayer(1) == ball.LastTouchPlayer)
            {
                side = FootballTeamSide.Right;
                return true;
            }
        }

        side = default;
        return false;
    }
}
