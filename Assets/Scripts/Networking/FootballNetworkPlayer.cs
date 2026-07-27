using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(FootballPlayerController))]
public sealed class FootballNetworkPlayer : NetworkBehaviour
{
    private const float InputRefreshInterval = 0.1f;

    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private FootballBallKicker _kicker;
    [SerializeField] private FootballBallBicycleKicker _bicycleKicker;
    [SerializeField] private FootballBallHeader _header;
    [SerializeField] private FootballPlayerAnimator _animator;
    [SerializeField] private FootballBallKickInput _localKickInput;
    [SerializeField] private FootballBallHeaderInput _localHeaderInput;
    [SerializeField] private Renderer[] _teamRenderers;
    [SerializeField] private Material _leftTeamMaterial;
    [SerializeField] private Material _rightTeamMaterial;

    [SyncVar(hook = nameof(OnTeamSideChanged))] private FootballTeamSide _teamSide;
    [SyncVar] private Vector2 _presentationMoveInput;
    [SyncVar] private bool _presentationIsGrounded;
    [SyncVar] private bool _presentationIsJumping;
    [SyncVar] private int _presentationFacingDirection = 1;

    private FootballInput _input;
    private Vector2 _lastSentMoveInput;
    private float _nextInputRefreshTime;
    private float _nextClientDiagnosticTime;
    private float _nextClientStateDiagnosticTime;
    private float _nextServerDiagnosticTime;
    private uint _serverMoveCommandCount;
    private bool _serverGameplayEnabled;

    private void Awake()
    {
        ResolveReferences();
    }

    public override void OnStartServer()
    {
        ResolveReferences();
        ApplyTeamVisuals(_teamSide);
        _controller.SetNetworkSimulationEnabled(true);
        _controller.DoubleJumped += OnServerDoubleJumped;
        FootballNetworkDiagnostics.Write(
            "PLAYER-SERVER",
            $"Started. netId={netId}; connectionId={connectionToClient?.connectionId}; side={_teamSide}; " +
            $"scene={gameObject.scene.name}; rb={DescribeRigidbody()}"
        );
    }

    public override void OnStopServer()
    {
        if (_controller != null)
            _controller.DoubleJumped -= OnServerDoubleJumped;
    }

    public override void OnStartClient()
    {
        ResolveReferences();
        ApplyTeamVisuals(_teamSide);
        SetLegacyInputEnabled(false);

        if (!isServer)
            _controller.SetNetworkSimulationEnabled(false);

        FootballNetworkDiagnostics.Write(
            "PLAYER-CLIENT",
            $"Started. netId={netId}; isLocalPlayer={isLocalPlayer}; isOwned={isOwned}; side={_teamSide}; " +
            $"scene={gameObject.scene.name}; rb={DescribeRigidbody()}; sync={DescribeNetworkTransform()}; " +
            $"visuals={DescribeTeamVisuals()}"
        );
    }

    public override void OnStartLocalPlayer()
    {
        _input = new FootballInput();
        _input.Player.Jump.performed += OnJump;
        _input.Ball.Kick.performed += OnKick;
        _input.Ball.Header.performed += OnHeader;
        _input.Enable();
        FootballNetworkDiagnostics.Write(
            "INPUT-CLIENT",
            $"Local player started. netId={netId}; keyboardPresent={Keyboard.current != null}; " +
            $"gamepadCount={Gamepad.all.Count}; actionAssetEnabled={_input.asset.enabled}"
        );
    }

    public override void OnStopLocalPlayer()
    {
        if (_input == null)
            return;

        _input.Player.Jump.performed -= OnJump;
        _input.Ball.Kick.performed -= OnKick;
        _input.Ball.Header.performed -= OnHeader;
        _input.Dispose();
        _input = null;
    }

    [Server]
    public void ServerInitialize(FootballTeamSide teamSide)
    {
        _teamSide = teamSide;
        ApplyTeamVisuals(teamSide);
    }

