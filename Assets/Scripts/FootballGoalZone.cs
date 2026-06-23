using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class FootballGoalZone : MonoBehaviour
{
    [SerializeField] private FootballTeamSide _defendingSide;

    public event Action<FootballGoalZone, FootballBall> BallEntered;

    public FootballTeamSide DefendingSide => _defendingSide;

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolveBall(other, out FootballBall ball))
            return;

        BallEntered?.Invoke(this, ball);
    }

    private static bool TryResolveBall(Collider collider, out FootballBall ball)
    {
        ball = null;

        if (collider == null)
            return false;

        if (collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent(out ball))
            return true;

        if (collider.TryGetComponent(out ball))
            return true;

        ball = collider.GetComponentInParent<FootballBall>();
        return ball != null;
    }
}
