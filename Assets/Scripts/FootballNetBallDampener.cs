using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballNetBallDampener : MonoBehaviour
{
    [Header("Initial contact")]
    [SerializeField, Range(0f, 1f)] private float _linearVelocityMultiplier = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _angularVelocityMultiplier = 0.25f;

    [Header("Continuous contact")]
    [SerializeField, Range(0f, 1f)] private float _contactDamping = 0.8f;
    [SerializeField, Min(0f)] private float _stopSpeed = 0.1f;

    private void OnCollisionEnter(Collision collision)
    {
        if (!TryGetBallRigidbody(collision.collider, out Rigidbody ballRigidbody))
            return;

        ballRigidbody.linearVelocity *= _linearVelocityMultiplier;
        ballRigidbody.angularVelocity *= _angularVelocityMultiplier;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!TryGetBallRigidbody(collision.collider, out Rigidbody ballRigidbody))
            return;

        ballRigidbody.linearVelocity *= _contactDamping;
        ballRigidbody.angularVelocity *= _contactDamping;

        if (ballRigidbody.linearVelocity.sqrMagnitude <= _stopSpeed * _stopSpeed)
            ballRigidbody.linearVelocity = Vector3.zero;
    }

    private static bool TryGetBallRigidbody(Collider other, out Rigidbody ballRigidbody)
    {
        ballRigidbody = other != null ? other.attachedRigidbody : null;

        if (ballRigidbody == null)
            return false;

        return ballRigidbody.GetComponent<FootballBall>() != null;
    }
}
