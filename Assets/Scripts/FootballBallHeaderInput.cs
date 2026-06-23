using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
[RequireComponent(typeof(FootballBallHeader))]
public sealed class FootballBallHeaderInput : MonoBehaviour
{
    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private FootballBallHeader _header;

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

        _input.Ball.Header.performed += OnHeader;
        _input.Ball.Enable();
    }

    private void OnDisable()
    {
        if (_controller != null)
            _controller.InputAssigned -= OnInputAssigned;

        if (_input == null)
            return;

        _input.Ball.Header.performed -= OnHeader;
        _input.Ball.Disable();
    }

    private void OnDestroy()
    {
        _input?.Dispose();
    }

    private void OnHeader(InputAction.CallbackContext context)
    {
        if (!context.performed || _header == null)
            return;

        _header.TryHeader();
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

        if (_header == null)
            _header = GetComponent<FootballBallHeader>();
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
