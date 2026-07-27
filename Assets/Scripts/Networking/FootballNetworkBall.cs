using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(FootballBall))]
public sealed class FootballNetworkBall : NetworkBehaviour
{
    [SerializeField] private FootballBall _ball;
    [SerializeField] private FootballBallRespawnInput _respawnInput;
    private float _nextClientDiagnosticsTime;

    private void Awake()
    {
        ResolveReferences();
    }

    public override void OnStartClient()
    {
        ResolveReferences();

        if (_respawnInput != null)
            _respawnInput.enabled = false;

        if (!isServer && _ball != null)
            _ball.enabled = false;

        FootballNetworkDiagnostics.Write(
            "BALL-CLIENT",
            $"Started. netId={netId}; scene={gameObject.scene.name}; {DescribeClientState()}"
        );
        FootballNetworkManager.Instance?.BindClientBall(transform);
    }

    private void Update()
    {
        if (!isClient || Time.unscaledTime < _nextClientDiagnosticsTime)
            return;

        _nextClientDiagnosticsTime = Time.unscaledTime + 1f;
        FootballNetworkDiagnostics.Write("BALL-CLIENT-STATE", $"netId={netId}; {DescribeClientState()}");
    }

    private void ResolveReferences()
    {
        if (_ball == null)
            _ball = GetComponent<FootballBall>();
        if (_respawnInput == null)
            _respawnInput = GetComponent<FootballBallRespawnInput>();
    }

    private string DescribeClientState()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        NetworkRigidbodyUnreliable networkRigidbody = GetComponent<NetworkRigidbodyUnreliable>();
        string bodyState = body == null
            ? "rb=missing"
            : $"rbKinematic={body.isKinematic}; rbPos={body.position}; velocity={body.linearVelocity}";
        string syncState = networkRigidbody == null
            ? "sync=missing"
            : $"syncEnabled={networkRigidbody.enabled}; updateMethod={networkRigidbody.updateMethod}; " +
              $"clientSnapshots={networkRigidbody.clientSnapshots.Count}";

        return $"transformPos={transform.position}; {bodyState}; {syncState}";
    }
}
