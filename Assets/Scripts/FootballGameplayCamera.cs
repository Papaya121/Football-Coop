using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballGameplayCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _highTargetY = 10f;
    [SerializeField] private float _maxTargetY = 20f;
    [SerializeField] private float _highCameraYOffset = 2.5f;
    [SerializeField] private float _maxZoomOutDistance = 6f;
    [SerializeField, Min(0.01f)] private float _smoothTime = 0.25f;

    private Vector3 _baseLocalPosition;
    private Vector3 _velocity;
    private bool _hasBaseLocalPosition;

    private void Awake()
    {
        CaptureBaseLocalPosition();
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    private void LateUpdate()
    {
        CaptureBaseLocalPosition();

        Vector3 targetPosition = _baseLocalPosition;

        if (_target != null)
        {
            float heightT = Mathf.InverseLerp(_highTargetY, _maxTargetY, _target.position.y);
            targetPosition.y += _highCameraYOffset * heightT;
            targetPosition.z -= _maxZoomOutDistance * heightT;
        }

        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetPosition, ref _velocity, _smoothTime);
    }

    private void CaptureBaseLocalPosition()
    {
        if (_hasBaseLocalPosition)
            return;

        _baseLocalPosition = transform.localPosition;
        _hasBaseLocalPosition = true;
    }
}
