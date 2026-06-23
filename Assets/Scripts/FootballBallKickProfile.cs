using System;
using UnityEngine;

[Serializable]
public sealed class FootballBallKickProfile
{
    [SerializeField, Min(0.1f)] private float _range = 1.05f;
    [SerializeField, Range(0f, 180f)] private float _maxKickAngle = 105f;
    [SerializeField, Min(0f)] private float _speed = 15f;
    [SerializeField, Range(0f, 1f)] private float _upwardInfluence = 0.18f;
    [SerializeField, Range(0f, 1f)] private float _playerVelocityInfluence = 0.35f;
    [SerializeField, Min(0f)] private float _spin = 7.5f;
    [SerializeField, Min(0f)] private float _cooldown = 0.18f;
    [SerializeField, Min(0f)] private float _receptionSuppressionTime = 0.16f;

    public float Range => _range;
    public float MaxKickAngle => _maxKickAngle;
    public float Cooldown => _cooldown;
    public float ReceptionSuppressionTime => _receptionSuppressionTime;

    public bool CanReach(Vector3 origin, int facingDirection, Vector3 ballPosition)
    {
        Vector3 toBall = ToGameplayPlane(ballPosition - origin);

        if (toBall.sqrMagnitude > _range * _range)
            return false;

        if (toBall.sqrMagnitude <= Mathf.Epsilon)
            return true;

        Vector3 facing = Vector3.right * NormalizeFacingDirection(facingDirection);
        return Vector3.Angle(facing, toBall) <= _maxKickAngle;
    }

    public Vector3 CreateLinearVelocity(int facingDirection, Vector3 playerVelocity)
    {
        Vector3 direction = new Vector3(NormalizeFacingDirection(facingDirection), _upwardInfluence, 0f).normalized;
        Vector3 inheritedVelocity = ToGameplayPlane(playerVelocity) * _playerVelocityInfluence;

        return direction * _speed + inheritedVelocity;
    }

    public Vector3 CreateAngularVelocity(Vector3 linearVelocity)
    {
        return Vector3.forward * (-linearVelocity.x * _spin);
    }

    private static int NormalizeFacingDirection(int facingDirection)
    {
        return facingDirection < 0 ? -1 : 1;
    }

    private static Vector3 ToGameplayPlane(Vector3 value)
    {
        value.z = 0f;
        return value;
    }
}
