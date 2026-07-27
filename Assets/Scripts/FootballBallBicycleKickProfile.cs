using System;
using UnityEngine;

[Serializable]
public sealed class FootballBallBicycleKickProfile
{
    [SerializeField, Min(0.1f)] private float _range = 1.25f;
    [SerializeField, Min(-2f)] private float _minimumBallHeightFromOrigin = -0.25f;
    [SerializeField, Min(0f)] private float _speed = 16f;
    [SerializeField, Range(0f, 1f)] private float _upwardInfluence = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _playerVelocityInfluence = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _ballVelocityInfluence = 0.3f;
    [SerializeField, Min(0f)] private float _maxBallVelocityBonus = 7f;
    [SerializeField, Range(0.01f, 1f)] private float _backInputThreshold = 0.5f;
    [SerializeField, Min(0f)] private float _spin = 9f;
    [SerializeField, Min(0f)] private float _cooldown = 0.35f;
    [SerializeField, Min(0f)] private float _receptionSuppressionTime = 0.22f;
    [SerializeField, Min(0f)] private float _ballCollisionIgnoreTime = 0.25f;

    public float Range => _range;
    public float MinimumBallHeightFromOrigin => _minimumBallHeightFromOrigin;
    public float Cooldown => _cooldown;
    public float ReceptionSuppressionTime => _receptionSuppressionTime;
    public float BallCollisionIgnoreTime => _ballCollisionIgnoreTime;

    public bool CanStart(FootballPlayerController controller)
    {
        if (controller == null)
            return false;

        return CanStart(controller, controller.FacingDirection);
    }

    public bool CanStart(FootballPlayerController controller, int facingDirection)
    {
        if (controller == null || controller.IsGrounded)
            return false;

        float horizontalInput = controller.MoveInput.x;

        if (Mathf.Abs(horizontalInput) < _backInputThreshold)
            return false;

        return Mathf.Sign(horizontalInput) != NormalizeFacingDirection(facingDirection);
    }

    public bool CanReach(Vector3 origin, Vector3 ballPosition)
    {
        Vector3 toBall = ToGameplayPlane(ballPosition - origin);

        if (toBall.sqrMagnitude > _range * _range)
            return false;

        return ballPosition.y - origin.y >= _minimumBallHeightFromOrigin;
    }

    public Vector3 CreateLinearVelocity(int facingDirection, Vector3 playerVelocity)
    {
        return CreateLinearVelocity(facingDirection, playerVelocity, Vector3.zero);
    }

    public Vector3 CreateLinearVelocity(int facingDirection, Vector3 playerVelocity, Vector3 ballVelocity)
    {
        Vector3 direction = new Vector3(-NormalizeFacingDirection(facingDirection), _upwardInfluence, 0f).normalized;
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
