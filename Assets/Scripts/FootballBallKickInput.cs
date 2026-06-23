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

        _input.Ball.Kick.performed += OnKick;
        _input.Ball.Enable();
    }

    private void OnDisable()
    {
        if (_controller != null)
            _controller.InputAssigned -= OnInputAssigned;

        if (_input == null)
            return;

        _input.Ball.Kick.performed -= OnKick;
        _input.Ball.Disable();
    }

    private void OnDestroy()
    {
        _input?.Dispose();
    }

    private void OnKick(InputAction.CallbackContext context)
    {
        if (!context.performed || _kicker == null)
            return;

        if (_bicycleKicker != null && _bicycleKicker.CanAttemptBicycleKick())
        {
            _bicycleKicker.TryBicycleKick();
            return;
        }

        _kicker.TryKick();
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
    }

    private void EnsureInput()
    {
        if (_input != null)
            return;

        _input = new FootballInput();
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
