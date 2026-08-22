using System.Collections.Generic;
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

    private GameObject _mainWindow;
    private GameObject _localWindow;
    private GameObject _multiplayerWindow;
    private GameObject _matchmakingWindow;
    private Button _startButton;
    private TMP_Text _matchmakingStatusText;
    private TMP_Text[] _deviceLabels;
    private bool _isLocalSetupOpen;
    private FootballNetworkManager _networkManager;

    private void Awake()
    {
        ResolveView();
        BindButtons();
        ShowMainWindow();
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

    private void ResolveView()
    {
        _mainWindow = FindDirectChild("Main Window").gameObject;
        _localWindow = FindDirectChild("Local Window").gameObject;
        _multiplayerWindow = FindDirectChild("Multiplayer Window").gameObject;
        _matchmakingWindow = FindDirectChild("Matchmaking Window").gameObject;

        _startButton = FindButton(_localWindow.transform, "Start Button");
        Transform inputGroup = FindDescendant(_localWindow.transform, "Input Group");
        var labels = new List<TMP_Text>(LocalPlayerSetupSession.PlayerCapacity);

        foreach (Transform slot in inputGroup)
        {
            TMP_Text typeLabel = FindDescendant(slot, "Type Text")?.GetComponent<TMP_Text>();
            if (typeLabel != null)
                labels.Add(typeLabel);
        }

        _deviceLabels = labels.ToArray();

        Transform matchmakingButtons = FindDescendant(_matchmakingWindow.transform, "Buttons Group");
        _matchmakingStatusText = FindDescendant(matchmakingButtons, "Input Text")?.GetComponent<TMP_Text>();
    }

    private void BindButtons()
    {
        FindButton(_mainWindow.transform, "LocalGame Button").onClick.AddListener(OpenLocalSetup);
        Button aiButton = FindOptionalButton(_mainWindow.transform, "AI Button");

        if (aiButton != null)
            aiButton.onClick.AddListener(StartAiGame);

        Button learningButton = FindOptionalButton(_mainWindow.transform, "Learning Button");

        if (learningButton != null)
            learningButton.onClick.AddListener(StartTutorial);

        FindButton(_mainWindow.transform, "MultiplayerGame Button").onClick.AddListener(OpenMultiplayer);
        FindButton(_mainWindow.transform, "Exit Button").onClick.AddListener(Quit);
        FindButton(_localWindow.transform, "Back Button").onClick.AddListener(CancelLocalSetup);
        _startButton.onClick.AddListener(StartLocalGame);
        FindButton(_multiplayerWindow.transform, "Back Button").onClick.AddListener(ShowMainWindow);
        FindButton(_multiplayerWindow.transform, "LocalGame Button").onClick.AddListener(StartMatchmaking);
        FindButton(_matchmakingWindow.transform, "Back Button").onClick.AddListener(CancelMatchmaking);
    }

    private void OpenLocalSetup()
    {
        LocalPlayerSetupSession.Clear();
        ShowOnly(_localWindow);
        _isLocalSetupOpen = true;
    }

    private void CancelLocalSetup()
    {
        LocalPlayerSetupSession.Clear();
        ShowMainWindow();
    }

    private void StartLocalGame()
    {
        if (!LocalPlayerSetupSession.IsReady)
            return;

        LocalPlayerSetupSession.Confirm();
        SceneManager.LoadScene(GameplaySceneName);
    }

    private void StartAiGame()
    {
        StartSinglePlayerGame(false);
    }

    private void StartTutorial()
    {
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
        _networkManager.FindMatch();
    }

    private void CancelMatchmaking()
    {
        _networkManager?.CancelMatchmaking();
        OpenMultiplayer();
    }

    private void ShowMainWindow()
    {
        _isLocalSetupOpen = false;
        ShowOnly(_mainWindow);
    }

    private void ShowOnly(GameObject window)
    {
        _mainWindow.SetActive(window == _mainWindow);
        _localWindow.SetActive(window == _localWindow);
        _multiplayerWindow.SetActive(window == _multiplayerWindow);
        _matchmakingWindow.SetActive(window == _matchmakingWindow);
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
        _networkManager.ReturnedToMenu -= OpenMultiplayer;
        _networkManager.MatchmakingStatusChanged += SetMatchmakingStatus;
        _networkManager.MatchLoading += HideMenuWindows;
        _networkManager.ReturnedToMenu += OpenMultiplayer;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (_networkManager == null)
            return;

        _networkManager.MatchmakingStatusChanged -= SetMatchmakingStatus;
        _networkManager.MatchLoading -= HideMenuWindows;
        _networkManager.ReturnedToMenu -= OpenMultiplayer;
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

    private Transform FindDirectChild(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
            throw new MissingReferenceException($"Menu window '{childName}' is missing under {name}.");
        return child;
    }

    private static Button FindButton(Transform root, string buttonName)
    {
        Transform target = FindDescendant(root, buttonName);
        Button button = target != null ? target.GetComponent<Button>() : null;
        if (button == null)
            throw new MissingReferenceException($"Button '{buttonName}' is missing under {root.name}.");
        return button;
    }

    private static Button FindOptionalButton(Transform root, string buttonName)
    {
        Transform target = FindDescendant(root, buttonName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
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

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
