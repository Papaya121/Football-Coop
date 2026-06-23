using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballBallActionDebugLogger : MonoBehaviour
{
    [SerializeField] private FootballBallKicker _kicker;
    [SerializeField] private FootballBallBicycleKicker _bicycleKicker;
    [SerializeField] private FootballBallHeader _header;
    [SerializeField] private bool _logSuccessfulActions = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (_kicker != null)
            _kicker.Kicked += OnKicked;

        if (_bicycleKicker != null)
            _bicycleKicker.BicycleKicked += OnBicycleKicked;

        if (_header != null)
            _header.Headed += OnHeaded;
    }

    private void OnDisable()
    {
        if (_kicker != null)
            _kicker.Kicked -= OnKicked;

        if (_bicycleKicker != null)
            _bicycleKicker.BicycleKicked -= OnBicycleKicked;

        if (_header != null)
            _header.Headed -= OnHeaded;
    }

    private void OnKicked()
    {
        LogAction("Kick");
    }

    private void OnBicycleKicked()
    {
        LogAction("Bicycle Kick");
    }

    private void OnHeaded()
    {
        LogAction("Header");
    }

    private void LogAction(string actionName)
    {
        if (!_logSuccessfulActions)
            return;

        Debug.Log($"{name}: {actionName}", this);
    }

    private void ResolveReferences()
    {
        if (_kicker == null)
            _kicker = GetComponent<FootballBallKicker>();

        if (_bicycleKicker == null)
            _bicycleKicker = GetComponent<FootballBallBicycleKicker>();

        if (_header == null)
            _header = GetComponent<FootballBallHeader>();
    }
}
