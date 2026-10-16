using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class FootballControlsHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text _leftInput;
    [SerializeField] private TMP_Text _rightInput;
    private FootballPlayerJoinManager _joinManager;

    private void Awake()
    {
        _joinManager = FindAnyObjectByType<FootballPlayerJoinManager>();
    }

    private void LateUpdate()
    {
        bool mobile = FootballInputPlatform.IsMobile;
        bool online = NetworkClient.active || NetworkServer.active;
        Refresh(_leftInput, 0, false, mobile, online);
        Refresh(_rightInput, 1, true, mobile, online);
    }

    private void Refresh(TMP_Text label, int index, bool reverse, bool mobile, bool online)
    {
        if (label == null)
            return;

        FootballPlayerController player = _joinManager != null ? _joinManager.GetPlayer(index) : null;
        bool visible = !mobile && (online
            ? index == 0 && NetworkClient.localPlayer != null
            : player != null && player.gameObject.activeInHierarchy &&
                player.GetComponent<FootballBotBrain>() == null);
        if (label.gameObject.activeSelf != visible)
            label.gameObject.SetActive(visible);
        if (!visible)
            return;

        FootballPlayerControlSource source = player != null ? player.ControlSource : FootballPlayerControlSource.WasdKeyboard;
        InputDevice device = player != null ? player.ControlDevice : Keyboard.current;
        if (online)
        {
            // Network input accepts all devices; follow the most recently used gamepad/keyboard.
            Gamepad gamepad = Gamepad.current;
            Keyboard keyboard = Keyboard.current;
            bool useGamepad = gamepad != null && (keyboard == null || gamepad.lastUpdateTime > keyboard.lastUpdateTime);
            source = useGamepad ? FootballPlayerControlSource.Gamepad : FootballPlayerControlSource.WasdKeyboard;
            device = useGamepad ? (InputDevice)gamepad : keyboard;
        }

        string kick;
        string header;
        if (source == FootballPlayerControlSource.Gamepad && device is Gamepad pad)
        {
            kick = pad.buttonEast.displayName;
            header = pad.buttonNorth.displayName;
        }
        else
        {
            Keyboard keyboard = device as Keyboard ?? Keyboard.current;
            bool arrows = source == FootballPlayerControlSource.ArrowKeyboard;
            kick = keyboard != null ? (arrows ? keyboard.leftBracketKey : keyboard.kKey).displayName : arrows ? "[" : "K";
            header = keyboard != null ? (arrows ? keyboard.rightBracketKey : keyboard.jKey).displayName : arrows ? "]" : "J";
        }

        string kickAction = FootballLocalization.Localize("Удар ногой");
        string headerAction = FootballLocalization.Localize("Удар головой");
        string text = reverse
            ? $"{kickAction} – {kick}\n{headerAction} – {header}"
            : $"{kick} – {kickAction}\n{header} – {headerAction}";
        if (label.text != text)
            label.text = text;
    }
}
