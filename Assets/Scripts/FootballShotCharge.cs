using UnityEngine;
using UnityEngine.UI;

public enum FootballShotChargeAction
{
    Kick,
    Header
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup), typeof(Image))]
public sealed class FootballShotCharge : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _fillImage;
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.12f;
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.1f;

    [Header("Charge")]
    [SerializeField, Min(0f)] private float _chargeDelay = 0.15f;
    [SerializeField, Min(0.01f)] private float _chargeDuration = 1.2f;
    [SerializeField, Min(0f)] private float _fixedPowerMultiplier = 1f;
    [SerializeField, Min(0f)] private float _maximumPowerMultiplier = 1.8f;
    [SerializeField] private AnimationCurve _powerCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private FootballShotChargeAction _activeAction;
    private float _chargeStartedAt;
    private bool _isCharging;
    private bool _resetFillWhenHidden;

    public bool IsCharging => _isCharging;
    public float CurrentCharge => _isCharging ? CalculateCharge(Time.time) : 0f;

    private void Awake()
    {
        ResolveReferences();
        ConfigureFillImage();
        HideImmediately();
    }

    private void Reset()
    {
        ResolveReferences();
        ConfigureFillImage();
        HideImmediately();
    }

    private void OnValidate()
    {
        _chargeDuration = Mathf.Max(0.01f, _chargeDuration);
        _fixedPowerMultiplier = Mathf.Max(0f, _fixedPowerMultiplier);
        _maximumPowerMultiplier = Mathf.Max(_fixedPowerMultiplier, _maximumPowerMultiplier);
    }

    private void OnDisable()
    {
        _isCharging = false;
        HideImmediately();
    }

    private void Update()
    {
        if (_canvasGroup == null || _fillImage == null)
            return;

        float heldTime = _isCharging ? Mathf.Max(0f, Time.time - _chargeStartedAt) : 0f;
        bool shouldBeVisible = _isCharging && heldTime >= _chargeDelay;
        float targetAlpha = shouldBeVisible ? 1f : 0f;
        float fadeDuration = shouldBeVisible ? _fadeInDuration : _fadeOutDuration;

        _canvasGroup.alpha = fadeDuration <= 0f
            ? targetAlpha
            : Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.deltaTime / fadeDuration);

        if (_isCharging)
            _fillImage.fillAmount = CalculateCharge(Time.time);
        else if (_resetFillWhenHidden && _canvasGroup.alpha <= 0f)
        {
            _fillImage.fillAmount = 0f;
            _resetFillWhenHidden = false;
        }
    }

    public void BeginCharge(FootballShotChargeAction action)
    {
        _activeAction = action;
        _chargeStartedAt = Time.time;
        _isCharging = true;
        _resetFillWhenHidden = false;

        if (_fillImage != null)
            _fillImage.fillAmount = 0f;
    }

    public bool TryReleaseCharge(FootballShotChargeAction action, out float normalizedCharge)
    {
        normalizedCharge = 0f;

        if (!_isCharging || _activeAction != action)
            return false;

        normalizedCharge = CalculateCharge(Time.time);
        _isCharging = false;
        _resetFillWhenHidden = true;
        return true;
    }

    public void CancelCharge(FootballShotChargeAction action)
    {
        if (!_isCharging || _activeAction != action)
            return;

        CancelCharge();
    }

    public void CancelCharge()
    {
        _isCharging = false;
        _resetFillWhenHidden = true;
    }

    public float EvaluatePowerMultiplier(float normalizedCharge)
    {
        return Mathf.Lerp(
            _fixedPowerMultiplier,
            _maximumPowerMultiplier,
            EvaluateNormalizedCharge(normalizedCharge)
        );
    }

    public float EvaluateNormalizedCharge(float normalizedCharge)
    {
        float t = Mathf.Clamp01(normalizedCharge);
        return _powerCurve != null ? Mathf.Clamp01(_powerCurve.Evaluate(t)) : t;
    }

    private float CalculateCharge(float currentTime)
    {
        float chargeTime = currentTime - _chargeStartedAt - _chargeDelay;

        if (chargeTime <= 0f)
            return 0f;

        return Mathf.Clamp01(chargeTime / Mathf.Max(0.01f, _chargeDuration));
    }

    private void ResolveReferences()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_fillImage == null)
            _fillImage = GetComponent<Image>();
    }

    private void ConfigureFillImage()
    {
        if (_fillImage == null)
            return;

        _fillImage.type = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Horizontal;
        _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fillImage.fillClockwise = true;
    }

    private void HideImmediately()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_fillImage != null)
            _fillImage.fillAmount = 0f;

        _resetFillWhenHidden = false;
    }
}
