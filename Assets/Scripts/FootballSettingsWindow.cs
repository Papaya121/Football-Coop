using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FootballSettingsWindow : MonoBehaviour
{
    [SerializeField] private Slider _volumeSlider;
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private TMP_Text _nicknameCounter;
    [SerializeField, Range(1, PlayerNickname.MaxLength)] private int _nicknameLimit = 14;
    [SerializeField] private Button _englishButton;
    [SerializeField] private Button _russianButton;
    [SerializeField] private CanvasGroup _englishGroup;
    [SerializeField] private CanvasGroup _russianGroup;
    [SerializeField, Range(0f, 1f)] private float _unselectedLanguageAlpha = 0.343f;

    private PlayerProfileService _profileService;
    private Color _counterColor;

    public Selectable PreferredSelection => _volumeSlider;

    private void Awake()
    {
        _counterColor = _nicknameCounter.color;
        _volumeSlider.minValue = 0f;
        _volumeSlider.maxValue = 1f;
        _volumeSlider.wholeNumbers = false;
        // TMP counts UTF-16 code units; enforce the shared text-element limit ourselves.
        _nicknameInput.characterLimit = 0;
        _nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        _nicknameInput.richText = false;
    }

    private void OnEnable()
    {
        _profileService = LocalPlayerProfile.Service;
        _volumeSlider.SetValueWithoutNotify(FootballAudioSettings.MasterVolume);
        RefreshNickname(_profileService.Profile);
        RefreshLanguage();
        _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        _nicknameInput.onValueChanged.AddListener(OnNicknameEdited);
        _nicknameInput.onEndEdit.AddListener(OnNicknameEndEdit);
        _englishButton.onClick.AddListener(SelectEnglish);
        _russianButton.onClick.AddListener(SelectRussian);
        _profileService.ProfileChanged += RefreshNickname;
        FootballLocalization.LanguageChanged += RefreshLanguage;
    }

    private void OnDisable()
    {
        CommitNickname();
        _volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        _nicknameInput.onValueChanged.RemoveListener(OnNicknameEdited);
        _nicknameInput.onEndEdit.RemoveListener(OnNicknameEndEdit);
        _englishButton.onClick.RemoveListener(SelectEnglish);
        _russianButton.onClick.RemoveListener(SelectRussian);
        if (_profileService != null)
            _profileService.ProfileChanged -= RefreshNickname;
        FootballLocalization.LanguageChanged -= RefreshLanguage;
    }

    public void CommitNickname()
    {
        if (_profileService == null)
            return;

        _profileService.TrySetNickname(_nicknameInput.text);
        _nicknameInput.SetTextWithoutNotify(_profileService.Profile.Nickname);
        RefreshCounter(_profileService.Profile.Nickname);
    }

    private void OnNicknameEndEdit(string _) => CommitNickname();

    private void OnVolumeChanged(float volume)
    {
        if (!FootballAudioSettings.SetMasterVolume(volume))
            _volumeSlider.SetValueWithoutNotify(FootballAudioSettings.MasterVolume);
    }

    private void RefreshNickname(PlayerProfile profile)
    {
        // Rating updates must not replace a nickname currently being typed.
        if (_nicknameInput.isFocused)
            return;
        _nicknameInput.SetTextWithoutNotify(profile.Nickname);
        RefreshCounter(profile.Nickname);
    }

    private void OnNicknameEdited(string nickname)
    {
        var text = new StringInfo(nickname);
        if (text.LengthInTextElements > _nicknameLimit)
        {
            nickname = text.SubstringByTextElements(0, _nicknameLimit);
            _nicknameInput.SetTextWithoutNotify(nickname);
        }
        RefreshCounter(nickname);
    }

    private void RefreshCounter(string nickname)
    {
        _nicknameCounter.text = $"{new StringInfo(nickname).LengthInTextElements}/{_nicknameLimit}";
        _nicknameCounter.color = PlayerNickname.TryNormalize(nickname, out _) ? _counterColor : Color.red;
    }

    private void SelectEnglish() => FootballLocalization.SetLanguage("en");
    private void SelectRussian() => FootballLocalization.SetLanguage("ru");

    private void RefreshLanguage()
    {
        _englishGroup.alpha = FootballLocalization.Language == "en" ? 1f : _unselectedLanguageAlpha;
        _russianGroup.alpha = FootballLocalization.Language == "ru" ? 1f : _unselectedLanguageAlpha;
        // Dimmed choices remain clickable so the player can switch languages.
    }
}
