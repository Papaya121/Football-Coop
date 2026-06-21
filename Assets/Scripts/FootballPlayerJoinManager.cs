using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DefaultExecutionOrder(-100)]
public sealed class FootballPlayerJoinManager : MonoBehaviour
{
    private const float GamepadStickJoinThreshold = 0.5f;

    private readonly FootballPlayerController[] _players = new FootballPlayerController[2];
    private readonly FootballPlayerControlSource[] _assignedSources = new FootballPlayerControlSource[2];
    private readonly InputDevice[] _assignedDevices = new InputDevice[2];

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
