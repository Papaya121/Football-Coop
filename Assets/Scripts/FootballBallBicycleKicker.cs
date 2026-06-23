using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
[RequireComponent(typeof(Rigidbody))]
public sealed class FootballBallBicycleKicker : MonoBehaviour
{
    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private Transform _bicycleKickOrigin;
    [SerializeField] private Vector3 _originOffset = new Vector3(0f, 1.15f, 0f);
    [SerializeField] private FootballHitZoneOrigin _zoneOrigin = new FootballHitZoneOrigin(new Vector3(0.5f, 0.575f, 0.5f), Vector3.zero);
    [SerializeField] private LayerMask _ballMask = ~0;
    [SerializeField] private FootballBallBicycleKickProfile _bicycleKickProfile = new FootballBallBicycleKickProfile();

    private readonly Collider[] _overlapResults = new Collider[8];
    private readonly List<CollisionIgnorePair> _activeIgnoredCollisions = new List<CollisionIgnorePair>();

    private Rigidbody _rigidbody;
    private float _nextBicycleKickTime;

    public event Action BicycleKickAttempted;
    public event Action BicycleKicked;

    public Vector3 ZoneOrigin => GetOrigin();
    public float ZoneRange => _bicycleKickProfile != null ? _bicycleKickProfile.Range : 0f;
    public float MinimumBallHeightFromOrigin => _bicycleKickProfile != null ? _bicycleKickProfile.MinimumBallHeightFromOrigin : 0f;

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

    private void OnDisable()
    {
        RestoreAllIgnoredCollisions();
    }

    public bool CanAttemptBicycleKick()
    {
        EnsureProfile();
        return _bicycleKickProfile.CanStart(_controller);
    }

    public bool TryBicycleKick()
    {
        EnsureProfile();

        int capturedFacingDirection = _controller != null ? _controller.FacingDirection : 1;

        if (!_bicycleKickProfile.CanStart(_controller, capturedFacingDirection))
            return false;

        BicycleKickAttempted?.Invoke();

        if (Time.time < _nextBicycleKickTime)
            return false;

        if (!TryFindBall(out FootballBall ball))
            return false;

        IgnoreBallCollisionTemporarily(ball);

        Vector3 linearVelocity = _bicycleKickProfile.CreateLinearVelocity(capturedFacingDirection, _rigidbody.linearVelocity);
        Vector3 angularVelocity = _bicycleKickProfile.CreateAngularVelocity(linearVelocity);

        ball.ApplyBicycleKick(linearVelocity, angularVelocity, _bicycleKickProfile.ReceptionSuppressionTime);
        _nextBicycleKickTime = Time.time + _bicycleKickProfile.Cooldown;
        BicycleKicked?.Invoke();

        return true;
    }

    private void IgnoreBallCollisionTemporarily(FootballBall ball)
    {
        float ignoreTime = _bicycleKickProfile.BallCollisionIgnoreTime;

        if (ball == null || ignoreTime <= 0f)
            return;

        Collider[] playerColliders = GetComponentsInChildren<Collider>();
        Collider[] ballColliders = ball.GetComponentsInChildren<Collider>();
        List<CollisionIgnorePair> ignoredPairs = new List<CollisionIgnorePair>();

        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider == null || !playerCollider.enabled)
                continue;

            foreach (Collider ballCollider in ballColliders)
            {
                if (ballCollider == null || !ballCollider.enabled)
                    continue;

                Physics.IgnoreCollision(playerCollider, ballCollider, true);

                CollisionIgnorePair pair = new CollisionIgnorePair(playerCollider, ballCollider);
                ignoredPairs.Add(pair);
                _activeIgnoredCollisions.Add(pair);
            }
        }

        if (ignoredPairs.Count > 0)
            StartCoroutine(RestoreIgnoredCollisionsAfterDelay(ignoredPairs, ignoreTime));
    }

    private IEnumerator RestoreIgnoredCollisionsAfterDelay(List<CollisionIgnorePair> ignoredPairs, float delay)
    {
        yield return new WaitForSeconds(delay);

        RestoreIgnoredCollisions(ignoredPairs);
    }

    private void RestoreIgnoredCollisions(List<CollisionIgnorePair> ignoredPairs)
    {
        foreach (CollisionIgnorePair pair in ignoredPairs)
        {
            RestoreIgnoredCollision(pair);
            _activeIgnoredCollisions.Remove(pair);
        }
    }

    private void RestoreAllIgnoredCollisions()
    {
        foreach (CollisionIgnorePair pair in _activeIgnoredCollisions)
            RestoreIgnoredCollision(pair);

        _activeIgnoredCollisions.Clear();
    }

    private static void RestoreIgnoredCollision(CollisionIgnorePair pair)
    {
        if (pair.PlayerCollider == null || pair.BallCollider == null)
            return;

        Physics.IgnoreCollision(pair.PlayerCollider, pair.BallCollider, false);
    }

    private bool TryFindBall(out FootballBall selectedBall)
    {
        selectedBall = null;

        Vector3 origin = GetOrigin();
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            _bicycleKickProfile.Range,
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

            if (!_bicycleKickProfile.CanReach(origin, interactionPoint))
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
        return _zoneOrigin.GetPosition(this, _bicycleKickOrigin, _originOffset);
    }

    private void EnsureOrigin()
    {
        if (_zoneOrigin == null)
            _zoneOrigin = new FootballHitZoneOrigin(new Vector3(0.5f, 0.575f, 0.5f), Vector3.zero);
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
        if (_bicycleKickProfile == null)
            _bicycleKickProfile = new FootballBallBicycleKickProfile();
    }

    private static bool TryResolveBall(Collider collider, out FootballBall ball)
    {
        if (collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent(out ball))
            return true;

        ball = collider.GetComponentInParent<FootballBall>();
        return ball != null;
    }

    private readonly struct CollisionIgnorePair : IEquatable<CollisionIgnorePair>
    {
        public CollisionIgnorePair(Collider playerCollider, Collider ballCollider)
        {
            PlayerCollider = playerCollider;
            BallCollider = ballCollider;
        }

        public Collider PlayerCollider { get; }
        public Collider BallCollider { get; }

        public bool Equals(CollisionIgnorePair other)
        {
            return PlayerCollider == other.PlayerCollider && BallCollider == other.BallCollider;
        }

        public override bool Equals(object obj)
        {
            return obj is CollisionIgnorePair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((PlayerCollider != null ? PlayerCollider.GetHashCode() : 0) * 397) ^
                    (BallCollider != null ? BallCollider.GetHashCode() : 0);
            }
        }
    }
}
