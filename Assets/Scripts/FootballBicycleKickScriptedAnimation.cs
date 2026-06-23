using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
[RequireComponent(typeof(FootballBallBicycleKicker))]
public sealed class FootballBicycleKickScriptedAnimation : MonoBehaviour
{
    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private FootballBallBicycleKicker _bicycleKicker;
    [SerializeField] private FootballPlayerAnimator _playerAnimator;
    [SerializeField] private Transform _rotationRoot;
    [SerializeField] private bool _playKickAnimation = true;
    [SerializeField, Min(0.01f)] private float _duration = 0.45f;
    [SerializeField] private AnimationCurve _spinCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float _rotationDegrees = 360f;
    [SerializeField] private bool _spinOppositeFacing = true;

    private Coroutine _spinCoroutine;
    private Quaternion _restoreRotation;
    private bool _isSpinning;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (_bicycleKicker != null)
            _bicycleKicker.BicycleKicked += OnBicycleKicked;
    }

    private void OnDisable()
    {
        if (_bicycleKicker != null)
            _bicycleKicker.BicycleKicked -= OnBicycleKicked;

        StopSpin();
    }

    private void OnBicycleKicked()
    {
        ResolveReferences();

        if (_playKickAnimation && _playerAnimator != null)
            _playerAnimator.TriggerKickAnimation();

        if (_rotationRoot == null)
            return;

        StopSpin();
        _spinCoroutine = StartCoroutine(Spin());
    }

    private IEnumerator Spin()
    {
        _isSpinning = true;
        _restoreRotation = _rotationRoot.localRotation;

        float elapsed = 0f;
        int facingDirection = _controller != null ? _controller.FacingDirection : 1;
        float direction = NormalizeFacingDirection(facingDirection);

        if (_spinOppositeFacing)
            direction = -direction;

        float targetDegrees = _rotationDegrees * direction;

        while (elapsed < _duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / _duration);
            float progress = _spinCurve != null ? _spinCurve.Evaluate(normalizedTime) : normalizedTime;
            float angle = Mathf.Lerp(0f, targetDegrees, progress);

            _rotationRoot.localRotation = _restoreRotation * Quaternion.AngleAxis(angle, Vector3.forward);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _rotationRoot.localRotation = _restoreRotation;
        _isSpinning = false;
        _spinCoroutine = null;
    }

    private void StopSpin()
    {
        if (_spinCoroutine != null)
        {
            StopCoroutine(_spinCoroutine);
            _spinCoroutine = null;
        }

        if (_isSpinning && _rotationRoot != null)
            _rotationRoot.localRotation = _restoreRotation;

        _isSpinning = false;
    }

    private void ResolveReferences()
    {
        if (_controller == null)
            _controller = GetComponent<FootballPlayerController>();

        if (_bicycleKicker == null)
            _bicycleKicker = GetComponent<FootballBallBicycleKicker>();

        if (_playerAnimator == null)
            _playerAnimator = GetComponent<FootballPlayerAnimator>();
    }

    private static int NormalizeFacingDirection(int facingDirection)
    {
        return facingDirection < 0 ? -1 : 1;
    }
}
