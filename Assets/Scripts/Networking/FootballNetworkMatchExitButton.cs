using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class FootballNetworkMatchExitButton : MonoBehaviour
{
    private const string MenuSceneName = "Menu";

    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private string _giveUpText = "Сдаться";
    [SerializeField] private string _exitText = "Выйти";

    private bool _matchFinished;
    private FootballMatchController _localMatchController;

    private void Awake()
    {
        ResolveReferences();
        _button.onClick.AddListener(OnClicked);
        ShowGiveUp();
    }

    private void Start()
    {
        if (LocalPlayerSetupSession.IsTutorial)
        {
            _matchFinished = true;
            SetLabel(_exitText);
            return;
        }

        if (NetworkClient.active)
            return;

        _localMatchController = FindAnyObjectByType<FootballMatchController>();
        if (_localMatchController == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _localMatchController.MatchFinished += OnLocalMatchFinished;
        _matchFinished = _localMatchController.State == FootballMatchState.Finished;
        RefreshLocalLabel();
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);

        if (_localMatchController != null)
            _localMatchController.MatchFinished -= OnLocalMatchFinished;
    }

    public void ApplyMatchState(FootballMatchState state)
    {
        _matchFinished = state == FootballMatchState.Finished;
        SetLabel(_matchFinished ? _exitText : _giveUpText);

        if (_button != null)
            _button.interactable = true;
    }

#if UNITY_EDITOR
    public void EditorConfigure(Button button, TMP_Text label)
    {
        _button = button;
        _label = label;
    }
#endif

    private void OnClicked()
    {
        if (_button != null)
            _button.interactable = false;

        if (LocalPlayerSetupSession.IsTutorial)
        {
            FootballTutorialController tutorial = FindAnyObjectByType<FootballTutorialController>(FindObjectsInactive.Include);
            if (tutorial != null)
            {
                tutorial.ExitToMenu();
                return;
            }

            Time.timeScale = 1f;
            LocalPlayerSetupSession.Clear();
            SceneManager.LoadScene(MenuSceneName);
            return;
        }

        if (NetworkClient.active)
        {
            FootballNetworkManager.Instance?.RequestMatchExit(_matchFinished);
            return;
        }

        if (LocalPlayerSetupSession.IsAiMatch && !_matchFinished)
            _localMatchController?.TryForfeitLocalPlayer();

        Time.timeScale = 1f;
        LocalPlayerSetupSession.Clear();
        SceneManager.LoadScene(MenuSceneName);
    }

    private void ShowGiveUp()
    {
        _matchFinished = false;
        SetLabel(_giveUpText);
    }

    private void OnLocalMatchFinished()
    {
        _matchFinished = true;
        RefreshLocalLabel();
    }

    private void RefreshLocalLabel()
    {
        bool canGiveUp =
            LocalPlayerSetupSession.IsAiMatch && !_matchFinished;
        SetLabel(canGiveUp ? _giveUpText : _exitText);

        if (_button != null)
            _button.interactable = true;
    }

    private void SetLabel(string value)
    {
        if (_label != null)
            FootballLocalization.SetText(_label, value);
    }

    private void ResolveReferences()
    {
        if (_button == null)
            _button = GetComponent<Button>();
        if (_label == null)
            _label = GetComponentInChildren<TMP_Text>(true);
    }
}