    [Server]
    public void ServerSetGameplayEnabled(bool enabled)
    {
        _serverGameplayEnabled = enabled;

        if (!enabled)
            _controller?.SetNetworkMoveInput(Vector2.zero);
    }

#if UNITY_EDITOR
    public void EditorConfigureTeamVisuals(
        Renderer[] teamRenderers,
        Material leftTeamMaterial,
        Material rightTeamMaterial)
    {
        _teamRenderers = teamRenderers;
        _leftTeamMaterial = leftTeamMaterial;
        _rightTeamMaterial = rightTeamMaterial;
    }
#endif

    private void Update()
    {
        if (isLocalPlayer && _input != null)
            SendMovementInput();

        if (isClient && Time.unscaledTime >= _nextClientStateDiagnosticTime)
        {
            _nextClientStateDiagnosticTime = Time.unscaledTime + 1f;
            FootballNetworkDiagnostics.Write(
                "PLAYER-CLIENT-STATE",
                $"netId={netId}; local={isLocalPlayer}; transformPos={transform.position}; " +
                $"rb={DescribeRigidbody()}; sync={DescribeNetworkTransform()}"
            );
        }

        if (isServer)
            CapturePresentationState();
        else if (isClient && _controller != null)
            _controller.ApplyNetworkPresentation(
                _presentationMoveInput,
                _presentationIsGrounded,
                _presentationIsJumping,
                _presentationFacingDirection
            );
    }

    [Command(channel = Channels.Unreliable)]
    private void CmdSetMoveInput(Vector2 moveInput)
    {
        _serverMoveCommandCount++;

        if (_controller == null || !_serverGameplayEnabled)
            return;

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        _controller.SetNetworkMoveInput(moveInput);

        if (Time.unscaledTime >= _nextServerDiagnosticTime)
        {
            _nextServerDiagnosticTime = Time.unscaledTime + 1f;
            FootballNetworkDiagnostics.Write(
                "INPUT-SERVER",
                $"Move command. netId={netId}; count={_serverMoveCommandCount}; value={moveInput}; " +
                $"controllerValue={_controller.MoveInput}; rb={DescribeRigidbody()}"
            );
        }
    }

    [Command]
    private void CmdJump()
    {
        if (_serverGameplayEnabled)
            _controller?.QueueNetworkJump();
    }

    [Command]
    private void CmdKick()
    {
        if (!_serverGameplayEnabled)
            return;

        if (_bicycleKicker != null && _bicycleKicker.CanAttemptBicycleKick())
        {
            if (_bicycleKicker.TryBicycleKick())
                RpcPlayAction(FootballNetworkAction.BicycleKick);
            return;
        }

        if (_kicker != null && _kicker.TryKick())
            RpcPlayAction(FootballNetworkAction.Kick);
    }

    [Command]
    private void CmdHeader()
    {
        if (!_serverGameplayEnabled)
            return;

        if (_header != null && _header.TryHeader())
            RpcPlayAction(FootballNetworkAction.Header);
    }

    [ClientRpc]
    private void RpcPlayAction(FootballNetworkAction action)
    {
        ResolveReferences();

        switch (action)
        {
            case FootballNetworkAction.Kick:
                _animator?.TriggerKickAnimation();
                break;
            case FootballNetworkAction.BicycleKick:
                _animator?.TriggerBicycleKickAnimation();
                break;
            case FootballNetworkAction.Header:
                _animator?.TriggerHeaderAnimation();
                break;
            case FootballNetworkAction.DoubleJump:
                _animator?.TriggerDoubleJumpAnimation();
                break;
        }

        if (action != FootballNetworkAction.DoubleJump)
            FootballSoundPlayer.TryPlay(FootballSoundIds.Kick, transform.position);
    }

