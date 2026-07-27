using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballMatchHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private string _noPlayersText = "Нажмите любую кнопку";
    [SerializeField] private string _onePlayerText = "Ожидание второго игрока";
    [SerializeField] private string[] _countdownTexts = { "3", "2", "1" };
    [SerializeField] private string _runningStatusText = "";
    [SerializeField] private string _leftGoalText = "<color=#65A5AC>Гол!</color>";
    [SerializeField] private string _rightGoalText = "<color=#C34A4A>Гол!</color>";
    [SerializeField] private string _leftWinnerText = "Матч закончен!\n<color=#65A5AC>Синий</color> победил!";
    [SerializeField] private string _rightWinnerText = "Матч закончен!\n<color=#C34A4A>Красный</color> победил!";
    [SerializeField] private string _drawStatusText = "Матч закончен!\nНичья!";
    [SerializeField] private string _victoryText = "Матч закончен!\nВы победили!";
    [SerializeField] private string _defeatText = "Матч закончен!\nВы проиграли!";
    [SerializeField] private bool _hideEmptyStatus = true;

    private string _currentStatusText;
    private string _currentTimerText;

    public void ShowWaiting(int playerCount, float matchDurationSeconds)
    {
        SetStatus(playerCount <= 0 ? _noPlayersText : _onePlayerText);
        SetTimer(matchDurationSeconds);
    }

    public void ShowCountdown(int countdownValue, float matchDurationSeconds)
    {
        SetStatus(GetCountdownText(countdownValue));
        SetTimer(matchDurationSeconds);
    }

    public void ShowRunning(float remainingSeconds)
    {
        SetStatus(_runningStatusText);
        SetTimer(remainingSeconds);
    }

    public void ShowGoal(FootballTeamSide scoringSide, float remainingSeconds)
    {
        SetStatus(scoringSide == FootballTeamSide.Left ? _leftGoalText : _rightGoalText);
        SetTimer(remainingSeconds);
    }

    public void ShowFinished(float remainingSeconds, FootballMatchResult result)
    {
        SetStatus(GetFinishedText(result));
        SetTimer(remainingSeconds);
    }

    public void ShowNetworkFinished(
        float remainingSeconds,
        FootballMatchResult result,
        FootballTeamSide localSide)
    {
        bool isDraw = result == FootballMatchResult.Draw;
        bool localPlayerWon =
            (result == FootballMatchResult.LeftWon && localSide == FootballTeamSide.Left) ||
            (result == FootballMatchResult.RightWon && localSide == FootballTeamSide.Right);

        SetStatus(isDraw ? _drawStatusText : localPlayerWon ? _victoryText : _defeatText);
        SetTimer(remainingSeconds);
    }

    private void SetStatus(string value)
    {
        if (_statusText == null)
            return;

        if (_currentStatusText != value)
        {
            _statusText.text = value;
            _currentStatusText = value;
        }

        bool shouldBeActive = !_hideEmptyStatus || !string.IsNullOrEmpty(value);

        if (_statusText.gameObject.activeSelf != shouldBeActive)
            _statusText.gameObject.SetActive(shouldBeActive);
    }

    private void SetTimer(float seconds)
    {
        if (_timerText == null)
            return;

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int secondsPart = totalSeconds % 60;
        string value = $"{minutes:00}:{secondsPart:00}";

        if (_currentTimerText == value)
            return;

        _timerText.text = value;
        _currentTimerText = value;
    }

    private string GetCountdownText(int countdownValue)
    {
        if (_countdownTexts == null || _countdownTexts.Length == 0)
            return countdownValue.ToString();

        int index = _countdownTexts.Length - countdownValue;

        if (index >= 0 && index < _countdownTexts.Length)
            return _countdownTexts[index];

        return countdownValue.ToString();
    }

    private string GetFinishedText(FootballMatchResult result)
    {
        switch (result)
        {
            case FootballMatchResult.LeftWon:
                return _leftWinnerText;
            case FootballMatchResult.RightWon:
                return _rightWinnerText;
            default:
                return _drawStatusText;
        }
    }
}
