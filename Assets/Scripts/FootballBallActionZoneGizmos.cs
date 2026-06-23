using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballBallActionZoneGizmos : MonoBehaviour
{
    private const int CircleSegments = 48;

    [SerializeField] private bool _drawHitZones = true;
    [SerializeField] private bool _drawOnlyWhenSelected = false;
    [SerializeField] private bool _drawKickZone = true;
    [SerializeField] private bool _drawHeaderZone = true;
    [SerializeField] private bool _drawBicycleKickZone = true;
    [SerializeField] private FootballBallKicker _kicker;
    [SerializeField] private FootballBallHeader _header;
    [SerializeField] private FootballBallBicycleKicker _bicycleKicker;
    [SerializeField] private Color _kickColor = new Color(1f, 0.72f, 0.08f, 1f);
    [SerializeField] private Color _headerColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color _bicycleKickColor = new Color(1f, 0.25f, 0.9f, 1f);

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnDrawGizmos()
    {
        if (_drawOnlyWhenSelected)
            return;

        DrawHitZones();
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawOnlyWhenSelected)
            return;

        DrawHitZones();
    }

    private void DrawHitZones()
    {
        if (!_drawHitZones)
            return;

        ResolveReferences();

        if (_drawKickZone && _kicker != null)
            DrawForwardZone(_kicker.ZoneOrigin, _kicker.ZoneRange, _kicker.ZoneAngle, _kicker.FacingDirection, _kickColor);

        if (_drawHeaderZone && _header != null)
            DrawForwardZone(_header.ZoneOrigin, _header.ZoneRange, _header.ZoneAngle, _header.FacingDirection, _headerColor);

        if (_drawBicycleKickZone && _bicycleKicker != null)
            DrawBicycleZone(_bicycleKicker.ZoneOrigin, _bicycleKicker.ZoneRange, _bicycleKicker.MinimumBallHeightFromOrigin, _bicycleKickColor);
    }

    private void ResolveReferences()
    {
        if (_kicker == null)
            _kicker = GetComponent<FootballBallKicker>();

        if (_header == null)
            _header = GetComponent<FootballBallHeader>();

        if (_bicycleKicker == null)
            _bicycleKicker = GetComponent<FootballBallBicycleKicker>();
    }

    private static void DrawForwardZone(Vector3 origin, float range, float angle, int facingDirection, Color color)
    {
        if (range <= 0f)
            return;

        Gizmos.color = color;
        DrawCircle(origin, range);

        Vector3 forward = Vector3.right * NormalizeFacingDirection(facingDirection);
        Vector3 upperLimit = Quaternion.AngleAxis(angle, Vector3.forward) * forward;
        Vector3 lowerLimit = Quaternion.AngleAxis(-angle, Vector3.forward) * forward;

        Gizmos.DrawLine(origin, origin + upperLimit.normalized * range);
        Gizmos.DrawLine(origin, origin + lowerLimit.normalized * range);
    }

    private static void DrawBicycleZone(Vector3 origin, float range, float minimumBallHeightFromOrigin, Color color)
    {
        if (range <= 0f)
            return;

        Gizmos.color = color;
        DrawCircle(origin, range);

        float y = origin.y + minimumBallHeightFromOrigin;
        Vector3 left = new Vector3(origin.x - range, y, origin.z);
        Vector3 right = new Vector3(origin.x + range, y, origin.z);

        Gizmos.DrawLine(left, right);
    }

    private static void DrawCircle(Vector3 origin, float radius)
    {
        Vector3 previous = origin + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= CircleSegments; i++)
        {
            float angle = Mathf.PI * 2f * i / CircleSegments;
            Vector3 current = origin + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }

    private static int NormalizeFacingDirection(int facingDirection)
    {
        return facingDirection < 0 ? -1 : 1;
    }
}
