using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum FootballPlayerControlSource
{
    WasdKeyboard,
    ArrowKeyboard,
    Gamepad
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class FootballPlayerController : MonoBehaviour
{
    private const float MinimumScaleMultiplier = 0.01f;

    [SerializeField] private Transform _visualRoot;
    [SerializeField] private LayerMask _groundMask;

    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _groundAcceleration = 70f;
    [SerializeField] private float _airAcceleration = 25f;
    [SerializeField] private float _jumpForce = 9f;
    [SerializeField, Min(0)] private int _airJumpCount = 1;
    [SerializeField, Min(1f)] private float _fallGravityMultiplier = 2.5f;

    [SerializeField] private float _coyoteTime = 0.08f;
    [SerializeField] private float _jumpBufferTime = 0.1f;
    [SerializeField] private float _groundCheckDistance = 0.08f;
    [SerializeField] private float _groundCheckRadiusMultiplier = 0.9f;
    [SerializeField, Range(0f, 89f)] private float _maxGroundAngle = 60f;
    [SerializeField] private Vector3 _jumpColliderCenter = new Vector3(0f, 1.73f, 0f);
    [SerializeField, Min(0.01f)] private float _jumpColliderHeight = 1.57f;
    [SerializeField, Min(0.01f)] private float _colliderResizeSpeed = 8f;

    private FootballInput _input;
    private Rigidbody _rigidbody;
    private CapsuleCollider _collider;
    private PhysicsMaterial _movementMaterial;
    private Vector3 _baseScale;
    private bool _hasBaseScale;
    private Vector3 _defaultColliderCenter;
    private float _defaultColliderHeight;
    private Vector3 _initialVisualScale;
    private bool _hasInitialVisualScale;

    private Vector2 _moveInput;

    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private int _remainingAirJumps;

    private FootballPlayerControlSource _controlSource = FootballPlayerControlSource.Gamepad;
    private InputDevice _controlDevice;

    private bool _isGrounded;
    private bool _isJumping;
    private Vector3 _groundNormal = Vector3.up;
    private int _facingDirection = 1;
    private float _gravity = GameParameterDefinitions.DefaultPlayerGravity;
    private float _scaleMultiplier = GameParameterDefinitions.DefaultPlayerScale;

    public event Action<FootballPlayerControlSource, InputDevice> InputAssigned;
    public event Action DoubleJumped;

    public FootballPlayerControlSource ControlSource => _controlSource;
    public InputDevice ControlDevice => _controlDevice;
    public Vector2 MoveInput => _moveInput;
    public int FacingDirection => _facingDirection;
    public bool IsGrounded => _isGrounded;
    public bool IsRunning => !_isJumping && _isGrounded && Mathf.Abs(_moveInput.x) > 0.05f;
    public bool IsJumping => _isJumping;

    private void Awake()
    {
        ResolveReferences();
        EnsureInput();
        CaptureBaseScale();

        if (_visualRoot)
        {
            _initialVisualScale = _visualRoot.localScale;
            _hasInitialVisualScale = true;
            _facingDirection = (int)_visualRoot.localScale.x;
        }

        _defaultColliderCenter = _collider.center;
        _defaultColliderHeight = _collider.height;

        ConfigureRigidbody();
        ApplyGameParameters();
    }

    public void AssignInput(FootballPlayerControlSource source, InputDevice device = null)
    {
        EnsureInput();

        _controlSource = source;
        _controlDevice = device;
        _moveInput = Vector2.zero;
        _jumpBufferTimer = 0f;
        _remainingAirJumps = _airJumpCount;

        ApplyInputRestrictions();
        InputAssigned?.Invoke(_controlSource, _controlDevice);
    }

    public void Respawn(Vector3 position, Quaternion rotation)
    {
        ResolveReferences();

        _moveInput = Vector2.zero;
        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _remainingAirJumps = _airJumpCount;
        _isGrounded = false;
        _isJumping = false;
        _groundNormal = Vector3.up;

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.position = ToGameplayPlane(position);
        _rigidbody.rotation = rotation;

        transform.SetPositionAndRotation(ToGameplayPlane(position), rotation);

        _collider.center = _defaultColliderCenter;
        _collider.height = _defaultColliderHeight;

        if (_visualRoot == null || !_hasInitialVisualScale)
            return;

        _visualRoot.localScale = _initialVisualScale;
        _facingDirection = _initialVisualScale.x >= 0f ? 1 : -1;
    }

    private void ResolveReferences()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
    }

    private void EnsureInput()
    {
        if (_input != null)
            return;

        _input = new FootballInput();
        ApplyInputRestrictions();
    }

    private void OnEnable()
    {
        GameParameterSessionValues.ValueChanged += OnGameParameterChanged;
        ApplyGameParameters();

        EnsureInput();

        _input.Player.Move.performed += OnMove;
        _input.Player.Move.canceled += OnMove;
        _input.Player.Jump.performed += OnJump;

        _input.Player.Enable();
    }

    private void OnDisable()
    {
        GameParameterSessionValues.ValueChanged -= OnGameParameterChanged;

        if (_input == null)
            return;

        _input.Player.Move.performed -= OnMove;
        _input.Player.Move.canceled -= OnMove;
        _input.Player.Jump.performed -= OnJump;

        _input.Player.Disable();
        _moveInput = Vector2.zero;
    }

    private void OnDestroy()
    {
        _input?.Dispose();

        if (_movementMaterial != null)
            Destroy(_movementMaterial);
    }

    private void ApplyInputRestrictions()
    {
        bool wasEnabled = _input.Player.enabled;

        if (wasEnabled)
            _input.Player.Disable();

        _input.devices = _controlDevice != null ? new[] { _controlDevice } : null;

        _input.bindingMask = FootballInputBindingMasks.FromControlSource(_controlSource);

        if (wasEnabled)
            _input.Player.Enable();
    }

    private void FixedUpdate()
    {
        _isGrounded = CheckGrounded();

        UpdateTimers();
        UpdateJumpingState();
        Move();
        TryJump();
        ApplyGravity();
        UpdateColliderShape();
        LockPlane();
        UpdateFacing();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        _jumpBufferTimer = _jumpBufferTime;
    }

    private void ConfigureRigidbody()
    {
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rigidbody.useGravity = false;
        _rigidbody.freezeRotation = true;

        _rigidbody.constraints =
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        _movementMaterial = new PhysicsMaterial("Football Player Movement")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        _collider.material = _movementMaterial;
    }

    private void UpdateTimers()
    {
        if (_isGrounded)
        {
            _coyoteTimer = _coyoteTime;
            _remainingAirJumps = _airJumpCount;
        }
        else
        {
            _coyoteTimer = Mathf.Max(0f, _coyoteTimer - Time.fixedDeltaTime);
        }

        _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - Time.fixedDeltaTime);
    }

    private void Move()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        float acceleration = _isGrounded ? _groundAcceleration : _airAcceleration;

        if (_isGrounded)
            velocity = MoveAlongGround(velocity, acceleration);
        else
            velocity.x = Mathf.MoveTowards(
                velocity.x,
                _moveInput.x * _moveSpeed,
                acceleration * Time.fixedDeltaTime
            );

        velocity.z = 0f;

        _rigidbody.linearVelocity = velocity;
    }

    private Vector3 MoveAlongGround(Vector3 velocity, float acceleration)
    {
        Vector3 moveAxis = Vector3.ProjectOnPlane(Vector3.right, _groundNormal);

        if (moveAxis.sqrMagnitude < 0.0001f)
            moveAxis = Vector3.right;
        else
            moveAxis.Normalize();

        if (moveAxis.x < 0f)
            moveAxis = -moveAxis;

        float currentSpeed = Vector3.Dot(velocity, moveAxis);
        float targetSpeed = _moveInput.x * _moveSpeed;
        float newSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            acceleration * Time.fixedDeltaTime
        );

        Vector3 normalVelocity = Vector3.Project(velocity, _groundNormal);

        if (Vector3.Dot(normalVelocity, _groundNormal) > 0f)
            normalVelocity = Vector3.zero;

        return moveAxis * newSpeed + normalVelocity;
    }

    private void TryJump()
    {
        if (_jumpBufferTimer <= 0f)
            return;

        bool canUseGroundJump = _coyoteTimer > 0f;
        bool canUseAirJump = !canUseGroundJump && _remainingAirJumps > 0;

        if (!canUseGroundJump && !canUseAirJump)
            return;

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;

        _rigidbody.linearVelocity = velocity;
        _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.VelocityChange);

        _isJumping = true;
        _jumpBufferTimer = 0f;
        _coyoteTimer = 0f;

        if (canUseAirJump)
        {
            _remainingAirJumps--;
            DoubleJumped?.Invoke();
        }
    }

    private void UpdateJumpingState()
    {
        if (_isJumping && _isGrounded && _rigidbody.linearVelocity.y <= 0.01f)
            _isJumping = false;
    }

    private void ApplyGravity()
    {
        Vector3 gravityAcceleration = GetGravityAcceleration(_gravity);

        if (_gravity > 0f)
            _rigidbody.AddForce(gravityAcceleration, ForceMode.Acceleration);

        if (_isGrounded || _rigidbody.linearVelocity.y >= 0f)
            return;

        Vector3 extraGravity = gravityAcceleration * (_fallGravityMultiplier - 1f);
        _rigidbody.AddForce(extraGravity, ForceMode.Acceleration);
    }

    private void UpdateColliderShape()
    {
        Vector3 targetCenter = _isJumping ? _jumpColliderCenter : _defaultColliderCenter;
        float targetHeight = _isJumping ? _jumpColliderHeight : _defaultColliderHeight;
        float maxDistanceDelta = _colliderResizeSpeed * Time.fixedDeltaTime;

        _collider.center = Vector3.MoveTowards(_collider.center, targetCenter, maxDistanceDelta);
        _collider.height = Mathf.MoveTowards(_collider.height, targetHeight, maxDistanceDelta);
    }

    private bool CheckGrounded()
    {
        _groundNormal = Vector3.up;

        Vector3 scale = transform.lossyScale;
        Vector3 origin = transform.TransformPoint(_defaultColliderCenter);

        float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float heightScale = Mathf.Abs(scale.y);

        float radius = _collider.radius * radiusScale * _groundCheckRadiusMultiplier;
        float height = Mathf.Max(_defaultColliderHeight * heightScale, radius * 2f);
        float distance = height * 0.5f - radius + _groundCheckDistance;

        if (!Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out RaycastHit hit,
            distance,
            _groundMask,
            QueryTriggerInteraction.Ignore
        ))
            return false;

        if (Vector3.Angle(hit.normal, Vector3.up) > _maxGroundAngle)
            return false;

        _groundNormal = hit.normal;
        return true;
    }

    private void LockPlane()
    {
        Vector3 position = transform.position;
        position.z = 0f;
        transform.position = position;

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.z = 0f;
        _rigidbody.linearVelocity = velocity;
    }

    private void UpdateFacing()
    {
        if (!_isGrounded || _isJumping)
            return;

        if (Mathf.Abs(_moveInput.x) < 0.05f)
            return;

        int direction = _moveInput.x > 0f ? 1 : -1;

        if (direction == _facingDirection)
            return;

        _facingDirection = direction;

        if (_visualRoot == null)
            return;

        Vector3 scale = _visualRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * _facingDirection;
        _visualRoot.localScale = scale;
    }

    private static Vector3 ToGameplayPlane(Vector3 value)
    {
        value.z = 0f;
        return value;
    }

    private void CaptureBaseScale()
    {
        if (_hasBaseScale)
            return;

        _baseScale = transform.localScale;
        _hasBaseScale = true;
    }

    private void ApplyGameParameters()
    {
        _gravity = Mathf.Max(0f, GameParameterSessionValues.GetValue(GameParameterId.PlayerGravity));
        _jumpForce = Mathf.Max(0f, GameParameterSessionValues.GetValue(GameParameterId.PlayerJump));
        _airAcceleration = Mathf.Max(0f, GameParameterSessionValues.GetValue(GameParameterId.PlayerAirAcceleration));
        _scaleMultiplier = Mathf.Max(MinimumScaleMultiplier, GameParameterSessionValues.GetValue(GameParameterId.PlayerScale));

        ApplyScale();
    }

    private void ApplyScale()
    {
        CaptureBaseScale();
        transform.localScale = _baseScale * _scaleMultiplier;
    }

    private void OnGameParameterChanged(string key, float value)
    {
        if (!GameParameterDefinitions.IsPlayerParameter(key))
            return;

        ApplyGameParameters();
    }

    private static Vector3 GetGravityAcceleration(float gravity)
    {
        if (Physics.gravity.sqrMagnitude <= 0f)
            return Vector3.down * gravity;

        return Physics.gravity.normalized * gravity;
    }
}
