using UnityEngine;

public sealed class FootballMatchClock
{
    private readonly float _matchDurationSeconds;
    private readonly int _countdownSeconds;

    private float _matchRemainingSeconds;
    private float _countdownRemainingSeconds;
    private float _goalPauseRemainingSeconds;

    public FootballMatchState State { get; private set; }
    public float MatchDurationSeconds => _matchDurationSeconds;
    public float MatchRemainingSeconds => _matchRemainingSeconds;
    public float GoalPauseRemainingSeconds => _goalPauseRemainingSeconds;

    public int CountdownValue
    {
        get
        {
            if (State != FootballMatchState.Countdown)
                return 0;

            return Mathf.Clamp(Mathf.CeilToInt(_countdownRemainingSeconds), 1, _countdownSeconds);
        }
    }

    public FootballMatchClock(float matchDurationSeconds, int countdownSeconds)
    {
        _matchDurationSeconds = Mathf.Max(1f, matchDurationSeconds);
        _countdownSeconds = Mathf.Max(1, countdownSeconds);

        WaitForPlayers();
    }

    public void WaitForPlayers()
    {
        State = FootballMatchState.WaitingForPlayers;
        _matchRemainingSeconds = _matchDurationSeconds;
        _countdownRemainingSeconds = _countdownSeconds;
        _goalPauseRemainingSeconds = 0f;
    }

    public void StartCountdown()
    {
        State = FootballMatchState.Countdown;
        _matchRemainingSeconds = _matchDurationSeconds;
        _countdownRemainingSeconds = _countdownSeconds;
        _goalPauseRemainingSeconds = 0f;
    }

    public void StartGoalPause(float durationSeconds)
    {
        if (State != FootballMatchState.Running)
            return;

        State = FootballMatchState.GoalPause;
        _goalPauseRemainingSeconds = Mathf.Max(0f, durationSeconds);

        if (_goalPauseRemainingSeconds <= 0f)
            State = FootballMatchState.Running;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (State == FootballMatchState.Countdown)
        {
            TickCountdown(deltaTime);
            return;
        }

        if (State == FootballMatchState.GoalPause)
        {
            TickGoalPause(deltaTime);
            return;
        }

        if (State == FootballMatchState.Running)
            TickMatch(deltaTime);
    }

    private void TickCountdown(float deltaTime)
    {
        _countdownRemainingSeconds -= deltaTime;

        if (_countdownRemainingSeconds > 0f)
            return;

        State = FootballMatchState.Running;
        _countdownRemainingSeconds = 0f;
        _matchRemainingSeconds = _matchDurationSeconds;
    }

    private void TickGoalPause(float deltaTime)
    {
        _goalPauseRemainingSeconds = Mathf.Max(0f, _goalPauseRemainingSeconds - deltaTime);

        if (_goalPauseRemainingSeconds > 0f)
            return;

        State = FootballMatchState.Running;
    }

    private void TickMatch(float deltaTime)
    {
        _matchRemainingSeconds = Mathf.Max(0f, _matchRemainingSeconds - deltaTime);

        if (_matchRemainingSeconds > 0f)
            return;

        State = FootballMatchState.Finished;
    }
}
