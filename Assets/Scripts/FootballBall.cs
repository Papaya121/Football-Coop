using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public sealed class FootballBall : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _maxLinearSpeed = 28f;
    [SerializeField, Min(0f)] private float _maxAngularSpeed = 45f;
    [SerializeField] private bool _lockToGameplayPlane = true;

    private Rigidbody _rigidbody;
    private float _passiveContactSuppressedUntil;

    public Vector3 LinearVelocity => _rigidbody.linearVelocity;
    public bool CanReceivePassiveContact => Time.time >= _passiveContactSuppressedUntil;

    private void Awake()
    {
        ResolveReferences();
        ConfigureRigidbody();
    }

    private void Reset()
    {
        ResolveReferences();
        ConfigureRigidbody();
    }

    private void FixedUpdate()
    {
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
    }

    private void ConfigureRigidbody()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rigidbody.constraints |= RigidbodyConstraints.FreezePositionZ;
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
}
