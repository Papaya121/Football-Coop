using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
[RequireComponent(typeof(Rigidbody))]
public sealed class FootballBallHeader : MonoBehaviour
{
    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private Transform _headerOrigin;
    [SerializeField] private Vector3 _originOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private FootballHitZoneOrigin _zoneOrigin = new FootballHitZoneOrigin(new Vector3(0.5f, 0.825f, 0.5f), Vector3.zero);
    [SerializeField] private LayerMask _ballMask = ~0;
    [SerializeField] private FootballBallHeaderProfile _headerProfile = new FootballBallHeaderProfile();

    private readonly Collider[] _overlapResults = new Collider[8];

    private Rigidbody _rigidbody;
    private float _nextHeaderTime;

    public event Action HeaderAttempted;
    public event Action Headed;

    public Vector3 ZoneOrigin => GetOrigin();
    public float ZoneRange => _headerProfile != null ? _headerProfile.Range : 0f;
    public float ZoneAngle => _headerProfile != null ? _headerProfile.MaxHeaderAngle : 0f;
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

    public bool TryHeader()
    {
        EnsureProfile();
        HeaderAttempted?.Invoke();

        if (Time.time < _nextHeaderTime)
            return false;

        if (!TryFindBall(out FootballBall ball))
            return false;

        Vector3 linearVelocity = _headerProfile.CreateLinearVelocity(_controller.FacingDirection, _rigidbody.linearVelocity);
        Vector3 angularVelocity = _headerProfile.CreateAngularVelocity(linearVelocity);

        ball.ApplyHeader(linearVelocity, angularVelocity, _headerProfile.ReceptionSuppressionTime);
        _nextHeaderTime = Time.time + _headerProfile.Cooldown;
        Headed?.Invoke();

        return true;
    }

    private bool TryFindBall(out FootballBall selectedBall)
    {
        selectedBall = null;

        Vector3 origin = GetOrigin();
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            _headerProfile.Range,
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

            if (!_headerProfile.CanReach(origin, _controller.FacingDirection, interactionPoint))
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
        return _zoneOrigin.GetPosition(this, _headerOrigin, _originOffset);
    }

    private void EnsureOrigin()
    {
        if (_zoneOrigin == null)
            _zoneOrigin = new FootballHitZoneOrigin(new Vector3(0.5f, 0.825f, 0.5f), Vector3.zero);
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
        if (_headerProfile == null)
            _headerProfile = new FootballBallHeaderProfile();
    }

    private static bool TryResolveBall(Collider collider, out FootballBall ball)
    {
        if (collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent(out ball))
            return true;

        ball = collider.GetComponentInParent<FootballBall>();
        return ball != null;
    }
}
