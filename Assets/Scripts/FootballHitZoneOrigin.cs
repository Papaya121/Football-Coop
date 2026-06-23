using System;
using UnityEngine;

[Serializable]
public sealed class FootballHitZoneOrigin
{
    [SerializeField] private bool _useColliderAnchor = true;
    [SerializeField] private Collider _collider;
    [SerializeField] private Vector3 _boundsAnchor = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private Vector3 _offset = Vector3.zero;

    public FootballHitZoneOrigin()
    {
    }

    public FootballHitZoneOrigin(Vector3 boundsAnchor, Vector3 offset)
    {
        _useColliderAnchor = true;
        _boundsAnchor = boundsAnchor;
        _offset = offset;
    }

    public Vector3 GetPosition(Component owner, Transform explicitOrigin, Vector3 fallbackLocalOffset)
    {
        if (explicitOrigin != null)
            return explicitOrigin.position;

        if (_useColliderAnchor && TryGetCollider(owner, out Collider collider))
            return GetBoundsPoint(collider.bounds) + GetOwnerOffset(owner);

        if (owner != null)
            return owner.transform.TransformPoint(fallbackLocalOffset);

        return fallbackLocalOffset;
    }

    public void ResolveDefaultCollider(Component owner)
    {
        if (_collider != null)
            return;

        if (TryGetCollider(owner, out Collider collider))
            _collider = collider;
    }

    private bool TryGetCollider(Component owner, out Collider collider)
    {
        if (_collider != null && _collider.enabled)
        {
            collider = _collider;
            return true;
        }

        if (owner == null)
        {
            collider = null;
            return false;
        }

        if (owner.TryGetComponent(out Collider directCollider) && directCollider.enabled)
        {
            collider = directCollider;
            return true;
        }

        Collider[] childColliders = owner.GetComponentsInChildren<Collider>();

        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider childCollider = childColliders[i];

            if (childCollider == null || !childCollider.enabled)
                continue;

            collider = childCollider;
            return true;
        }

        collider = null;
        return false;
    }

    private Vector3 GetBoundsPoint(Bounds bounds)
    {
        return new Vector3(
            Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(_boundsAnchor.x)),
            Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(_boundsAnchor.y)),
            Mathf.Lerp(bounds.min.z, bounds.max.z, Mathf.Clamp01(_boundsAnchor.z))
        );
    }

    private Vector3 GetOwnerOffset(Component owner)
    {
        if (owner == null)
            return _offset;

        return owner.transform.TransformVector(_offset);
    }
}
