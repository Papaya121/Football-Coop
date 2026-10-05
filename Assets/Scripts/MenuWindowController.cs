using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MenuWindowController : MonoBehaviour
{
    private const string GameplaySceneName = "Gameplay";
    private const int MenuFrameRate = 60;
    private const float StickJoinThreshold = 0.5f;
    private static readonly Color SelectionColor = new Color(0.3f, 0.9f, 1f, 1f);

    [Header("Windows")]
    [SerializeField] private GameObject _mainWindow;
    [SerializeField] private GameObject _localWindow;
    [SerializeField] private GameObject _multiplayerWindow;
    [SerializeField] private GameObject _matchmakingWindow;
    [SerializeField] private CanvasGroup _mainWindowGroup;
    [SerializeField] private CanvasGroup _localWindowGroup;
    [SerializeField] private CanvasGroup _multiplayerWindowGroup;
    [SerializeField] private CanvasGroup _matchmakingWindowGroup;
    [SerializeField] private GameObject _settingsWindow;
    [SerializeField] private CanvasGroup _settingsWindowGroup;
    [SerializeField] private FootballSettingsWindow _settingsView;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _settingsBackButton;

    [Header("Window Transition")]
    [SerializeField, Min(0.01f)] private float _fadeOutDuration = 0.14f;
    [SerializeField, Min(0.01f)] private float _fadeInDuration = 0.22f;

    [Header("Main Window")]
    [SerializeField] private Button _localGameButton;
    [SerializeField] private Button _aiButton;
    [SerializeField] private Button _onlineButton;
    [SerializeField] private Button _tutorialButton;

    [Header("Local Window")]
    [SerializeField] private Button _localBackButton;
    [SerializeField] private Button _startButton;
    [SerializeField] private Transform _inputGroup;

    [Header("Multiplayer Window")]
    [SerializeField] private Button _multiplayerBackButton;
    [SerializeField] private Button _matchmakingButton;

    [Header("Matchmaking Window")]
    [SerializeField] private Button _matchmakingBackButton;
    [SerializeField] private TMP_Text _matchmakingStatusText;

    private TMP_Text[] _deviceLabels;
    private readonly Dictionary<GameObject, CanvasGroup> _windowGroupCache = new(4);
    private GameObject[] _windows;
    private Tween _windowTransition;
    private GameObject _visibleWindow;
    private bool _isLocalSetupOpen;
    private FootballNetworkManager _networkManager;
    private readonly Dictionary<Graphic, Outline> _selectionOutlines = new();
    private readonly HashSet<Selectable> _generatedNavigationButtons = new();
    private Outline _selectedOutline;
    private Coroutine _selectionCoroutine;
    private Gamepad _preferredGamepad;
    private int _lastGamepadJoinFrame = -1;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = MenuFrameRate;
        ValidateReferences();
        _localGameButton.gameObject.SetActive(!FootballInputPlatform.IsMobile);
        CacheViewData();
        BindButtons();
        _isLocalSetupOpen = false;
        ShowOnlyImmediate(_mainWindow);
    }

    private void OnEnable()
    {
        LocalPlayerSetupSession.Changed += RefreshLocalSetup;
        ResolveNetworkManager();
        SubscribeToNetworkEvents();
        RefreshLocalSetup();
    }

    private void Start()
    {
        RequestSelection(_mainWindow, GetPreferredSelectable(_mainWindow));
    }

    private void OnDisable()
    {
        if (_selectionCoroutine != null)
        {
            StopCoroutine(_selectionCoroutine);
            _selectionCoroutine = null;
        }

        SetSelectedOutline(null);
        _windowTransition?.Kill();
        _windowTransition = null;
        LocalPlayerSetupSession.Changed -= RefreshLocalSetup;
        UnsubscribeFromNetworkEvents();
    }

    private void Update()
    {
        UpdatePreferredGamepad();

        if (WasGamepadBackPressed())
        {
            HandleGamepadBack();
            return;
        }

        if (!_isLocalSetupOpen || LocalPlayerSetupSession.IsReady)
            return;

        TryJoinKeyboard();
        TryJoinGamepads();
    }

    private void LateUpdate()
    {
        if (_visibleWindow == null || FootballMatchResultModal.IsOpen)
        {
            SetSelectedOutline(null);
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        Selectable selected = eventSystem != null && eventSystem.currentSelectedGameObject != null
            ? eventSystem.currentSelectedGameObject.GetComponent<Selectable>()
            : null;

        if (selected == null || !selected.IsInteractable() ||
            !selected.transform.IsChildOf(_visibleWindow.transform))
        {
            SetSelectedOutline(null);

            if (_selectionCoroutine == null && GamepadNavigationHeld())
                RequestSelection(_visibleWindow, GetPreferredSelectable(_visibleWindow));

            return;
        }

        Graphic target = selected.targetGraphic;

        if (target == null)
        {
            SetSelectedOutline(null);
            return;
        }

        if (!_selectionOutlines.TryGetValue(target, out Outline outline) || outline == null)
        {
            outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = SelectionColor;
            outline.effectDistance = new Vector2(4f, -4f);
            outline.enabled = false;
            _selectionOutlines[target] = outline;
        }

        SetSelectedOutline(outline);
    }

    private void CacheViewData()
    {
        _windows = new[] { _mainWindow, _localWindow, _multiplayerWindow, _matchmakingWindow, _settingsWindow };
        _windowGroupCache.Clear();
        _windowGroupCache.Add(_mainWindow, _mainWindowGroup);
        _windowGroupCache.Add(_localWindow, _localWindowGroup);
        _windowGroupCache.Add(_multiplayerWindow, _multiplayerWindowGroup);
        _windowGroupCache.Add(_matchmakingWindow, _matchmakingWindowGroup);
        _windowGroupCache.Add(_settingsWindow, _settingsWindowGroup);

        var labels = new List<TMP_Text>(LocalPlayerSetupSession.PlayerCapacity);

        foreach (Transform slot in _inputGroup)
        {
            TMP_Text typeLabel = slot.GetComponentInChildren<TMP_Text>(true);
            if (typeLabel != null)
                labels.Add(typeLabel);
        }

        _deviceLabels = labels.ToArray();
    }

    private void BindButtons()
    {
        _settingsButton.onClick.AddListener(OpenSettings);
        _settingsBackButton.onClick.AddListener(CloseSettings);
        _localGameButton.onClick.AddListener(OpenLocalSetup);
        _aiButton.onClick.AddListener(StartAiGame);
        _onlineButton.onClick.AddListener(OpenMultiplayer);

        if (_tutorialButton != null)
            _tutorialButton.onClick.AddListener(StartTutorial);
        _localBackButton.onClick.AddListener(CancelLocalSetup);
        _startButton.onClick.AddListener(StartLocalGame);
        _multiplayerBackButton.onClick.AddListener(BackToMain);
        _matchmakingButton.onClick.AddListener(StartMatchmaking);
        _matchmakingBackButton.onClick.AddListener(CancelMatchmaking);
    }

    public void OpenSettings()
    {
        _isLocalSetupOpen = false;
        ShowOnly(_settingsWindow);
    }

    public void CloseSettings()
    {
        _settingsView.CommitNickname();
        ShowMainWindow();
    }

    private void OpenLocalSetup()
    {
        if (FootballInputPlatform.IsMobile)
            return;

        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonPlay);
        LocalPlayerSetupSession.Clear();
        ShowOnly(_localWindow);
        _isLocalSetupOpen = true;
    }

    private void CancelLocalSetup()
    {
        if (WasGamepadSubmitPressed() &&
            (!LocalPlayerSetupSession.IsReady || _lastGamepadJoinFrame == Time.frameCount))
            return;

        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonBack);
        LocalPlayerSetupSession.Clear();
        ShowMainWindow();
    }

    private void StartLocalGame()
    {
        if (!LocalPlayerSetupSession.IsReady)
            return;

        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonStartLocal);
        LocalPlayerSetupSession.Confirm();
        SceneManager.LoadScene(GameplaySceneName);
    }

    private void StartAiGame()
    {
        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonAi);
        StartSinglePlayerGame(false);
    }

    private void StartTutorial()
    {
        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonTutorial);
        StartSinglePlayerGame(true);
    }

    private void StartSinglePlayerGame(bool tutorial, bool onlineFallback = false)
    {
        FootballPlayerControlSource source;
        InputDevice device;
        Gamepad selectedGamepad = GetSubmittingGamepad() ?? _preferredGamepad;

        if (FootballInputPlatform.IsMobile)
        {
            source = FootballPlayerControlSource.Mobile;
            device = null;
        }
        else if (selectedGamepad != null && selectedGamepad.added)
        {
            source = FootballPlayerControlSource.Gamepad;
            device = selectedGamepad;
        }
        else if (Keyboard.current != null)
        {
            source = FootballPlayerControlSource.WasdKeyboard;
            device = Keyboard.current;
        }
        else if (Gamepad.current != null || Gamepad.all.Count > 0)
        {
            source = FootballPlayerControlSource.Gamepad;
            device = Gamepad.current != null ? Gamepad.current : Gamepad.all[0];
        }
        else
        {
            Debug.LogWarning("Cannot start an AI match because no keyboard or gamepad is connected.", this);
            return;
        }

        bool prepared = tutorial
            ? LocalPlayerSetupSession.PrepareTutorialMatch(source, device)
            : onlineFallback
                ? LocalPlayerSetupSession.PrepareOnlineFallbackAiMatch(source, device)
                : LocalPlayerSetupSession.PrepareAiMatch(source, device);

        if (!prepared)
        {
            Debug.LogWarning($"Failed to prepare the local {(tutorial ? "tutorial" : "AI match")} input.", this);
            return;
        }

        SceneManager.LoadScene(GameplaySceneName);
    }

    private void OpenMultiplayer()
    {
        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonOnline);
        ShowMultiplayer();
    }

    private void ShowMultiplayer()
    {
        LocalPlayerSetupSession.Clear();
        ShowOnly(_multiplayerWindow);
    }

    private void StartMatchmaking()
    {
        ResolveNetworkManager();

        if (_networkManager == null)
        {
            SetMatchmakingStatus("Сетевой менеджер не настроен");
            ShowOnly(_matchmakingWindow);
            return;
        }

        ShowOnly(_matchmakingWindow);
        SetMatchmakingStatus("Подключение");
        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonStartOnline);
        _networkManager.FindMatch();
    }

    private void CancelMatchmaking()
    {
        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonCancelMatchmaking);
        _networkManager?.CancelMatchmaking();
        ShowMultiplayer();
    }

    private void BackToMain()
    {
        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonBack);
        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        _isLocalSetupOpen = false;
        ShowOnly(_mainWindow);
    }

    private void ShowOnly(GameObject window)
    {
        if (_visibleWindow == window && (_windowTransition == null || !_windowTransition.IsActive()))
            return;

        GameObject outgoingWindow = _visibleWindow;
        CanvasGroup outgoingGroup = outgoingWindow != null ? _windowGroupCache[outgoingWindow] : null;
        CanvasGroup incomingGroup = window != null ? _windowGroupCache[window] : null;

        _windowTransition?.Kill();
        _windowTransition = null;

        for (int i = 0; i < _windows.Length; i++)
        {
            GameObject cachedWindow = _windows[i];
            if (cachedWindow == outgoingWindow || cachedWindow == window)
                continue;

            CanvasGroup cachedGroup = _windowGroupCache[cachedWindow];
            SetWindowInteraction(cachedGroup, false);
            cachedGroup.alpha = 0f;
            cachedWindow.SetActive(false);
        }

        if (outgoingGroup != null)
            SetWindowInteraction(outgoingGroup, false);

        if (incomingGroup != null)
        {
            SetWindowInteraction(incomingGroup, false);
            if (window != outgoingWindow)
                incomingGroup.alpha = 0f;
            window.SetActive(true);
        }

        _visibleWindow = window;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        SetSelectedOutline(null);
        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        if (outgoingGroup != null && outgoingWindow != window)
        {
            if (outgoingGroup.alpha > 0.001f)
                sequence.Append(outgoingGroup.DOFade(0f, _fadeOutDuration).SetEase(Ease.InCubic));

            sequence.AppendCallback(() => outgoingWindow.SetActive(false));
        }

        if (incomingGroup != null)
            sequence.Append(incomingGroup.DOFade(1f, _fadeInDuration).SetEase(Ease.OutCubic));

        _windowTransition = sequence
            .OnComplete(() =>
            {
                if (incomingGroup != null && _visibleWindow == window)
                {
                    SetWindowInteraction(incomingGroup, true);
                    RequestSelection(window, GetPreferredSelectable(window));
                }

                _windowTransition = null;
            })
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void ShowOnlyImmediate(GameObject window)
    {
        _windowTransition?.Kill();
        _windowTransition = null;
        _visibleWindow = window;
        SetSelectedOutline(null);

        for (int i = 0; i < _windows.Length; i++)
        {
            GameObject cachedWindow = _windows[i];
            bool isVisible = cachedWindow == window;
            CanvasGroup group = _windowGroupCache[cachedWindow];
            group.alpha = isVisible ? 1f : 0f;
            SetWindowInteraction(group, isVisible);
            cachedWindow.SetActive(isVisible);
        }
    }

    private static void SetWindowInteraction(CanvasGroup group, bool enabled)
    {
        group.interactable = enabled;
        group.blocksRaycasts = enabled;
    }

    private void ResolveNetworkManager()
    {
        FootballNetworkManager manager = FootballNetworkManager.Instance;

        if (manager == null)
            manager = FindAnyObjectByType<FootballNetworkManager>();

        if (_networkManager == manager)
            return;

        UnsubscribeFromNetworkEvents();
        _networkManager = manager;

        if (isActiveAndEnabled)
            SubscribeToNetworkEvents();
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkManager == null)
            return;

        _networkManager.MatchmakingStatusChanged -= OnNetworkStatusChanged;
        _networkManager.MatchLoading -= HideMenuWindows;
        _networkManager.ReturnedToMenu -= ShowMultiplayer;
        _networkManager.AiFallbackRequested -= StartAiFallbackGame;
        _networkManager.MatchmakingStatusChanged += OnNetworkStatusChanged;
        _networkManager.MatchLoading += HideMenuWindows;
        _networkManager.ReturnedToMenu += ShowMultiplayer;
        _networkManager.AiFallbackRequested += StartAiFallbackGame;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (_networkManager == null)
            return;

        _networkManager.MatchmakingStatusChanged -= OnNetworkStatusChanged;
        _networkManager.MatchLoading -= HideMenuWindows;
        _networkManager.ReturnedToMenu -= ShowMultiplayer;
        _networkManager.AiFallbackRequested -= StartAiFallbackGame;
    }

    private void StartAiFallbackGame()
    {
        GameParameterSessionValues.Clear();
        StartSinglePlayerGame(false, true);
    }

    private void SetMatchmakingStatus(string status)
    {
        if (_matchmakingStatusText != null)
            FootballLocalization.SetText(_matchmakingStatusText, status);
    }

    private void OnNetworkStatusChanged(string status)
    {
        FootballLocalization.SetText(_matchmakingStatusText,
            _networkManager.ClientStatusSource, _networkManager.ClientStatusArguments);
    }

    private void HideMenuWindows()
    {
        ShowOnly(null);
    }

    private void RefreshLocalSetup()
    {
        if (_startButton != null)
            _startButton.interactable = LocalPlayerSetupSession.IsReady;

        if (_visibleWindow == _localWindow)
        {
            if (LocalPlayerSetupSession.IsReady)
                RequestSelection(_localWindow, _startButton);
            else
                RequestSelection(_localWindow, _localBackButton);
        }

        if (_deviceLabels == null)
            return;

        for (int i = 0; i < _deviceLabels.Length; i++)
        {
            if (!LocalPlayerSetupSession.TryGetPlayer(i, out FootballPlayerControlSource source, out _))
                FootballLocalization.SetText(_deviceLabels[i], "Нажмите\nклавишу");
            else
                FootballLocalization.SetText(_deviceLabels[i], GetSourceLabel(source));
        }
    }

    private static string GetSourceLabel(FootballPlayerControlSource source)
    {
        return source switch
        {
            FootballPlayerControlSource.WasdKeyboard => "Keyboard\nWASD",
            FootballPlayerControlSource.ArrowKeyboard => "Keyboard\n← → ↑ ↓",
            FootballPlayerControlSource.Gamepad => "Gamepad",
            _ => source.ToString()
        };
    }

    private static void TryJoinKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ||
            keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            LocalPlayerSetupSession.TryAdd(FootballPlayerControlSource.WasdKeyboard, keyboard);

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame ||
            keyboard.downArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            LocalPlayerSetupSession.TryAdd(FootballPlayerControlSource.ArrowKeyboard, keyboard);
    }

    private void TryJoinGamepads()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            bool pressed = false;
            foreach (InputControl control in gamepad.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame)
                {
                    pressed = true;
                    break;
                }
            }

            if (pressed || gamepad.leftStick.ReadValue().sqrMagnitude >= StickJoinThreshold * StickJoinThreshold ||
                gamepad.rightStick.ReadValue().sqrMagnitude >= StickJoinThreshold * StickJoinThreshold)
            {
                if (LocalPlayerSetupSession.TryAdd(FootballPlayerControlSource.Gamepad, gamepad))
                    _lastGamepadJoinFrame = Time.frameCount;
            }
        }
    }

    private void RequestSelection(GameObject window, Selectable preferred)
    {
        if (_selectionCoroutine != null)
            StopCoroutine(_selectionCoroutine);

        _selectionCoroutine = StartCoroutine(SelectWindowButtonNextFrame(window, preferred));
    }

    private IEnumerator SelectWindowButtonNextFrame(GameObject window, Selectable preferred)
    {
        yield return null;
        _selectionCoroutine = null;

        if (!isActiveAndEnabled || window == null || window != _visibleWindow ||
            FootballMatchResultModal.IsOpen)
            yield break;

        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
            yield break;

        Canvas.ForceUpdateCanvases();
        List<Selectable> buttons = new();

        foreach (Selectable button in window.GetComponentsInChildren<Selectable>(false))
        {
            if (button.gameObject.activeInHierarchy && button.IsInteractable())
                buttons.Add(button);
        }

        foreach (Selectable button in buttons)
        {
            Navigation navigation = button.navigation;

            // Keep links configured by hand in the Button's Navigation settings.
            if (navigation.mode != Navigation.Mode.Automatic &&
                !_generatedNavigationButtons.Contains(button))
                continue;

            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = FindDirectionalButton(button, buttons, Vector2.up);
            navigation.selectOnDown = FindDirectionalButton(button, buttons, Vector2.down);
            navigation.selectOnLeft = FindDirectionalButton(button, buttons, Vector2.left);
            navigation.selectOnRight = FindDirectionalButton(button, buttons, Vector2.right);
            button.navigation = navigation;
            _generatedNavigationButtons.Add(button);
        }

        if (window == _mainWindow && _tutorialButton != null &&
            buttons.Contains(_tutorialButton) && buttons.Contains(_localGameButton))
        {
            // Learning sits below Game Buttons; Upper sits above them. Keep
            // vertical travel through the middle row instead of skipping it.
            SetGeneratedVerticalNeighbor(_tutorialButton, _localGameButton, true);
            SetGeneratedVerticalNeighbor(_localGameButton, _tutorialButton, false);
        }

        Selectable firstSelection = preferred != null && buttons.Contains(preferred)
            ? preferred
            : buttons.Count > 0 ? buttons[0] : null;

        if (firstSelection != null)
            eventSystem.SetSelectedGameObject(firstSelection.gameObject);
    }

    private static Selectable FindDirectionalButton(Selectable source, List<Selectable> buttons, Vector2 direction)
    {
        Vector2 origin = GetButtonCenter(source);
        Selectable nearest = null;
        float bestScore = float.PositiveInfinity;

        foreach (Selectable candidate in buttons)
        {
            if (candidate == source)
                continue;

            Vector2 offset = GetButtonCenter(candidate) - origin;
            float forward = Vector2.Dot(offset, direction);
            float sideways = Mathf.Abs(direction.x * offset.y - direction.y * offset.x);

            float maximumSideways = forward * (direction.y != 0f ? 2f : 0.75f);

            // Ignore buttons mostly to the side and leave an empty edge without wrapping.
            if (forward <= 0.01f || sideways > maximumSideways)
                continue;

            float score = forward + sideways * (direction.y != 0f ? 0.5f : 2f);

            if (score < bestScore)
            {
                bestScore = score;
                nearest = candidate;
            }
        }

        if (nearest != null || direction.y == 0f)
            return nearest;

        // The menu has separate button columns. When a column ends, Up/Down
        // should still reach the closest row above or below in another column.
        foreach (Selectable candidate in buttons)
        {
            if (candidate == source)
                continue;

            Vector2 offset = GetButtonCenter(candidate) - origin;
            float forward = Vector2.Dot(offset, direction);

            if (forward <= 0.01f)
                continue;

            float score = forward + Mathf.Abs(offset.x) * 0.25f;

            if (score < bestScore)
            {
                bestScore = score;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void SetGeneratedVerticalNeighbor(Button source, Button target, bool up)
    {
        if (!_generatedNavigationButtons.Contains(source))
            return;

        Navigation navigation = source.navigation;

        if (up)
            navigation.selectOnUp = target;
        else
            navigation.selectOnDown = target;

        source.navigation = navigation;
    }

    private static Vector2 GetButtonCenter(Selectable button)
    {
        if (button.transform is RectTransform rectTransform)
            return rectTransform.TransformPoint(rectTransform.rect.center);

        return button.transform.position;
    }

    private Selectable GetPreferredSelectable(GameObject window)
    {
        if (window == _mainWindow)
            return FootballInputPlatform.IsMobile ? _aiButton : _localGameButton;
        if (window == _localWindow)
            return LocalPlayerSetupSession.IsReady ? _startButton : _localBackButton;
        if (window == _multiplayerWindow)
            return _matchmakingButton;
        if (window == _matchmakingWindow)
            return _matchmakingBackButton;
        if (window == _settingsWindow)
            return _settingsView.PreferredSelection;
        return null;
    }

    private void SetSelectedOutline(Outline outline)
    {
        if (_selectedOutline == outline)
            return;

        if (_selectedOutline != null)
            _selectedOutline.enabled = false;

        _selectedOutline = outline;

        if (_selectedOutline != null)
            _selectedOutline.enabled = true;
    }

    private static bool GamepadNavigationHeld()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.dpad.ReadValue().sqrMagnitude > 0.25f ||
                gamepad.leftStick.ReadValue().sqrMagnitude > StickJoinThreshold * StickJoinThreshold ||
                gamepad.buttonSouth.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static bool WasGamepadBackPressed()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.buttonEast.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static bool WasGamepadSubmitPressed()
    {
        return GetSubmittingGamepad() != null;
    }

    private static Gamepad GetSubmittingGamepad()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame)
                return gamepad;
        }

        return null;
    }

    private void UpdatePreferredGamepad()
    {
        if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            _preferredGamepad = null;

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.dpad.ReadValue().sqrMagnitude > 0.25f ||
                gamepad.leftStick.ReadValue().sqrMagnitude > StickJoinThreshold * StickJoinThreshold ||
                gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame)
            {
                _preferredGamepad = gamepad;
                return;
            }
        }
    }

    private void HandleGamepadBack()
    {
        if (_visibleWindow == _localWindow)
            CancelLocalSetup();
        else if (_visibleWindow == _matchmakingWindow)
            CancelMatchmaking();
        else if (_visibleWindow == _multiplayerWindow)
            BackToMain();
        else if (_visibleWindow == _settingsWindow)
            CloseSettings();
    }

    private void ValidateReferences()
    {
        if (_settingsWindow == null || _settingsWindowGroup == null || _settingsView == null ||
            _settingsButton == null || _settingsBackButton == null ||
            _mainWindow == null || _localWindow == null || _multiplayerWindow == null ||
            _matchmakingWindow == null || _mainWindowGroup == null || _localWindowGroup == null ||
            _multiplayerWindowGroup == null || _matchmakingWindowGroup == null ||
            _localGameButton == null || _aiButton == null ||
            _onlineButton == null || _localBackButton == null || _startButton == null ||
            _inputGroup == null || _multiplayerBackButton == null || _matchmakingButton == null ||
            _matchmakingBackButton == null || _matchmakingStatusText == null)
            throw new MissingReferenceException(
                $"{nameof(MenuWindowController)} on '{name}' has missing UI references. " +
                "Use Reset in the component context menu to auto-wire empty fields.");
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AutoWireReferences();
    }

    private void OnValidate()
    {
        _fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);
        _fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);

        if (!Application.isPlaying)
            AutoWireReferences();
    }

    private void AutoWireReferences()
    {
        _mainWindow ??= transform.Find("Main Window")?.gameObject;
        _localWindow ??= transform.Find("Local Window")?.gameObject;
        _multiplayerWindow ??= transform.Find("Multiplayer Window")?.gameObject;
        _matchmakingWindow ??= transform.Find("Matchmaking Window")?.gameObject;
        _settingsWindow ??= transform.Find("Settings")?.gameObject;
        _settingsWindowGroup ??= _settingsWindow != null ? _settingsWindow.GetComponent<CanvasGroup>() : null;
        _settingsView ??= _settingsWindow != null ? _settingsWindow.GetComponent<FootballSettingsWindow>() : null;
        _settingsButton ??= FindOptionalButton(_mainWindow?.transform, "Settings Button");
        _settingsBackButton ??= _settingsWindow != null ? _settingsWindow.transform.Find("Button")?.GetComponent<Button>() : null;
        _mainWindowGroup ??= _mainWindow != null ? _mainWindow.GetComponent<CanvasGroup>() : null;
        _localWindowGroup ??= _localWindow != null ? _localWindow.GetComponent<CanvasGroup>() : null;
        _multiplayerWindowGroup ??= _multiplayerWindow != null ? _multiplayerWindow.GetComponent<CanvasGroup>() : null;
        _matchmakingWindowGroup ??= _matchmakingWindow != null ? _matchmakingWindow.GetComponent<CanvasGroup>() : null;

        Transform gameButtons = _mainWindow != null
            ? FindDescendant(_mainWindow.transform, "Game Buttons")
            : null;

        _localGameButton ??= FindOptionalButton(gameButtons, "LocalGame Button");
        _aiButton ??= FindOptionalButton(gameButtons, "AI Button");
        _onlineButton ??= FindOptionalButton(gameButtons, "Online Button");
        _tutorialButton ??= FindOptionalButton(_mainWindow?.transform, "Learning Button");
        _localBackButton ??= FindOptionalButton(_localWindow?.transform, "Back Button");
        _startButton ??= FindOptionalButton(_localWindow?.transform, "Start Button");
        _inputGroup ??= FindDescendant(_localWindow?.transform, "Input Group");
        _multiplayerBackButton ??= FindOptionalButton(_multiplayerWindow?.transform, "Back Button");
        _matchmakingButton ??= FindOptionalButton(_multiplayerWindow?.transform, "LocalGame Button");
        _matchmakingBackButton ??= FindOptionalButton(_matchmakingWindow?.transform, "Back Button");

        if (_matchmakingStatusText == null)
        {
            Transform buttons = FindDescendant(_matchmakingWindow?.transform, "Buttons Group");
            Transform status = FindDescendant(buttons, "Input Text");
            _matchmakingStatusText = status != null ? status.GetComponent<TMP_Text>() : null;
        }
    }

    private static Button FindOptionalButton(Transform root, string buttonName)
    {
        Transform target = FindDescendant(root, buttonName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if (child.name == objectName)
                return child;

            Transform nested = FindDescendant(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
#endif

    private static void Quit()
    {
        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonExit);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
