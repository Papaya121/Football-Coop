using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MenuWindowController : MonoBehaviour
{
    private const string GameplaySceneName = "Gameplay";
    private const float StickJoinThreshold = 0.5f;

    [Header("Windows")]
    [SerializeField] private GameObject _mainWindow;
    [SerializeField] private GameObject _localWindow;
    [SerializeField] private GameObject _multiplayerWindow;
    [SerializeField] private GameObject _matchmakingWindow;
    [SerializeField] private CanvasGroup _mainWindowGroup;
    [SerializeField] private CanvasGroup _localWindowGroup;
    [SerializeField] private CanvasGroup _multiplayerWindowGroup;
    [SerializeField] private CanvasGroup _matchmakingWindowGroup;

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

    private void Awake()
    {
        ValidateReferences();
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

    private void OnDisable()
    {
        _windowTransition?.Kill();
        _windowTransition = null;
        LocalPlayerSetupSession.Changed -= RefreshLocalSetup;
        UnsubscribeFromNetworkEvents();
    }

    private void Update()
    {
        if (!_isLocalSetupOpen || LocalPlayerSetupSession.IsReady)
            return;

        TryJoinKeyboard();
        TryJoinGamepads();
    }

    private void CacheViewData()
    {
        _windows = new[] { _mainWindow, _localWindow, _multiplayerWindow, _matchmakingWindow };
        _windowGroupCache.Clear();
        _windowGroupCache.Add(_mainWindow, _mainWindowGroup);
        _windowGroupCache.Add(_localWindow, _localWindowGroup);
        _windowGroupCache.Add(_multiplayerWindow, _multiplayerWindowGroup);
        _windowGroupCache.Add(_matchmakingWindow, _matchmakingWindowGroup);

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

    private void OpenLocalSetup()
    {
        FootballAnalytics.MenuButton(FootballAnalytics.PressButtonPlay);
        LocalPlayerSetupSession.Clear();
        ShowOnly(_localWindow);
        _isLocalSetupOpen = true;
    }

    private void CancelLocalSetup()
    {
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

    private void StartSinglePlayerGame(bool tutorial)
    {
        FootballPlayerControlSource source;
        InputDevice device;

        if (Keyboard.current != null)
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
        SetMatchmakingStatus("Подключение к серверу…");
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
                    SetWindowInteraction(incomingGroup, true);

                _windowTransition = null;
            })
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void ShowOnlyImmediate(GameObject window)
    {
        _windowTransition?.Kill();
        _windowTransition = null;
        _visibleWindow = window;

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
        if (_networkManager == null)
            _networkManager = FindAnyObjectByType<FootballNetworkManager>();
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkManager == null)
            return;

        _networkManager.MatchmakingStatusChanged -= SetMatchmakingStatus;
        _networkManager.MatchLoading -= HideMenuWindows;
        _networkManager.ReturnedToMenu -= ShowMultiplayer;
        _networkManager.MatchmakingStatusChanged += SetMatchmakingStatus;
        _networkManager.MatchLoading += HideMenuWindows;
        _networkManager.ReturnedToMenu += ShowMultiplayer;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (_networkManager == null)
            return;

        _networkManager.MatchmakingStatusChanged -= SetMatchmakingStatus;
        _networkManager.MatchLoading -= HideMenuWindows;
        _networkManager.ReturnedToMenu -= ShowMultiplayer;
    }

    private void SetMatchmakingStatus(string status)
    {
        if (_matchmakingStatusText != null)
            _matchmakingStatusText.text = status;
    }

    private void HideMenuWindows()
    {
        ShowOnly(null);
    }

    private void RefreshLocalSetup()
    {
        if (_startButton != null)
            _startButton.interactable = LocalPlayerSetupSession.IsReady;

        if (_deviceLabels == null)
            return;

        for (int i = 0; i < _deviceLabels.Length; i++)
        {
            if (!LocalPlayerSetupSession.TryGetPlayer(i, out FootballPlayerControlSource source, out _))
                _deviceLabels[i].text = "Нажмите\nклавишу";
            else
                _deviceLabels[i].text = GetSourceLabel(source);
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

    private static void TryJoinGamepads()
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
                LocalPlayerSetupSession.TryAdd(FootballPlayerControlSource.Gamepad, gamepad);
        }
    }

    private void ValidateReferences()
    {
        if (_mainWindow == null || _localWindow == null || _multiplayerWindow == null ||
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
