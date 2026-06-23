using System;
using UnityEngine;

[Serializable]
public sealed class FootballBallReceptionProfile
{
    [SerializeField, Min(0f)] private float _minimumImpactSpeed = 1.25f;
    [SerializeField, Range(0f, 1f)] private float _horizontalVelocityMultiplier = 0.16f;
    [SerializeField, Range(0f, 1f)] private float _verticalVelocityMultiplier = 0.12f;
    [SerializeField, Range(0f, 1f)] private float _receiverVelocityInfluence = 0.25f;
    [SerializeField, Min(0f)] private float _snapToStopSpeed = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _angularVelocityMultiplier = 0.2f;
    [SerializeField, Min(0f)] private float _cooldown = 0.08f;

    public float Cooldown => _cooldown;
    public float AngularVelocityMultiplier => _angularVelocityMultiplier;

    public bool TryCreateVelocity(
        Vector3 ballVelocity,
        Vector3 receiverVelocity,
        float impactSpeed,
        out Vector3 receivedVelocity
    )
    {
        receivedVelocity = Vector3.zero;

        if (impactSpeed < _minimumImpactSpeed)
            return false;

        Vector3 relativeVelocity = ToGameplayPlane(ballVelocity - receiverVelocity);
        Vector3 inheritedVelocity = ToGameplayPlane(receiverVelocity) * _receiverVelocityInfluence;

        relativeVelocity.x *= _horizontalVelocityMultiplier;
        relativeVelocity.y *= _verticalVelocityMultiplier;

        receivedVelocity = inheritedVelocity + relativeVelocity;
        receivedVelocity.z = 0f;

        if (receivedVelocity.sqrMagnitude <= _snapToStopSpeed * _snapToStopSpeed)
            receivedVelocity = Vector3.zero;

        return true;
    }

    private static Vector3 ToGameplayPlane(Vector3 value)
    {
        value.z = 0f;
        return value;
    }
}
