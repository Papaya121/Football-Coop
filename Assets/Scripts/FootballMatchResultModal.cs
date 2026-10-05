using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mirror;

[DisallowMultipleComponent]
public sealed class FootballMatchResultModal : MonoBehaviour
{
    private const string ResourceName = "MatchResultModal";
    private const string GameplaySceneName = "Gameplay";
    private const string MenuSceneName = "Menu";

    [SerializeField] private TMP_Text _outcomeText;
    [SerializeField] private TMP_Text _ratingText;
    [SerializeField] private Button _continueButton;
    [SerializeField] private GameObject _victoryVisual;
    [SerializeField] private GameObject _drawVisual;
    [SerializeField] private GameObject _defeatVisual;

    private static FootballMatchResultModal _instance;
    private bool _pausedLocalMatch;
    private float _previousTimeScale;
    private Coroutine _focusCoroutine;

    public static bool IsOpen => _instance != null && _instance.isActiveAndEnabled;

    public static void ShowResult(PlayerMatchOutcome outcome, int previousRating, int newRating)
    {
        if (_instance == null)
        {
            FootballMatchResultModal prefab = Resources.Load<FootballMatchResultModal>(ResourceName);
            _instance = prefab != null ? Instantiate(prefab) : CreateDefault();
        }

        _instance.Present(outcome, previousRating, newRating);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        FootballLocalization.BindHierarchy(gameObject);
    }

    private void OnDestroy()
    {
        if (_continueButton != null)
            _continueButton.onClick.RemoveListener(OnContinueClicked);

        if (_pausedLocalMatch)
            Time.timeScale = _previousTimeScale;

        if (_instance == this)
            _instance = null;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RequestButtonFocus();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_focusCoroutine != null)
        {
            StopCoroutine(_focusCoroutine);
            _focusCoroutine = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RequestButtonFocus();
    }

    private void RequestButtonFocus()
    {
        if (_focusCoroutine != null)
            StopCoroutine(_focusCoroutine);

        _focusCoroutine = StartCoroutine(FocusButtonNextFrame());
    }

    private IEnumerator FocusButtonNextFrame()
    {
        yield return null;
        _focusCoroutine = null;

        if (_continueButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
    }

    private void Present(PlayerMatchOutcome outcome, int previousRating, int newRating)
    {
        Canvas canvas = GetComponent<Canvas>();

        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (_outcomeText != null)
            FootballLocalization.SetText(_outcomeText, outcome switch
            {
                PlayerMatchOutcome.Victory => "Победа",
                PlayerMatchOutcome.Draw => "Ничья",
                _ => "Поражение"
            });

        if (_ratingText != null)
            FootballLocalization.SetText(_ratingText, "Рейтинг: {0} → {1}", previousRating, newRating);

        if (_victoryVisual != null)
            _victoryVisual.SetActive(outcome == PlayerMatchOutcome.Victory);

        if (_drawVisual != null)
            _drawVisual.SetActive(outcome == PlayerMatchOutcome.Draw);

        if (_defeatVisual != null)
            _defeatVisual.SetActive(outcome == PlayerMatchOutcome.Defeat);

        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveListener(OnContinueClicked);
            _continueButton.onClick.AddListener(OnContinueClicked);
        }

        gameObject.SetActive(true);

        if (!_pausedLocalMatch && LocalPlayerSetupSession.IsOnlineFallbackAiMatch &&
            SceneManager.GetSceneByName(GameplaySceneName).isLoaded && !NetworkClient.active)
        {
            _previousTimeScale = Time.timeScale;
            _pausedLocalMatch = true;
            Time.timeScale = 0f;
        }
    }

    private void OnContinueClicked()
    {
        bool gameplayLoaded = SceneManager.GetSceneByName(GameplaySceneName).isLoaded;
        bool localFallback = LocalPlayerSetupSession.IsOnlineFallbackAiMatch;
        Destroy(gameObject);

        if (!gameplayLoaded)
            return;

        if (NetworkClient.active)
            FootballNetworkManager.Instance?.RequestMatchExit(true);
        else if (localFallback)
        {
            Time.timeScale = 1f;
            LocalPlayerSetupSession.Clear();
            SceneManager.LoadScene(MenuSceneName);
        }
    }

    private static FootballMatchResultModal CreateDefault()
    {
        GameObject root = new GameObject("Match Result Modal", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        FootballMatchResultModal modal = root.AddComponent<FootballMatchResultModal>();
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform backdrop = CreateRect("Backdrop", root.transform);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;
        backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        RectTransform panel = CreateRect("Panel", backdrop);
        SetCenteredRect(panel, Vector2.zero, new Vector2(640f, 320f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.13f, 0.2f, 1f);

        modal._outcomeText = CreateText("Outcome Text", panel, new Vector2(0f, 92f),
            new Vector2(560f, 80f), 50f);
        modal._ratingText = CreateText("Rating Text", panel, new Vector2(0f, 13f),
            new Vector2(560f, 65f), 34f);

        RectTransform buttonRect = CreateRect("Continue Button", panel);
        SetCenteredRect(buttonRect, new Vector2(0f, -93f), new Vector2(250f, 64f));
        buttonRect.gameObject.AddComponent<Image>().color = new Color(0.24f, 0.62f, 0.7f, 1f);
        modal._continueButton = buttonRect.gameObject.AddComponent<Button>();
        TMP_Text buttonText = CreateText("Button Text", buttonRect, Vector2.zero,
            new Vector2(240f, 60f), 28f);
        FootballLocalization.SetText(buttonText, "Продолжить");

        return modal;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject element = new GameObject(name, typeof(RectTransform));
        element.transform.SetParent(parent, false);
        return (RectTransform)element.transform;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static TMP_Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = CreateRect(name, parent);
        SetCenteredRect(rect, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
