using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public sealed class FootballBall : MonoBehaviour
{
    private const float MinimumScaleMultiplier = 0.01f;

    [SerializeField, Min(0f)] private float _maxLinearSpeed = 28f;
    [SerializeField, Min(0f)] private float _maxAngularSpeed = 45f;
    [SerializeField] private bool _lockToGameplayPlane = true;

    private Rigidbody _rigidbody;
    private SphereCollider _collider;
    private PhysicsMaterial _runtimeMaterial;
    private Vector3 _baseScale;
    private bool _hasBaseScale;
    private float _gravity = GameParameterDefinitions.DefaultBallGravity;
    private float _bounce = GameParameterDefinitions.DefaultBallBounce;
    private float _scaleMultiplier = GameParameterDefinitions.DefaultBallScale;
    private float _passiveContactSuppressedUntil;

    public Vector3 LinearVelocity => _rigidbody.linearVelocity;
    public bool CanReceivePassiveContact => Time.time >= _passiveContactSuppressedUntil;

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseScale();
        ConfigureRigidbody();
        ConfigureCollider();
        ApplyGameParameters();
    }

    private void OnEnable()
    {
        GameParameterSessionValues.ValueChanged += OnGameParameterChanged;
        ApplyGameParameters();
    }

    private void OnDisable()
    {
        GameParameterSessionValues.ValueChanged -= OnGameParameterChanged;
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }

    private void Reset()
    {
        ResolveReferences();
        ConfigureRigidbody();
    }

    private void FixedUpdate()
    {
        ApplyGravity();

        if (_lockToGameplayPlane)
            LockToGameplayPlane();

        ClampMotion();
    }

    public void ApplyReception(Vector3 linearVelocity, float angularVelocityMultiplier)
    {
        ResolveReferences();

        _rigidbody.linearVelocity = ClampLinearVelocity(ToGameplayPlane(linearVelocity));
        _rigidbody.angularVelocity = ClampAngularVelocity(_rigidbody.angularVelocity * Mathf.Clamp01(angularVelocityMultiplier));
    }

    public void ApplyKick(Vector3 linearVelocity, Vector3 angularVelocity, float passiveContactSuppressionTime)
    {
        ApplyDirectedHit(linearVelocity, angularVelocity, passiveContactSuppressionTime);
    }

    public void ApplyHeader(Vector3 linearVelocity, Vector3 angularVelocity, float passiveContactSuppressionTime)
    {
        ApplyDirectedHit(linearVelocity, angularVelocity, passiveContactSuppressionTime);
    }

    public void ApplyBicycleKick(Vector3 linearVelocity, Vector3 angularVelocity, float passiveContactSuppressionTime)
    {
        ApplyDirectedHit(linearVelocity, angularVelocity, passiveContactSuppressionTime);
    }

    public void Respawn(Vector3 position)
    {
        Respawn(position, Quaternion.identity);
    }

    public void Respawn(Vector3 position, Quaternion rotation)
    {
        ResolveReferences();

        _passiveContactSuppressedUntil = 0f;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.position = ToGameplayPlane(position);
        _rigidbody.rotation = rotation;
        transform.SetPositionAndRotation(ToGameplayPlane(position), rotation);
    }

    public Vector3 GetClosestInteractionPoint(Vector3 origin)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        Vector3 selectedPoint = transform.position;
        float bestSqrDistance = ToGameplayPlane(selectedPoint - origin).sqrMagnitude;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            Vector3 point = collider.ClosestPoint(origin);
            float sqrDistance = ToGameplayPlane(point - origin).sqrMagnitude;

            if (sqrDistance >= bestSqrDistance)
                continue;

            selectedPoint = point;
            bestSqrDistance = sqrDistance;
        }

        return ToGameplayPlane(selectedPoint);
    }

    private void ApplyDirectedHit(Vector3 linearVelocity, Vector3 angularVelocity, float passiveContactSuppressionTime)
    {
        ResolveReferences();

        _rigidbody.linearVelocity = ClampLinearVelocity(ToGameplayPlane(linearVelocity));
        _rigidbody.angularVelocity = ClampAngularVelocity(angularVelocity);
        SuppressPassiveContact(passiveContactSuppressionTime);
    }

    private void ResolveReferences()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_collider == null)
            _collider = GetComponent<SphereCollider>();
    }

    private void CaptureBaseScale()
    {
        if (_hasBaseScale)
            return;

        _baseScale = transform.localScale;
        _hasBaseScale = true;
    }

    private void ConfigureRigidbody()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rigidbody.useGravity = false;
        _rigidbody.constraints |= RigidbodyConstraints.FreezePositionZ;
    }

    private void ConfigureCollider()
    {
        if (_collider == null)
            return;

        if (_runtimeMaterial == null)
            _runtimeMaterial = CreateRuntimeMaterial(_collider.sharedMaterial);

        _collider.material = _runtimeMaterial;
    }

    private void ApplyGameParameters()
    {
        _gravity = Mathf.Max(0f, GameParameterSessionValues.GetValue(GameParameterId.BallGravity));
        _bounce = Mathf.Clamp01(GameParameterSessionValues.GetValue(GameParameterId.BallBounce));
        _scaleMultiplier = Mathf.Max(MinimumScaleMultiplier, GameParameterSessionValues.GetValue(GameParameterId.BallScale));

        ApplyBounce();
        ApplyScale();
    }

    private void ApplyGravity()
    {
        if (_gravity <= 0f)
            return;

        _rigidbody.AddForce(GetGravityAcceleration(_gravity), ForceMode.Acceleration);
    }

    private void ApplyBounce()
    {
        if (_runtimeMaterial == null)
            return;

        _runtimeMaterial.bounciness = _bounce;
    }

    private void ApplyScale()
    {
        CaptureBaseScale();
        transform.localScale = _baseScale * _scaleMultiplier;
    }

    private void OnGameParameterChanged(string key, float value)
    {
        if (!GameParameterDefinitions.IsBallParameter(key))
            return;

        ApplyGameParameters();
    }

    private void LockToGameplayPlane()
    {
        Vector3 position = transform.position;
        position.z = 0f;
        transform.position = position;

        _rigidbody.linearVelocity = ToGameplayPlane(_rigidbody.linearVelocity);
    }

    private void ClampMotion()
    {
        _rigidbody.linearVelocity = ClampLinearVelocity(_rigidbody.linearVelocity);
        _rigidbody.angularVelocity = ClampAngularVelocity(_rigidbody.angularVelocity);
    }

    private Vector3 ClampLinearVelocity(Vector3 velocity)
    {
        if (_maxLinearSpeed <= 0f || velocity.sqrMagnitude <= _maxLinearSpeed * _maxLinearSpeed)
            return velocity;

        return velocity.normalized * _maxLinearSpeed;
    }

    private Vector3 ClampAngularVelocity(Vector3 velocity)
    {
        if (_maxAngularSpeed <= 0f || velocity.sqrMagnitude <= _maxAngularSpeed * _maxAngularSpeed)
            return velocity;

        return velocity.normalized * _maxAngularSpeed;
    }

    private void SuppressPassiveContact(float duration)
    {
        if (duration <= 0f)
            return;

        _passiveContactSuppressedUntil = Mathf.Max(_passiveContactSuppressedUntil, Time.time + duration);
    }

    private static Vector3 ToGameplayPlane(Vector3 value)
    {
        value.z = 0f;
        return value;
    }

    private static PhysicsMaterial CreateRuntimeMaterial(PhysicsMaterial source)
    {
        if (source == null)
            return new PhysicsMaterial("Football Ball Runtime");

        return new PhysicsMaterial($"{source.name} Runtime")
        {
            dynamicFriction = source.dynamicFriction,
            staticFriction = source.staticFriction,
            bounciness = source.bounciness,
            frictionCombine = source.frictionCombine,
            bounceCombine = source.bounceCombine
        };
    }

    private static Vector3 GetGravityAcceleration(float gravity)
    {
        if (Physics.gravity.sqrMagnitude <= 0f)
            return Vector3.down * gravity;

        return Physics.gravity.normalized * gravity;
    }
}
