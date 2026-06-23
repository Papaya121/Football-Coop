using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class FootballPlayerJoinManager : MonoBehaviour
{
    private const float GamepadStickJoinThreshold = 0.5f;

    private readonly FootballPlayerController[] _players = new FootballPlayerController[2];
    private readonly FootballPlayerControlSource[] _assignedSources = new FootballPlayerControlSource[2];
    private readonly InputDevice[] _assignedDevices = new InputDevice[2];

    private InputAction _restartAction;
    private int _assignedPlayerCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<FootballPlayerJoinManager>() != null)
            return;

        new GameObject(nameof(FootballPlayerJoinManager)).AddComponent<FootballPlayerJoinManager>();
    }

    private void Awake()
    {
        _restartAction = new InputAction("Restart", InputActionType.Button, "<Keyboard>/r");
        _restartAction.performed += OnRestart;

        _players[0] = FindScenePlayer("Player_L");
        _players[1] = FindScenePlayer("Player_R");

        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i] == null)
                Debug.LogWarning($"Missing player slot {i + 1}. Expected Player_L and Player_R in the scene.", this);
            else
                HidePlayer(_players[i]);
        }
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
        if (_assignedPlayerCount >= _players.Length)
            return;

        TryJoinKeyboard();
        TryJoinGamepads();
    }

    private static FootballPlayerController FindScenePlayer(string playerName)
    {
        foreach (FootballPlayerController player in Resources.FindObjectsOfTypeAll<FootballPlayerController>())
        {
            if (!player.gameObject.scene.IsValid())
                continue;

            if (player.gameObject.name == playerName)
                return player;
        }

        return null;
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
