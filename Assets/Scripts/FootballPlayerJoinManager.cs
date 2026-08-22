using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class FootballPlayerJoinManager : MonoBehaviour
{
    private const int PlayerCapacity = 2;
    private const int AiHumanPlayerIndex = 0;
    private const float GamepadStickJoinThreshold = 0.5f;

    [SerializeField] private FootballPlayerController[] _players = new FootballPlayerController[PlayerCapacity];

    private readonly FootballPlayerControlSource[] _assignedSources = new FootballPlayerControlSource[PlayerCapacity];
    private readonly InputDevice[] _assignedDevices = new InputDevice[PlayerCapacity];

    private InputAction _restartAction;
    private int _assignedPlayerCount;

    public event Action<int> PlayerCountChanged;

    public int AssignedPlayerCount => _assignedPlayerCount;
    public int RequiredPlayerCount => _players.Length;
    public bool HasRequiredPlayers => _assignedPlayerCount >= RequiredPlayerCount;

    public FootballPlayerController GetPlayer(int index)
    {
        return index >= 0 && index < _players.Length ? _players[index] : null;
    }

    private void Awake()
    {
        _restartAction = new InputAction("Restart", InputActionType.Button, "<Keyboard>/r");
        _restartAction.performed += OnRestart;

        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i] == null)
                Debug.LogWarning($"Missing player slot {i + 1} in {nameof(FootballPlayerJoinManager)}.", this);
            else
                HidePlayer(_players[i]);
        }

        ApplyConfirmedMenuSetup();
    }

    private void ApplyConfirmedMenuSetup()
    {
        if (!LocalPlayerSetupSession.IsConfirmed)
            return;

        for (int i = 0; i < LocalPlayerSetupSession.PlayerCount; i++)
        {
            if (!LocalPlayerSetupSession.TryGetPlayer(i, out FootballPlayerControlSource source, out InputDevice device))
            {
                Debug.LogWarning("A device selected in the menu is no longer connected. Players can join again.", this);
                LocalPlayerSetupSession.Clear();
                return;
            }

            TryJoin(source, device);
        }

        if (LocalPlayerSetupSession.IsAiMatch)
            TryJoinBot();
    }

    private void OnEnable()
    {
        _restartAction?.Enable();
    }

    private void OnDisable()
    {
        _restartAction?.Disable();
    }

    private void OnDestroy()
    {
        if (_restartAction == null)
            return;

        _restartAction.performed -= OnRestart;
        _restartAction.Dispose();
        _restartAction = null;
    }

    private void Update()
    {
        if (LocalPlayerSetupSession.IsAiMatch)
            TrySwitchAiHumanInput();

        if (_assignedPlayerCount >= _players.Length)
            return;

        TryJoinKeyboard();
        TryJoinGamepads();
    }

    private void TrySwitchAiHumanInput()
    {
        if (_assignedPlayerCount == 0 || _players.Length == 0)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (WasWasdControlUsed(keyboard))
                AssignAiHumanInput(FootballPlayerControlSource.WasdKeyboard, keyboard);

            if (WasArrowControlUsed(keyboard))
                AssignAiHumanInput(FootballPlayerControlSource.ArrowKeyboard, keyboard);
        }

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (WasGamepadPressed(gamepad))
                AssignAiHumanInput(FootballPlayerControlSource.Gamepad, gamepad);
        }
    }

    private void AssignAiHumanInput(FootballPlayerControlSource source, InputDevice device)
    {
        FootballPlayerController player = _players[AiHumanPlayerIndex];

        if (player == null ||
            (_assignedSources[AiHumanPlayerIndex] == source && _assignedDevices[AiHumanPlayerIndex] == device))
            return;

        _assignedSources[AiHumanPlayerIndex] = source;
        _assignedDevices[AiHumanPlayerIndex] = device;
        player.AssignInput(source, device);
    }

    private void OnValidate()
    {
        if (_players == null || _players.Length != PlayerCapacity)
            Array.Resize(ref _players, PlayerCapacity);
    }

    private static void HidePlayer(FootballPlayerController player)
    {
        player.enabled = true;
        player.gameObject.SetActive(false);
    }

    private void TryJoinKeyboard()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (WasWasdPressed(keyboard))
            TryJoin(FootballPlayerControlSource.WasdKeyboard, keyboard);

        if (WasArrowPressed(keyboard))
            TryJoin(FootballPlayerControlSource.ArrowKeyboard, keyboard);
    }

    private void TryJoinGamepads()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (!WasGamepadPressed(gamepad))
                continue;

            TryJoin(FootballPlayerControlSource.Gamepad, gamepad);
        }
    }

    private bool TryJoin(FootballPlayerControlSource source, InputDevice device)
    {
        if (_assignedPlayerCount >= _players.Length || IsSourceAssigned(source, device))
            return false;

        FootballPlayerController player = _players[_assignedPlayerCount];

        if (player == null)
            return false;

        _assignedSources[_assignedPlayerCount] = source;
        _assignedDevices[_assignedPlayerCount] = device;
        _assignedPlayerCount++;

        player.enabled = true;
        player.AssignInput(source, device);
        player.gameObject.SetActive(true);
        PlayerCountChanged?.Invoke(_assignedPlayerCount);

        return true;
    }

    private bool TryJoinBot()
    {
        if (_assignedPlayerCount >= _players.Length)
            return false;

        int botIndex = _players.Length - 1;
        FootballPlayerController botPlayer = _players[botIndex];

        if (botPlayer == null || botIndex == 0)
            return false;

        FootballBallKickInput kickInput = botPlayer.GetComponent<FootballBallKickInput>();
        FootballBallHeaderInput headerInput = botPlayer.GetComponent<FootballBallHeaderInput>();

        if (kickInput != null)
            kickInput.enabled = false;

        if (headerInput != null)
            headerInput.enabled = false;

        FootballBall ball = FindAnyObjectByType<FootballBall>();

        if (ball == null)
        {
            Debug.LogError("Cannot create the local bot because the scene ball is missing.", this);
            return false;
        }

        botPlayer.enabled = true;
        botPlayer.SetExternalControlEnabled(true);
        botPlayer.SetFacingDirection(-1);

        FootballBotBrain brain = botPlayer.GetComponent<FootballBotBrain>();

        if (brain == null)
            brain = botPlayer.gameObject.AddComponent<FootballBotBrain>();

        brain.Configure(FootballTeamSide.Right, ball, _players[0]);
        botPlayer.gameObject.SetActive(true);

        _assignedPlayerCount++;
        PlayerCountChanged?.Invoke(_assignedPlayerCount);
        return true;
    }

    private bool IsSourceAssigned(FootballPlayerControlSource source, InputDevice device)
    {
        for (int i = 0; i < _assignedPlayerCount; i++)
        {
            if (_assignedSources[i] != source)
                continue;

            if (source != FootballPlayerControlSource.Gamepad || _assignedDevices[i] == device)
                return true;
        }

        return false;
    }

    private static bool WasWasdPressed(Keyboard keyboard)
    {
        return keyboard.wKey.wasPressedThisFrame ||
            keyboard.aKey.wasPressedThisFrame ||
            keyboard.sKey.wasPressedThisFrame ||
            keyboard.dKey.wasPressedThisFrame;
    }

    private static bool WasArrowPressed(Keyboard keyboard)
    {
        return keyboard.upArrowKey.wasPressedThisFrame ||
            keyboard.leftArrowKey.wasPressedThisFrame ||
            keyboard.downArrowKey.wasPressedThisFrame ||
            keyboard.rightArrowKey.wasPressedThisFrame;
    }

    private static bool WasWasdControlUsed(Keyboard keyboard)
    {
        return WasWasdPressed(keyboard) ||
            keyboard.spaceKey.wasPressedThisFrame ||
            keyboard.kKey.wasPressedThisFrame ||
            keyboard.jKey.wasPressedThisFrame;
    }

    private static bool WasArrowControlUsed(Keyboard keyboard)
    {
        return WasArrowPressed(keyboard) ||
            keyboard.leftBracketKey.wasPressedThisFrame ||
            keyboard.rightBracketKey.wasPressedThisFrame;
    }

    private void OnRestart(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        RestartCurrentScene();
    }

    private static void RestartCurrentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
        if (activeScene.buildIndex < 0 && !string.IsNullOrEmpty(activeScene.path))
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                activeScene.path,
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            return;
        }
#endif

        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);
    }

    private static bool WasGamepadPressed(Gamepad gamepad)
    {
        foreach (InputControl control in gamepad.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame)
                return true;
        }

        return gamepad.leftStick.ReadValue().sqrMagnitude >= GamepadStickJoinThreshold * GamepadStickJoinThreshold ||
            gamepad.rightStick.ReadValue().sqrMagnitude >= GamepadStickJoinThreshold * GamepadStickJoinThreshold;
    }
}
