using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class FootballBallReceiver : MonoBehaviour
{
    [SerializeField] private FootballBallReceptionProfile _receptionProfile = new FootballBallReceptionProfile();

    private readonly Dictionary<FootballBall, float> _lastReceptionTimes = new Dictionary<FootballBall, float>();

    private Rigidbody _rigidbody;

    private void Awake()
    {
        ResolveReferences();
        EnsureProfile();
    }

    private void Reset()
    {
        ResolveReferences();
        EnsureProfile();
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryReceive(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryReceive(collision);
    }

    private void ResolveReferences()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();
    }

    private void TryReceive(Collision collision)
    {
        EnsureProfile();

        FootballBall ball = ResolveBall(collision);

        if (ball == null || !ball.CanReceivePassiveContact || IsOnCooldown(ball))
            return;

        float impactSpeed = GetImpactSpeed(collision, ball);

        if (!_receptionProfile.TryCreateVelocity(
            ball.LinearVelocity,
            _rigidbody.linearVelocity,
            impactSpeed,
            out Vector3 receivedVelocity
        ))
            return;

        ball.ApplyReception(receivedVelocity, _receptionProfile.AngularVelocityMultiplier);
        _lastReceptionTimes[ball] = Time.time;
    }

    private bool IsOnCooldown(FootballBall ball)
    {
        if (!_lastReceptionTimes.TryGetValue(ball, out float lastReceptionTime))
            return false;

        return Time.time - lastReceptionTime < _receptionProfile.Cooldown;
    }

    private float GetImpactSpeed(Collision collision, FootballBall ball)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed > 0f)
            return impactSpeed;

        return (ball.LinearVelocity - _rigidbody.linearVelocity).magnitude;
    }

    private static FootballBall ResolveBall(Collision collision)
    {
        if (collision.rigidbody != null && collision.rigidbody.TryGetComponent(out FootballBall ball))
            return ball;

        return collision.collider.GetComponentInParent<FootballBall>();
    }

    private void EnsureProfile()
    {
        if (_receptionProfile == null)
            _receptionProfile = new FootballBallReceptionProfile();
    }
}
