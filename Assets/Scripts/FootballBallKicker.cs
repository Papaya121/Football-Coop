using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
[RequireComponent(typeof(Rigidbody))]
public sealed class FootballBallKicker : MonoBehaviour
{
    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private Transform _kickOrigin;
    [SerializeField] private Vector3 _originOffset = new Vector3(0f, 0.65f, 0f);
    [SerializeField] private FootballHitZoneOrigin _zoneOrigin = new FootballHitZoneOrigin(new Vector3(0.5f, 0.325f, 0.5f), Vector3.zero);
    [SerializeField] private LayerMask _ballMask = ~0;
    [SerializeField] private FootballBallKickProfile _kickProfile = new FootballBallKickProfile();

    private readonly Collider[] _overlapResults = new Collider[8];

    private Rigidbody _rigidbody;
    private float _nextKickTime;

    public event Action KickAttempted;
    public event Action Kicked;

    public Vector3 ZoneOrigin => GetOrigin();
    public float ZoneRange => _kickProfile != null ? _kickProfile.Range : 0f;
    public float ZoneAngle => _kickProfile != null ? _kickProfile.MaxKickAngle : 0f;
    public int FacingDirection => _controller != null ? _controller.FacingDirection : 1;

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

    public bool TryKick()
    {
        EnsureProfile();
        KickAttempted?.Invoke();

        if (Time.time < _nextKickTime)
            return false;

        if (!TryFindBall(out FootballBall ball))
            return false;

        Vector3 linearVelocity = _kickProfile.CreateLinearVelocity(_controller.FacingDirection, _rigidbody.linearVelocity, ball.LinearVelocity);
        Vector3 angularVelocity = _kickProfile.CreateAngularVelocity(linearVelocity);

        ball.ApplyKick(linearVelocity, angularVelocity, _kickProfile.ReceptionSuppressionTime);
        _nextKickTime = Time.time + _kickProfile.Cooldown;
        Kicked?.Invoke();

        return true;
    }

    private bool TryFindBall(out FootballBall selectedBall)
    {
        selectedBall = null;

        Vector3 origin = GetOrigin();
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            _kickProfile.Range,
            _overlapResults,
            _ballMask,
            QueryTriggerInteraction.Ignore
        );

        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _overlapResults[i];

            if (hit == null || !TryResolveBall(hit, out FootballBall ball))
                continue;

            Vector3 interactionPoint = ball.GetClosestInteractionPoint(origin);

            if (!_kickProfile.CanReach(origin, _controller.FacingDirection, interactionPoint))
                continue;

            float sqrDistance = (interactionPoint - origin).sqrMagnitude;

            if (sqrDistance >= bestSqrDistance)
                continue;

            selectedBall = ball;
            bestSqrDistance = sqrDistance;
        }

        return selectedBall != null;
    }

    private Vector3 GetOrigin()
    {
        EnsureOrigin();
        return _zoneOrigin.GetPosition(this, _kickOrigin, _originOffset);
    }

    private void EnsureOrigin()
    {
        if (_zoneOrigin == null)
            _zoneOrigin = new FootballHitZoneOrigin(new Vector3(0.5f, 0.325f, 0.5f), Vector3.zero);
    }

    private void ResolveReferences()
    {
        if (_controller == null)
            _controller = GetComponent<FootballPlayerController>();

        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        EnsureOrigin();
        _zoneOrigin.ResolveDefaultCollider(this);
    }

    private void EnsureProfile()
    {
        if (_kickProfile == null)
            _kickProfile = new FootballBallKickProfile();
    }

    private static bool TryResolveBall(Collider collider, out FootballBall ball)
    {
        if (collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent(out ball))
            return true;

        ball = collider.GetComponentInParent<FootballBall>();
        return ball != null;
    }
}
