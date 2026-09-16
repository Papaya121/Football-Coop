using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
[RequireComponent(typeof(FootballBallKicker))]
[RequireComponent(typeof(FootballBallBicycleKicker))]
public sealed class FootballBallKickInput : MonoBehaviour
{
    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private FootballBallKicker _kicker;
    [SerializeField] private FootballBallBicycleKicker _bicycleKicker;
    [SerializeField] private FootballShotCharge _shotCharge;

    private FootballInput _input;

    private void Awake()
    {
        ResolveReferences();
        EnsureInput();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureInput();

        if (_controller != null)
        {
            _controller.InputAssigned += OnInputAssigned;
            ApplyInputRestrictions(_controller.ControlSource, _controller.ControlDevice);
        }

        _input.Ball.Kick.started += OnKick;
        _input.Ball.Kick.performed += OnKick;
        _input.Ball.Kick.canceled += OnKick;
        _input.Ball.Enable();
    }

    private void OnDisable()
    {
        if (_controller != null)
            _controller.InputAssigned -= OnInputAssigned;

        if (_input == null)
            return;

        _input.Ball.Kick.started -= OnKick;
        _input.Ball.Kick.performed -= OnKick;
        _input.Ball.Kick.canceled -= OnKick;
        _shotCharge?.CancelCharge(FootballShotChargeAction.Kick);
        _input.Ball.Disable();
    }

    private void OnDestroy()
    {
        _input?.Dispose();
    }

    private void OnKick(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _shotCharge?.BeginCharge(FootballShotChargeAction.Kick);
            return;
        }

        if (context.canceled)
        {
            _shotCharge?.CancelCharge(FootballShotChargeAction.Kick);
            return;
        }

        if (!context.performed || _kicker == null)
            return;

        float normalizedCharge = 0f;
        float powerMultiplier = 1f;

        if (_shotCharge != null)
        {
            if (!_shotCharge.TryReleaseCharge(FootballShotChargeAction.Kick, out normalizedCharge))
                return;

            powerMultiplier = _kicker.ChargeControlsHeight
                ? _shotCharge.EvaluatePowerMultiplier(0f)
                : _shotCharge.EvaluatePowerMultiplier(normalizedCharge);
            normalizedCharge = _shotCharge.EvaluateNormalizedCharge(normalizedCharge);
        }

        bool isLob = !_kicker.ChargeControlsHeight &&
            _controller != null &&
            FootballBallKicker.IsLobDirection(_controller.MoveInput);

        if (!isLob && _bicycleKicker != null && _bicycleKicker.CanAttemptBicycleKick())
        {
            _bicycleKicker.TryBicycleKick(powerMultiplier);
            return;
        }

        _kicker.TryKick(normalizedCharge, isLob);
    }

    private void OnInputAssigned(FootballPlayerControlSource source, InputDevice device)
    {
        EnsureInput();
        ApplyInputRestrictions(source, device);
    }

    private void ResolveReferences()
    {
        if (_controller == null)
            _controller = GetComponent<FootballPlayerController>();

        if (_kicker == null)
            _kicker = GetComponent<FootballBallKicker>();

        if (_bicycleKicker == null)
            _bicycleKicker = GetComponent<FootballBallBicycleKicker>();

        if (_shotCharge == null)
            _shotCharge = GetComponentInChildren<FootballShotCharge>(true);
    }

    private void EnsureInput()
    {
        if (_input != null)
            return;

        _input = new FootballInput();
        _input.Ball.Kick.wantsInitialStateCheck = true;
    }

    private void ApplyInputRestrictions(FootballPlayerControlSource source, InputDevice device)
    {
        bool wasEnabled = _input.Ball.enabled;

        if (wasEnabled)
            _input.Ball.Disable();

        _input.devices = device != null ? new[] { device } : null;
        _input.bindingMask = FootballInputBindingMasks.FromControlSource(source);

        if (wasEnabled)
            _input.Ball.Enable();
    }
}
