using UnityEngine;

[DisallowMultipleComponent]
public sealed class CanvasBillboard : MonoBehaviour
{
    [SerializeField] private CapsuleCollider _playerCollider;
    [SerializeField] private float _speedY = 1;
    private Camera _targetCamera;
    private Canvas _canvas;

    private Transform _debug;

    void Awake()
    {
        // _debug = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        // Destroy(_debug.gameObject.GetComponent<Collider>());
        // _debug.transform.localScale = Vector3.one * 0.1f;
        _canvas = GetComponent<Canvas>();
    }

    public void SetTargetCamera(Camera targetCamera)
    {
        _targetCamera = targetCamera;
    }

    private void LateUpdate()
    {
        if (_targetCamera == null)
            return;

        transform.rotation = _targetCamera.transform.rotation;
    }

    private void Update()
    {
        if (!_playerCollider)
            return;

        var pos = transform.localPosition;

        var targetY = _playerCollider.center.y + _playerCollider.height * 0.5f;

        pos.y = Mathf.MoveTowards(
            pos.y,
            targetY,
            _speedY * Time.deltaTime
        );

        transform.localPosition = pos;
    }
}
