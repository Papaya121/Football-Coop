using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public sealed class FootballBall : MonoBehaviour
{
    private const float MinimumScaleMultiplier = 0.01f;
    private const byte TrailAlpha = 0x28;

    private static readonly Color32 StrongKickTrailColor = new Color32(0xC8, 0x8D, 0x00, TrailAlpha);
    private static readonly Color32 DefaultTrailStartColor = new Color32(0xC8, 0xC8, 0xC8, TrailAlpha);
    private static readonly Color32 DefaultTrailEndColor = new Color32(0xCC, 0xCC, 0xCC, TrailAlpha);

    [SerializeField, Min(0f)] private float _maxLinearSpeed = 24f;
    [SerializeField, Min(0f)] private float _maxAngularSpeed = 45f;
    [SerializeField] private bool _lockToGameplayPlane = true;
    [FormerlySerializedAs("_strongKickSoundSpeed")]
    [SerializeField, Min(0f)] private float _strongKickSpeed = 18f;

    [Header("Strong kick VFX")]
    [SerializeField] private GameObject _strongKickVfx;
    [SerializeField, Min(0f)] private float _strongKickVfxDuration = 0.8f;
    [SerializeField] private TrailRenderer[] _strongKickTrails;

    private Rigidbody _rigidbody;
    private SphereCollider _collider;
    private PhysicsMaterial _runtimeMaterial;
    private Vector3 _baseScale;
    private bool _hasBaseScale;
    private float _gravity = GameParameterDefinitions.DefaultBallGravity;
    private float _bounce = GameParameterDefinitions.DefaultBallBounce;
    private float _scaleMultiplier = GameParameterDefinitions.DefaultBallScale;
    private float _passiveContactSuppressedUntil;
    private float _strongKickVfxEndTime;
    private bool _strongKickVfxIsFading;

    public Vector3 LinearVelocity => _rigidbody.linearVelocity;
    public bool CanReceivePassiveContact => Time.time >= _passiveContactSuppressedUntil;

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseScale();
        ConfigureRigidbody();
        ConfigureCollider();
        ApplyGameParameters();
        StopStrongKickVfx();
    }

    private void OnEnable()
    {
        GameParameterSessionValues.ValueChanged += OnGameParameterChanged;
        ApplyGameParameters();
    }

    private void OnDisable()
    {
        GameParameterSessionValues.ValueChanged -= OnGameParameterChanged;
        StopStrongKickVfx();
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
        UpdateStrongKickVfx();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        FootballSoundSurface surface = collision.collider.GetComponentInParent<FootballSoundSurface>();

        if (surface != null)
            surface.TryPlay(collision);
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
        PlayKickSound(linearVelocity);

        if (linearVelocity.magnitude >= _strongKickSpeed)
            PlayStrongKickVfx();
    }

    public void ApplyHeader(Vector3 linearVelocity, Vector3 angularVelocity, float passiveContactSuppressionTime)
    {
        ApplyDirectedHit(linearVelocity, angularVelocity, passiveContactSuppressionTime);
    }

    public void ApplyBicycleKick(Vector3 linearVelocity, Vector3 angularVelocity, float passiveContactSuppressionTime)
    {
        ApplyDirectedHit(linearVelocity, angularVelocity, passiveContactSuppressionTime);
        FootballSoundPlayer.TryPlay(FootballSoundIds.StrongKick, transform.position);
        PlayStrongKickVfx();
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
        StopStrongKickVfx();
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

        if (_strongKickTrails == null || _strongKickTrails.Length == 0)
            _strongKickTrails = GetComponentsInChildren<TrailRenderer>(true);
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

    private void PlayKickSound(Vector3 linearVelocity)
    {
        string soundId = linearVelocity.magnitude >= _strongKickSpeed
            ? FootballSoundIds.StrongKick
            : FootballSoundIds.Kick;

        FootballSoundPlayer.TryPlay(soundId, transform.position);
    }

    private void PlayStrongKickVfx()
    {
        if (_strongKickVfx == null)
            return;

        _strongKickVfx.SetActive(true);
        _strongKickVfxIsFading = false;
        _strongKickVfxEndTime = Time.time + _strongKickVfxDuration;
        SetStrongKickTrailColors(true);

        foreach (ParticleSystem particles in _strongKickVfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(false);
        }
    }

    private void UpdateStrongKickVfx()
    {
        if (_strongKickVfx == null || !_strongKickVfx.activeSelf)
            return;

        if (!_strongKickVfxIsFading && Time.time >= _strongKickVfxEndTime)
            BeginStrongKickVfxFadeOut();

        if (_strongKickVfxIsFading && !HasAliveStrongKickParticles())
            StopStrongKickVfx();
    }

    private void BeginStrongKickVfxFadeOut()
    {
        _strongKickVfxIsFading = true;
        SetStrongKickTrailColors(false);

        foreach (ParticleSystem particles in _strongKickVfx.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    private bool HasAliveStrongKickParticles()
    {
        foreach (ParticleSystem particles in _strongKickVfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.IsAlive(false))
                return true;
        }

        return false;
    }

    private void StopStrongKickVfx()
    {
        _strongKickVfxEndTime = 0f;
        _strongKickVfxIsFading = false;

        if (_strongKickVfx == null)
            return;

        foreach (ParticleSystem particles in _strongKickVfx.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

        _strongKickVfx.SetActive(false);
        SetStrongKickTrailColors(false);
    }

    private void SetStrongKickTrailColors(bool isStrongKick)
    {
        ResolveReferences();

        Color startColor = isStrongKick ? StrongKickTrailColor : DefaultTrailStartColor;
        Color endColor = isStrongKick ? StrongKickTrailColor : DefaultTrailEndColor;
        float alpha = TrailAlpha / 255f;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha, 1f)
            }
        );

        for (int i = 0; i < _strongKickTrails.Length; i++)
        {
            if (_strongKickTrails[i] != null)
                _strongKickTrails[i].colorGradient = gradient;
        }
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
