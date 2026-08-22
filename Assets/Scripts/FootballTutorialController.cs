using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class FootballTutorialController : MonoBehaviour
{
    private const float AnimationDuration = 0.22f;
    private const float MinimumInputDelay = 0.2f;
    private const float MinimumGameplayTimeBetweenTips = 2.5f;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _contentText;

    private FootballPlayerController _player;
    private FootballBall _ball;
    private FootballBallKicker _kicker;
    private FootballBallHeader _header;
    private FootballBallBicycleKicker _bicycleKicker;
    private Coroutine _sequence;
    private int _currentStepIndex = -1;
    private bool _gameIsPaused;
    private float _timeScaleBeforePause = 1f;

    private readonly TutorialStep[] _steps =
    {
        new TutorialStep(
            "ДВИЖЕНИЕ",
            TutorialAction.Move,
            TutorialSituation.Immediate,
            "Нажмите {move}, чтобы двигаться и развернуться."),
        new TutorialStep(
            "УДАР НОГОЙ",
            TutorialAction.Kick,
            TutorialSituation.BallInKickZone,
            "Мяч рядом! Нажмите {kick}, чтобы ударить в сторону, куда смотрит игрок."),
        new TutorialStep(
            "ВЫСОКИЙ МЯЧ",
            TutorialAction.Jump,
            TutorialSituation.HighBallNearby,
            "Мяч летит высоко. Нажмите {jump}, чтобы добраться до него."),
        new TutorialStep(
            "ДВОЙНОЙ ПРЫЖОК",
            TutorialAction.Jump,
            TutorialSituation.PlayerAirborne,
            "Пока игрок в воздухе, нажмите {jump} ещё раз, чтобы прыгнуть выше."),
        new TutorialStep(
            "УДАР ГОЛОВОЙ",
            TutorialAction.Header,
            TutorialSituation.BallInHeaderZone,
            "Мяч у головы! Нажмите {header}, чтобы направить его вперёд."),
        new TutorialStep(
            "УДАР ЧЕРЕЗ СЕБЯ",
            TutorialAction.Kick,
            TutorialSituation.BallInBicycleKickZone,
            "В воздухе удерживайте {back} и нажмите {kick}, чтобы выполнить удар через себя."),
        new TutorialStep(
            "ОБУЧЕНИЕ ЗАВЕРШЕНО",
            TutorialAction.Jump,
            TutorialSituation.Immediate,
            "Теперь попробуйте забить гол! Нажмите {jump}, чтобы продолжить матч.")
    };

    private void Awake()
    {
        ResolveView();

        if (!LocalPlayerSetupSession.IsTutorial)
        {
            gameObject.SetActive(false);
            return;
        }

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _panel.localScale = Vector3.one * 0.82f;
    }

    private void Start()
    {
        if (LocalPlayerSetupSession.IsTutorial)
            _sequence = StartCoroutine(RunTutorial());
    }

    private void OnDisable()
    {
        if (_player != null)
            _player.InputAssigned -= OnInputAssigned;

        ResumeGame();
    }

    private IEnumerator RunTutorial()
    {
        yield return null;
        ResolvePlayer();
        ResolveGameplayReferences();

        if (_player != null)
            _player.InputAssigned += OnInputAssigned;

        yield return new WaitForSecondsRealtime(0.35f);

        for (int index = 0; index < _steps.Length; index++)
        {
            _currentStepIndex = index;

            while (!IsSituationReady(_steps[index].Situation))
            {
                if (WasSkipPressed())
                {
                    FinishTutorial();
                    yield break;
                }

                yield return null;
            }

            PauseGame();
            RefreshStep(_steps[index], index);
            yield return AnimateWindow(true);

            float inputAllowedAt = Time.unscaledTime + MinimumInputDelay;
            while (Time.unscaledTime < inputAllowedAt || !WasActionPressed(_steps[index].Action))
            {
                if (WasSkipPressed())
                {
                    yield return AnimateWindow(false);
                    FinishTutorial();
                    yield break;
                }

                yield return null;
            }

            yield return AnimateWindow(false);
            ResumeGame();

            if (index < _steps.Length - 1)
            {
                float delay = _steps[index].Situation == TutorialSituation.HighBallNearby
                    ? 0.15f
                    : MinimumGameplayTimeBetweenTips;
                yield return new WaitForSecondsRealtime(delay);
            }
        }

        FinishTutorial();
    }

    private void FinishTutorial()
    {
        ResumeGame();

        if (_player != null)
            _player.InputAssigned -= OnInputAssigned;

        _sequence = null;
        _currentStepIndex = -1;
        gameObject.SetActive(false);
    }

    private void PauseGame()
    {
        if (_gameIsPaused)
            return;

        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        _gameIsPaused = true;
    }

    private void ResumeGame()
    {
        if (!_gameIsPaused)
            return;

        Time.timeScale = _timeScaleBeforePause;
        _gameIsPaused = false;
    }

    private IEnumerator AnimateWindow(bool showing)
    {
        if (showing)
            _canvasGroup.blocksRaycasts = true;

        float startAlpha = _canvasGroup.alpha;
        float targetAlpha = showing ? 1f : 0f;
        Vector3 startScale = _panel.localScale;
        Vector3 targetScale = Vector3.one * (showing ? 1f : 0.88f);
        float elapsed = 0f;

        while (elapsed < AnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / AnimationDuration);
            float eased = showing ? EaseOutBack(progress) : progress * progress;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            _panel.localScale = Vector3.LerpUnclamped(startScale, targetScale, eased);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        _panel.localScale = targetScale;

        if (!showing)
            _canvasGroup.blocksRaycasts = false;
    }

    private bool IsSituationReady(TutorialSituation situation)
    {
        if (situation == TutorialSituation.Immediate)
            return true;

        if (_player == null || _ball == null)
        {
            ResolvePlayer();
            ResolveGameplayReferences();
            return false;
        }

        Vector3 ballOffset = _ball.transform.position - _player.transform.position;

        return situation switch
        {
            TutorialSituation.BallInKickZone => IsBallInKickZone(),
            TutorialSituation.HighBallNearby => _player.IsGrounded &&
                Mathf.Abs(ballOffset.x) <= 2.4f && ballOffset.y >= 1.25f && ballOffset.y <= 4f,
            TutorialSituation.PlayerAirborne => !_player.IsGrounded,
            TutorialSituation.BallInHeaderZone => IsBallInHeaderZone(),
            TutorialSituation.BallInBicycleKickZone => IsBallInBicycleKickZone(),
            _ => false
        };
    }

    private bool IsBallInKickZone()
    {
        if (_kicker == null)
            return false;

        Vector3 offset = _ball.transform.position - _kicker.ZoneOrigin;
        return Mathf.Abs(offset.x) <= _kicker.ZoneRange + 0.45f &&
            Mathf.Abs(offset.y) <= _kicker.ZoneRange + 0.5f &&
            offset.x * _player.FacingDirection >= -0.2f;
    }

    private bool IsBallInHeaderZone()
    {
        if (_header == null)
            return false;

        Vector3 offset = _ball.transform.position - _header.ZoneOrigin;
        return offset.magnitude <= _header.ZoneRange + 0.55f &&
            _ball.transform.position.y >= _player.transform.position.y + 0.7f &&
            offset.x * _player.FacingDirection >= -0.25f;
    }

    private bool IsBallInBicycleKickZone()
    {
        if (_bicycleKicker == null || _player.IsGrounded)
            return false;

        Vector3 offset = _ball.transform.position - _bicycleKicker.ZoneOrigin;
        return offset.magnitude <= _bicycleKicker.ZoneRange + 0.5f &&
            offset.y >= _bicycleKicker.MinimumBallHeightFromOrigin - 0.15f;
    }

    private void RefreshStep(TutorialStep step, int index)
    {
        _titleText.text = step.Title;
        _contentText.text = FormatControls(step.Text) + "\n\n<size=55%><color=#A9A9A9>Esc / Menu — пропустить обучение</color></size>";
    }

    private string FormatControls(string text)
    {
        FootballPlayerControlSource source = _player != null
            ? _player.ControlSource
            : FootballPlayerControlSource.WasdKeyboard;

        return text
            .Replace("{move}", ControlLabel(GetMoveLabel(source)))
            .Replace("{jump}", ControlLabel(GetJumpLabel(source)))
            .Replace("{kick}", ControlLabel(GetKickLabel(source)))
            .Replace("{header}", ControlLabel(GetHeaderLabel(source)))
            .Replace("{back}", ControlLabel(GetBackLabel(source)));
    }

    private bool WasActionPressed(TutorialAction action)
    {
        FootballPlayerControlSource source = _player != null
            ? _player.ControlSource
            : FootballPlayerControlSource.WasdKeyboard;

        if (source == FootballPlayerControlSource.Gamepad)
        {
            Gamepad gamepad = _player?.ControlDevice as Gamepad ?? Gamepad.current;
            if (gamepad == null)
                return false;

            return action switch
            {
                TutorialAction.Move => Mathf.Abs(gamepad.leftStick.ReadValue().x) > 0.45f ||
                    gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame,
                TutorialAction.Jump => gamepad.buttonSouth.wasPressedThisFrame,
                TutorialAction.Kick => gamepad.buttonEast.wasPressedThisFrame,
                TutorialAction.Header => gamepad.buttonNorth.wasPressedThisFrame,
                _ => false
            };
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (source == FootballPlayerControlSource.ArrowKeyboard)
        {
            return action switch
            {
                TutorialAction.Move => keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame,
                TutorialAction.Jump => keyboard.upArrowKey.wasPressedThisFrame,
                TutorialAction.Kick => keyboard.leftBracketKey.wasPressedThisFrame,
                TutorialAction.Header => keyboard.rightBracketKey.wasPressedThisFrame,
                _ => false
            };
        }

        return action switch
        {
            TutorialAction.Move => keyboard.aKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame,
            TutorialAction.Jump => keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame,
            TutorialAction.Kick => keyboard.kKey.wasPressedThisFrame,
            TutorialAction.Header => keyboard.jKey.wasPressedThisFrame,
            _ => false
        };
    }

    private static bool WasSkipPressed()
    {
        return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
    }

    private void OnInputAssigned(FootballPlayerControlSource source, InputDevice device)
    {
        if (_sequence != null && _currentStepIndex >= 0 && _canvasGroup.alpha > 0f)
            RefreshStep(_steps[_currentStepIndex], _currentStepIndex);
    }

    private void ResolvePlayer()
    {
        FootballPlayerJoinManager joinManager = FindAnyObjectByType<FootballPlayerJoinManager>();
        if (joinManager != null)
            _player = joinManager.GetPlayer(0);

        if (_player == null)
        {
            FootballPlayerController[] players = FindObjectsByType<FootballPlayerController>(FindObjectsInactive.Exclude);
            foreach (FootballPlayerController player in players)
            {
                if (player.GetComponent<FootballBotBrain>() == null)
                {
                    _player = player;
                    break;
                }
            }
        }
    }

    private void ResolveGameplayReferences()
    {
        if (_player != null)
        {
            if (_kicker == null)
                _kicker = _player.GetComponent<FootballBallKicker>();

            if (_header == null)
                _header = _player.GetComponent<FootballBallHeader>();

            if (_bicycleKicker == null)
                _bicycleKicker = _player.GetComponent<FootballBallBicycleKicker>();
        }

        if (_ball == null)
            _ball = FindAnyObjectByType<FootballBall>();
    }

    private void ResolveView()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_panel == null)
            _panel = transform.Find("Learning Panel") as RectTransform;

        if (_panel != null)
        {
            if (_titleText == null)
                _titleText = _panel.Find("Title Text")?.GetComponent<TMP_Text>();

            if (_contentText == null)
                _contentText = _panel.Find("Content Text")?.GetComponent<TMP_Text>();
        }

        if (_canvasGroup == null || _panel == null || _titleText == null || _contentText == null)
            throw new MissingReferenceException("Learning Window must contain CanvasGroup, Learning Panel, Title Text and Content Text.");
    }

    private static string ControlLabel(string label) => $"<color=#94D86A><b>[ {label} ]</b></color>";

    private static string GetMoveLabel(FootballPlayerControlSource source) => source switch
    {
        FootballPlayerControlSource.ArrowKeyboard => "←  →",
        FootballPlayerControlSource.Gamepad => "ЛЕВЫЙ СТИК",
        _ => "A  D"
    };

    private string GetJumpLabel(FootballPlayerControlSource source) => source switch
    {
        FootballPlayerControlSource.ArrowKeyboard => "↑",
        FootballPlayerControlSource.Gamepad => IsPlayStationGamepad() ? "✕" : "A",
        _ => "SPACE / W"
    };

    private string GetKickLabel(FootballPlayerControlSource source) => source switch
    {
        FootballPlayerControlSource.ArrowKeyboard => "[",
        FootballPlayerControlSource.Gamepad => IsPlayStationGamepad() ? "○" : "B",
        _ => "K"
    };

    private string GetHeaderLabel(FootballPlayerControlSource source) => source switch
    {
        FootballPlayerControlSource.ArrowKeyboard => "]",
        FootballPlayerControlSource.Gamepad => IsPlayStationGamepad() ? "△" : "Y",
        _ => "J"
    };

    private string GetBackLabel(FootballPlayerControlSource source)
    {
        if (source == FootballPlayerControlSource.Gamepad)
            return "СТИК НАЗАД";

        bool facingRight = _player == null || _player.FacingDirection >= 0;
        if (source == FootballPlayerControlSource.ArrowKeyboard)
            return facingRight ? "←" : "→";

        return facingRight ? "A" : "D";
    }

    private bool IsPlayStationGamepad()
    {
        string product = (_player?.ControlDevice ?? Gamepad.current)?.description.product ?? string.Empty;
        product = product.ToLowerInvariant();
        return product.Contains("dualshock") || product.Contains("dualsense") || product.Contains("playstation");
    }

    private static float EaseOutBack(float value)
    {
        const float overshoot = 1.35f;
        float shifted = value - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
    }

    private readonly struct TutorialStep
    {
        public TutorialStep(string title, TutorialAction action, TutorialSituation situation, string text)
        {
            Title = title;
            Action = action;
            Situation = situation;
            Text = text;
        }

        public string Title { get; }
        public TutorialAction Action { get; }
        public TutorialSituation Situation { get; }
        public string Text { get; }
    }

    private enum TutorialAction
    {
        Move,
        Jump,
        Kick,
        Header
    }

    private enum TutorialSituation
    {
        Immediate,
        BallInKickZone,
        HighBallNearby,
        PlayerAirborne,
        BallInHeaderZone,
        BallInBicycleKickZone
    }
}
