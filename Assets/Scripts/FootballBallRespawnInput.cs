using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballBall))]
public sealed class FootballBallRespawnInput : MonoBehaviour
{
    [SerializeField] private FootballBall _ball;
    [SerializeField] private Vector3 _respawnPosition = new Vector3(0f, 4f, 0f);
    [SerializeField] private Key _respawnKey = Key.T;
    [SerializeField] private bool _clearTrails = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !keyboard[_respawnKey].wasPressedThisFrame)
            return;

        RespawnBall();
    }

    private void RespawnBall()
    {
        if (_ball == null)
            return;

        _ball.Respawn(_respawnPosition);

        if (!_clearTrails)
            return;

        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>())
            trail.Clear();
    }

    private void ResolveReferences()
    {
        if (_ball == null)
            _ball = GetComponent<FootballBall>();
    }
}
