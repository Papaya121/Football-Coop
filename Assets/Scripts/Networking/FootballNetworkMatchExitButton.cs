using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class FootballNetworkMatchExitButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private string _giveUpText = "Сдаться";
    [SerializeField] private string _exitText = "Выйти";

    private bool _matchFinished;

    private void Awake()
    {
        ResolveReferences();
        _button.onClick.AddListener(OnClicked);
        ShowGiveUp();
    }

    private void Start()
    {
        if (!NetworkClient.active)
            gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);
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

        FootballNetworkManager.Instance?.RequestMatchExit(_matchFinished);
    }

    private void ShowGiveUp()
    {
        _matchFinished = false;
        SetLabel(_giveUpText);
    }

    private void SetLabel(string value)
    {
        if (_label != null)
            _label.text = value;
    }

    private void ResolveReferences()
    {
        if (_button == null)
            _button = GetComponent<Button>();
        if (_label == null)
            _label = GetComponentInChildren<TMP_Text>(true);
    }
}
