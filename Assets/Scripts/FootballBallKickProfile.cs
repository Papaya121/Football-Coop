using System;
using UnityEngine;

[Serializable]
public sealed class FootballBallKickProfile
{
    [SerializeField, Min(0.1f)] private float _range = 1.05f;
    [SerializeField, Range(0f, 180f)] private float _maxKickAngle = 105f;
    [SerializeField, Min(0f)] private float _speed = 15f;
    [Tooltip("When enabled, charge changes the shot height while kick force stays constant. Disable to restore charge-based force and upward-input lobs.")]
    [SerializeField] private bool _chargeControlsHeight = true;
    [SerializeField, Range(0f, 1f)] private float _upwardInfluence = 0.18f;
    [SerializeField, Range(0f, 1f)] private float _lobUpwardInfluence = 0.65f;
    [SerializeField, Range(0f, 1f)] private float _playerVelocityInfluence = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _ballVelocityInfluence = 0.35f;
    [SerializeField, Min(0f)] private float _maxBallVelocityBonus = 8f;
    [SerializeField, Min(0f)] private float _spin = 7.5f;
    [SerializeField, Min(0f)] private float _cooldown = 0.18f;
    [SerializeField, Min(0f)] private float _receptionSuppressionTime = 0.16f;

    public float Range => _range;
    public float MaxKickAngle => _maxKickAngle;
    public float Speed => _speed;
    public bool ChargeControlsHeight => _chargeControlsHeight;
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
        return CreateLinearVelocity(facingDirection, playerVelocity, Vector3.zero);
    }

    public Vector3 CreateLinearVelocity(int facingDirection, Vector3 playerVelocity, Vector3 ballVelocity)
    {
        return CreateLinearVelocity(facingDirection, playerVelocity, ballVelocity, 1f);
    }

    public Vector3 CreateLinearVelocity(int facingDirection, Vector3 playerVelocity, Vector3 ballVelocity, float powerMultiplier)
    {
        return CreateLinearVelocity(facingDirection, playerVelocity, ballVelocity, powerMultiplier, false);
    }

    public Vector3 CreateLinearVelocity(
        int facingDirection,
        Vector3 playerVelocity,
        Vector3 ballVelocity,
        float powerMultiplier,
        bool isLob)
    {
        return CreateLinearVelocity(
            facingDirection,
            playerVelocity,
            ballVelocity,
            powerMultiplier,
            isLob,
            0f
        );
    }

    public Vector3 CreateLinearVelocity(
        int facingDirection,
        Vector3 playerVelocity,
        Vector3 ballVelocity,
        float powerMultiplier,
        bool isLob,
        float normalizedCharge)
    {
        Vector3 direction = _chargeControlsHeight
            ? CreateChargedHeightDirection(facingDirection, normalizedCharge)
            : new Vector3(
                NormalizeFacingDirection(facingDirection),
                isLob ? _lobUpwardInfluence : _upwardInfluence,
                0f
            ).normalized;
        Vector3 inheritedVelocity = ToGameplayPlane(playerVelocity) * _playerVelocityInfluence;
        float ballSpeedAlongHitDirection = Mathf.Abs(Vector3.Dot(ToGameplayPlane(ballVelocity), direction));
        float ballVelocityBonus = Mathf.Min(ballSpeedAlongHitDirection * _ballVelocityInfluence, _maxBallVelocityBonus);

        return direction * ((_speed * Mathf.Max(0f, powerMultiplier)) + ballVelocityBonus) + inheritedVelocity;
    }

    public Vector3 CreateAngularVelocity(Vector3 linearVelocity)
    {
        return Vector3.forward * (-linearVelocity.x * _spin);
    }

    private static int NormalizeFacingDirection(int facingDirection)
    {
        return facingDirection < 0 ? -1 : 1;
    }

    private Vector3 CreateChargedHeightDirection(int facingDirection, float normalizedCharge)
    {
        float minimumAngle = Mathf.Atan(Mathf.Max(0f, _upwardInfluence)) * Mathf.Rad2Deg;
        float maximumAngle = Mathf.Clamp(
            GameParameterSessionValues.GetValue(GameParameterId.BallMaxLobAngle),
            minimumAngle,
            GameParameterDefinitions.MaxBallMaxLobAngle
        );
        float angle = Mathf.Lerp(minimumAngle, maximumAngle, Mathf.Clamp01(normalizedCharge)) * Mathf.Deg2Rad;
        float horizontalDirection = NormalizeFacingDirection(facingDirection);

        return new Vector3(horizontalDirection * Mathf.Cos(angle), Mathf.Sin(angle), 0f);
    }

    private static Vector3 ToGameplayPlane(Vector3 value)
    {
        value.z = 0f;
        return value;
    }
}
