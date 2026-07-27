using UnityEngine;

// [DisallowMultipleComponent]
public sealed class FootballSoundSurface : MonoBehaviour
{
    [SerializeField] private string _soundId = FootballSoundIds.Crossbar;
    [SerializeField, Min(0f)] private float _minImpulse = 0.25f;
    [SerializeField, Min(0f)] private float _cooldown = 0.08f;
    [SerializeField, Range(0f, 1f)] private float _volumeMultiplier = 1f;

    [Header("Ball speed volume")]
    [SerializeField] private bool _useBallSpeedVolume;
    [SerializeField]
    private AnimationCurve _volumeByBallSpeed =
        AnimationCurve.Linear(0f, 0f, 24f, 1f);

    private float _nextPlayTime;

    public string SoundId => _soundId;

    public bool TryPlay(Collision collision)
    {
        if (collision == null || Time.time < _nextPlayTime)
            return false;

        if (collision.impulse.magnitude < _minImpulse)
            return false;

        float volume = GetVolume(collision);

        if (volume <= 0f)
            return false;

        Vector3 position = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        bool played = FootballSoundPlayer.TryPlay(_soundId, position, volume);

        if (played)
            _nextPlayTime = Time.time + _cooldown;

        return played;
    }

    private float GetVolume(Collision collision)
    {
        if (!_useBallSpeedVolume || _volumeByBallSpeed == null)
            return _volumeMultiplier;

        float ballSpeed = collision.relativeVelocity.magnitude;
        float speedMultiplier = Mathf.Clamp01(_volumeByBallSpeed.Evaluate(ballSpeed));
        return _volumeMultiplier * speedMultiplier;
    }
}
