using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class JuicyButtonTween : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler,
    ISubmitHandler
{
    [Header("Lift")]
    [SerializeField, Min(1f)] private float _hoverScale = 1.055f;
    [SerializeField, Range(0.8f, 1f)] private float _pressedScale = 0.94f;
    [SerializeField, Min(0.01f)] private float _hoverDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float _pressDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float _releaseDuration = 0.22f;

    [Header("Magnetic tilt")]
    [SerializeField, Range(0f, 15f)] private float _maxTilt = 8f;
    [SerializeField, Range(0f, 5f)] private float _maxTwist = 1.5f;
    [SerializeField, Range(0f, 20f)] private float _contentParallax = 8f;
    [SerializeField, Min(0.01f)] private float _tiltDuration = 0.12f;

    [Header("Timing")]
    [SerializeField] private bool _useUnscaledTime = true;

    private Button _button;
    private RectTransform _rectTransform;
    private Vector3 _restScale;
    private Quaternion _restRotation;
    private Tween _scaleTween;
    private Tween _rotationTween;
    private RectTransform[] _content;
    private Vector2[] _contentRestPositions;
    private Tween[] _contentTweens;
    private bool _pointerInside;
    private bool _pointerDown;
    private bool _selected;

    private bool CanAnimate => isActiveAndEnabled && _button != null && _button.IsInteractable();

    private void Awake()
    {
        _button = GetComponent<Button>();
        _rectTransform = (RectTransform)transform;
        _restScale = _rectTransform.localScale;
        _restRotation = _rectTransform.localRotation;
        CacheContent();
    }

    private void OnDisable()
    {
        KillTweens();

        if (_rectTransform == null)
            return;

        _rectTransform.localScale = _restScale;
        _rectTransform.localRotation = _restRotation;

        for (int i = 0; i < _content.Length; i++)
            _content[i].anchoredPosition = _contentRestPositions[i];

        _pointerInside = false;
        _pointerDown = false;
        _selected = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;

        if (CanAnimate && !_pointerDown)
            AnimateHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        _pointerDown = false;

        if (!CanAnimate)
            return;

        if (_selected)
        {
            TweenRotation(_restRotation, _releaseDuration, Ease.OutCubic);
            TweenContent(Vector2.zero, _releaseDuration);
        }
        else
            AnimateRest();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!CanAnimate || !_pointerInside || _pointerDown)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            eventData.position,
            eventData.enterEventCamera,
            out Vector2 localPoint);

        Rect rect = _rectTransform.rect;
        float normalizedX = Mathf.Clamp(localPoint.x / Mathf.Max(1f, rect.width * 0.5f), -1f, 1f);
        float normalizedY = Mathf.Clamp(localPoint.y / Mathf.Max(1f, rect.height * 0.5f), -1f, 1f);
        Vector3 tilt = new(
            -normalizedY * _maxTilt,
            normalizedX * _maxTilt,
            -normalizedX * _maxTwist);

        TweenRotation(_restRotation * Quaternion.Euler(tilt), _tiltDuration, Ease.OutQuad);
        TweenContent(new Vector2(normalizedX, normalizedY) * _contentParallax, _tiltDuration);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanAnimate || eventData.button != PointerEventData.InputButton.Left)
            return;

        _pointerDown = true;
        TweenScale(_restScale * _pressedScale, _pressDuration, Ease.OutCubic);
        TweenRotation(_restRotation, _pressDuration, Ease.OutCubic);
        TweenContent(Vector2.zero, _pressDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _pointerDown = false;

        if (!CanAnimate)
            return;

        if (_pointerInside || _selected)
            AnimateRelease();
        else
            AnimateRest();
    }

    public void OnSelect(BaseEventData eventData)
    {
        // A pointer click also selects a Unity Button. Only keep the lifted state
        // for navigation selection, otherwise a clicked button would stay hovered.
        _selected = eventData is not PointerEventData;

        if (_selected && CanAnimate && !_pointerDown)
            AnimateHover();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;

        if (CanAnimate && !_pointerInside)
            AnimateRest();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (CanAnimate)
            AnimateSubmit();
    }

    private void AnimateHover()
    {
        TweenScale(_restScale * _hoverScale, _hoverDuration, Ease.OutBack);
    }

    private void AnimateRest()
    {
        TweenScale(_restScale, _releaseDuration, Ease.OutBack);
        TweenRotation(_restRotation, _releaseDuration, Ease.OutCubic);
        TweenContent(Vector2.zero, _releaseDuration);
    }

    private void AnimateRelease()
    {
        _scaleTween?.Kill();
        _scaleTween = DOTween.Sequence()
            .Append(_rectTransform.DOScale(_restScale * (_hoverScale + 0.025f), _releaseDuration * 0.45f)
                .SetEase(Ease.OutCubic))
            .Append(_rectTransform.DOScale(_restScale * _hoverScale, _releaseDuration * 0.55f)
                .SetEase(Ease.OutBack))
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void AnimateSubmit()
    {
        _scaleTween?.Kill();
        float targetScale = _pointerInside || _selected ? _hoverScale : 1f;
        _scaleTween = DOTween.Sequence()
            .Append(_rectTransform.DOScale(_restScale * _pressedScale, _pressDuration)
                .SetEase(Ease.OutCubic))
            .Append(_rectTransform.DOScale(_restScale * (targetScale + 0.025f), _releaseDuration * 0.45f)
                .SetEase(Ease.OutCubic))
            .Append(_rectTransform.DOScale(_restScale * targetScale, _releaseDuration * 0.55f)
                .SetEase(Ease.OutBack))
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void TweenScale(Vector3 target, float duration, Ease ease)
    {
        _scaleTween?.Kill();
        _scaleTween = _rectTransform.DOScale(target, duration)
            .SetEase(ease)
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void TweenRotation(Quaternion target, float duration, Ease ease)
    {
        _rotationTween?.Kill();
        _rotationTween = _rectTransform.DOLocalRotateQuaternion(target, duration)
            .SetEase(ease)
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void TweenContent(Vector2 offset, float duration)
    {
        for (int i = 0; i < _content.Length; i++)
        {
            _contentTweens[i]?.Kill();
            _contentTweens[i] = _content[i]
                .DOAnchorPos(_contentRestPositions[i] + offset, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    private void CacheContent()
    {
        int childCount = _rectTransform.childCount;
        _content = new RectTransform[childCount];
        _contentRestPositions = new Vector2[childCount];
        _contentTweens = new Tween[childCount];

        for (int i = 0; i < childCount; i++)
        {
            _content[i] = (RectTransform)_rectTransform.GetChild(i);
            _contentRestPositions[i] = _content[i].anchoredPosition;
        }
    }

    private void KillTweens()
    {
        _scaleTween?.Kill();
        _rotationTween?.Kill();

        for (int i = 0; i < _contentTweens.Length; i++)
        {
            _contentTweens[i]?.Kill();
            _contentTweens[i] = null;
        }

        _scaleTween = null;
        _rotationTween = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _hoverScale = Mathf.Max(1f, _hoverScale);
        _pressedScale = Mathf.Clamp(_pressedScale, 0.8f, 1f);
        _hoverDuration = Mathf.Max(0.01f, _hoverDuration);
        _pressDuration = Mathf.Max(0.01f, _pressDuration);
        _releaseDuration = Mathf.Max(0.01f, _releaseDuration);
        _tiltDuration = Mathf.Max(0.01f, _tiltDuration);
    }
#endif
}
