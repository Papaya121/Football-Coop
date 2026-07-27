using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballMatchController : MonoBehaviour
{
    [SerializeField] private FootballPlayerJoinManager _joinManager;
    [SerializeField] private FootballScoreController _scoreController;
    [SerializeField] private FootballMatchResetter _matchResetter;
    [SerializeField] private FootballMatchHudView _hudView;
    [SerializeField, Min(1f)] private float _matchDurationSeconds = 180f;
    [SerializeField, Min(1)] private int _countdownSeconds = 3;
    [SerializeField, Min(0f)] private float _goalPauseSeconds = 2f;

    private FootballMatchClock _clock;
    private FootballTeamSide _lastScoringSide;
    private bool _hasLastScoringSide;

    public FootballMatchState State => _clock?.State ?? FootballMatchState.WaitingForPlayers;

    private void Awake()
    {
        _clock = new FootballMatchClock(_matchDurationSeconds, _countdownSeconds);
    }

    private void OnEnable()
    {
        if (_joinManager != null)
            _joinManager.PlayerCountChanged += OnPlayerCountChanged;

        if (_scoreController != null)
            _scoreController.GoalScored += OnGoalScored;
    }

    private void Start()
    {
        RefreshPlayerState();
        RefreshHud();
    }

    private void Update()
    {
        if (_clock == null)
            return;

        FootballMatchState previousState = _clock.State;

        _clock.Tick(Time.deltaTime);

        if (ShouldResetPositionsOnRunningEnter(previousState, _clock.State))
        {
            _matchResetter?.ResetToStartPositions();
            FootballSoundPlayer.TryPlay(FootballSoundIds.Whistle, transform.position);
        }

        if (previousState != FootballMatchState.Finished && _clock.State == FootballMatchState.Finished)
            FootballSoundPlayer.TryPlay(FootballSoundIds.Whistle, transform.position);

        RefreshHud();
    }

    private void OnDisable()
    {
        if (_joinManager != null)
            _joinManager.PlayerCountChanged -= OnPlayerCountChanged;

        if (_scoreController != null)
            _scoreController.GoalScored -= OnGoalScored;
    }

    private void OnPlayerCountChanged(int playerCount)
    {
        RefreshPlayerState();
        RefreshHud();
    }

    private void OnGoalScored(FootballTeamSide scoringSide, int leftScore, int rightScore)
    {
        if (_clock == null || _clock.State != FootballMatchState.Running)
            return;

        _lastScoringSide = scoringSide;
        _hasLastScoringSide = true;

        _clock.StartGoalPause(_goalPauseSeconds);

        if (_clock.State == FootballMatchState.Running)
            _matchResetter?.ResetToStartPositions();

        RefreshHud();
    }

    private void RefreshPlayerState()
    {
        if (_joinManager == null || _clock == null)
            return;

        if (_clock.State != FootballMatchState.WaitingForPlayers)
            return;

        if (_joinManager.HasRequiredPlayers)
            _clock.StartCountdown();
        else
            _clock.WaitForPlayers();
    }

    private void RefreshHud()
    {
        if (_hudView == null || _clock == null)
            return;

        switch (_clock.State)
        {
            case FootballMatchState.WaitingForPlayers:
                _hudView.ShowWaiting(_joinManager != null ? _joinManager.AssignedPlayerCount : 0, _clock.MatchDurationSeconds);
                break;
            case FootballMatchState.Countdown:
                _hudView.ShowCountdown(_clock.CountdownValue, _clock.MatchDurationSeconds);
                break;
            case FootballMatchState.Running:
                _hudView.ShowRunning(_clock.MatchRemainingSeconds);
                break;
            case FootballMatchState.GoalPause:
                _hudView.ShowGoal(_hasLastScoringSide ? _lastScoringSide : FootballTeamSide.Left, _clock.MatchRemainingSeconds);
                break;
            case FootballMatchState.Finished:
                _hudView.ShowFinished(_clock.MatchRemainingSeconds, GetMatchResult());
                break;
        }
    }

    private static bool ShouldResetPositionsOnRunningEnter(FootballMatchState previousState, FootballMatchState currentState)
    {
        if (currentState != FootballMatchState.Running)
            return false;

        return previousState == FootballMatchState.Countdown || previousState == FootballMatchState.GoalPause;
    }

    private FootballMatchResult GetMatchResult()
    {
        if (_scoreController == null)
            return FootballMatchResult.Draw;

        if (_scoreController.LeftScore > _scoreController.RightScore)
            return FootballMatchResult.LeftWon;

        if (_scoreController.RightScore > _scoreController.LeftScore)
            return FootballMatchResult.RightWon;

        return FootballMatchResult.Draw;
    }
}
