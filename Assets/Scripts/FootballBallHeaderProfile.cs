using System;
using UnityEngine;

[Serializable]
public sealed class FootballBallHeaderProfile
{
    [SerializeField, Min(0.1f)] private float _range = 0.85f;
    [SerializeField, Range(0f, 180f)] private float _maxHeaderAngle = 140f;
    [SerializeField, Min(0f)] private float _speed = 13f;
    [SerializeField, Range(0f, 1f)] private float _upwardInfluence = 0.28f;
    [SerializeField, Range(0f, 1f)] private float _playerVelocityInfluence = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _ballVelocityInfluence = 0.25f;
    [SerializeField, Min(0f)] private float _maxBallVelocityBonus = 6f;
    [SerializeField, Min(0f)] private float _spin = 2.5f;
    [SerializeField, Min(0f)] private float _cooldown = 0.22f;
    [SerializeField, Min(0f)] private float _receptionSuppressionTime = 0.18f;

    public float Range => _range;
    public float MaxHeaderAngle => _maxHeaderAngle;
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
        return Vector3.Angle(facing, toBall) <= _maxHeaderAngle;
    }

    public Vector3 CreateLinearVelocity(int facingDirection, Vector3 playerVelocity)
    {
        return CreateLinearVelocity(facingDirection, playerVelocity, Vector3.zero);
    }

    public Vector3 CreateLinearVelocity(int facingDirection, Vector3 playerVelocity, Vector3 ballVelocity)
    {
        Vector3 direction = new Vector3(NormalizeFacingDirection(facingDirection), _upwardInfluence, 0f).normalized;
        Vector3 inheritedVelocity = ToGameplayPlane(playerVelocity) * _playerVelocityInfluence;
        float ballSpeedAlongHitDirection = Mathf.Abs(Vector3.Dot(ToGameplayPlane(ballVelocity), direction));
        float ballVelocityBonus = Mathf.Min(ballSpeedAlongHitDirection * _ballVelocityInfluence, _maxBallVelocityBonus);

        return direction * (_speed + ballVelocityBonus) + inheritedVelocity;
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