    private void SendMovementInput()
    {
        Vector2 moveInput = Vector2.ClampMagnitude(_input.Player.Move.ReadValue<Vector2>(), 1f);
        bool changed = (moveInput - _lastSentMoveInput).sqrMagnitude > 0.0001f;

        if (!changed && Time.unscaledTime < _nextInputRefreshTime)
            return;

        _lastSentMoveInput = moveInput;
        _nextInputRefreshTime = Time.unscaledTime + InputRefreshInterval;
        CmdSetMoveInput(moveInput);

        if (changed || Time.unscaledTime >= _nextClientDiagnosticTime)
        {
            _nextClientDiagnosticTime = Time.unscaledTime + 1f;
            FootballNetworkDiagnostics.Write(
                "INPUT-CLIENT",
                $"Move sent. netId={netId}; value={moveInput}; changed={changed}; " +
                $"moveActionEnabled={_input.Player.Move.enabled}"
            );
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
            CmdJump();
    }

    private void OnKick(InputAction.CallbackContext context)
    {
        if (context.performed)
            CmdKick();
    }

    private void OnHeader(InputAction.CallbackContext context)
    {
        if (context.performed)
            CmdHeader();
    }

    private void CapturePresentationState()
    {
        if (_controller == null)
            return;

        _presentationMoveInput = _controller.MoveInput;
        _presentationIsGrounded = _controller.IsGrounded;
        _presentationIsJumping = _controller.IsJumping;
        _presentationFacingDirection = _controller.FacingDirection;
    }

    [Server]
    private void OnServerDoubleJumped()
    {
        RpcPlayAction(FootballNetworkAction.DoubleJump);
    }

    private void OnTeamSideChanged(FootballTeamSide _, FootballTeamSide newSide)
    {
        ApplyTeamVisuals(newSide);
    }

    private void ApplyTeamVisuals(FootballTeamSide teamSide)
    {
        Material teamMaterial = teamSide == FootballTeamSide.Left
            ? _leftTeamMaterial
            : _rightTeamMaterial;

        if (teamMaterial == null || _teamRenderers == null)
            return;

        foreach (Renderer teamRenderer in _teamRenderers)
        {
            if (teamRenderer == null)
                continue;

            Material[] materials = teamRenderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
                materials[i] = teamMaterial;

            teamRenderer.sharedMaterials = materials;
        }
    }

    private string DescribeRigidbody()
    {
        Rigidbody body = GetComponent<Rigidbody>();

        return body == null
            ? "missing"
            : $"kinematic={body.isKinematic}, sleeping={body.IsSleeping()}, pos={body.position}, velocity={body.linearVelocity}";
    }

    private string DescribeTeamVisuals()
    {
        if (_teamRenderers == null)
            return "renderers=null";

        string[] descriptions = new string[_teamRenderers.Length];

        for (int i = 0; i < _teamRenderers.Length; i++)
        {
            Renderer renderer = _teamRenderers[i];
            descriptions[i] = renderer == null
                ? "null"
                : $"{renderer.name}:{string.Join(",", System.Array.ConvertAll(renderer.sharedMaterials, material => material != null ? material.name : "null"))}";
        }

        return string.Join(";", descriptions);
    }

    private string DescribeNetworkTransform()
    {
        NetworkRigidbodyUnreliable networkRigidbody = GetComponent<NetworkRigidbodyUnreliable>();

        return networkRigidbody == null
            ? "missing"
            : $"enabled={networkRigidbody.enabled}, direction={networkRigidbody.syncDirection}, " +
              $"updateMethod={networkRigidbody.updateMethod}, clientSnapshots={networkRigidbody.clientSnapshots.Count}";
    }

    private void SetLegacyInputEnabled(bool enabled)
    {
        if (_localKickInput != null)
            _localKickInput.enabled = enabled;

        if (_localHeaderInput != null)
            _localHeaderInput.enabled = enabled;
    }

    private void ResolveReferences()
    {
        if (_controller == null)
            _controller = GetComponent<FootballPlayerController>();
        if (_kicker == null)
            _kicker = GetComponent<FootballBallKicker>();
        if (_bicycleKicker == null)
            _bicycleKicker = GetComponent<FootballBallBicycleKicker>();
        if (_header == null)
            _header = GetComponent<FootballBallHeader>();
        if (_animator == null)
            _animator = GetComponent<FootballPlayerAnimator>();
        if (_localKickInput == null)
            _localKickInput = GetComponent<FootballBallKickInput>();
        if (_localHeaderInput == null)
            _localHeaderInput = GetComponent<FootballBallHeaderInput>();
    }


    private enum FootballNetworkAction : byte
    {
        Kick,
        BicycleKick,
        Header,
        DoubleJump
    }
}
