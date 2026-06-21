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
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private LayerMask _groundMask;

    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _groundAcceleration = 70f;
    [SerializeField] private float _airAcceleration = 25f;
    [SerializeField] private float _jumpForce = 9f;
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
    private Vector3 _defaultColliderCenter;
    private float _defaultColliderHeight;

    private Vector2 _moveInput;

    private float _coyoteTimer;
    private float _jumpBufferTimer;

    private FootballPlayerControlSource _controlSource = FootballPlayerControlSource.Gamepad;
    private InputDevice _controlDevice;

    private bool _isGrounded;
    private bool _isJumping;
    private Vector3 _groundNormal = Vector3.up;
    private int _facingDirection = 1;

    public bool IsGrounded => _isGrounded;
    public bool IsRunning => !_isJumping && _isGrounded && Mathf.Abs(_moveInput.x) > 0.05f;
    public bool IsJumping => _isJumping;

    private void Awake()
    {
        ResolveReferences();
        EnsureInput();

        _defaultColliderCenter = _collider.center;
        _defaultColliderHeight = _collider.height;

        ConfigureRigidbody();
    }

    public void AssignInput(FootballPlayerControlSource source, InputDevice device = null)
    {
        EnsureInput();

        _controlSource = source;
        _controlDevice = device;
        _moveInput = Vector2.zero;
        _jumpBufferTimer = 0f;

        ApplyInputRestrictions();
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
        EnsureInput();

        _input.Player.Move.performed += OnMove;
        _input.Player.Move.canceled += OnMove;
        _input.Player.Jump.performed += OnJump;

        _input.Player.Enable();
    }

    private void OnDisable()
    {
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

        _input.bindingMask = _controlSource switch
        {
            FootballPlayerControlSource.WasdKeyboard => InputBinding.MaskByGroup("WASD"),
            FootballPlayerControlSource.ArrowKeyboard => InputBinding.MaskByGroup("Arrows"),
            FootballPlayerControlSource.Gamepad => InputBinding.MaskByGroup("Gamepad"),
            _ => null
        };

        if (wasEnabled)
            _input.Player.Enable();
    }

    private void FixedUpdate()
    {
        _isGrounded = CheckGrounded();

        UpdateTimers();
        UpdateJumpingState();
        Move();
        ApplyExtraFallGravity();
        TryJump();
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
        _coyoteTimer = _isGrounded ? _coyoteTime : Mathf.Max(0f, _coyoteTimer - Time.fixedDeltaTime);
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
        if (_jumpBufferTimer <= 0f || _coyoteTimer <= 0f)
            return;

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;

        _rigidbody.linearVelocity = velocity;
        _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.VelocityChange);

        _isJumping = true;
        _jumpBufferTimer = 0f;
        _coyoteTimer = 0f;
    }

    private void UpdateJumpingState()
    {
        if (_isJumping && _isGrounded && _rigidbody.linearVelocity.y <= 0.01f)
            _isJumping = false;
    }

    private void ApplyExtraFallGravity()
    {
        if (_isGrounded || _rigidbody.linearVelocity.y >= 0f)
            return;

        Vector3 extraGravity = Physics.gravity * (_fallGravityMultiplier - 1f);
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
}
