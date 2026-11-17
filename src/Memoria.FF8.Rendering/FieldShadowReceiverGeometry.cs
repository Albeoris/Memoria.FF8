using System.Numerics;
using Memoria.FF8.Mods;

namespace Memoria.FF8.Rendering;

internal sealed class FieldShadowReceiverGeometry
{
    private const float PlaneTolerance = 0.01f;
    private readonly Vector3 u;
    private readonly Vector3 v;
    private readonly Vector3 normal;
    private readonly Vector3 center;
    private readonly float halfWidth;
    private readonly float halfExtent;
    internal Vector3[] Corners { get; }

    internal FieldShadowReceiverGeometry(FieldShadowReceiver receiver)
    {
        (u, v) = receiver.Axes();
        normal = Vector3.Cross(u, v);
        center = receiver.Center;
        halfWidth = receiver.Width * 0.5f;
        halfExtent = receiver.Extent * 0.5f;
        Corners = receiver.Corners();
    }

    internal bool IsWithinReach(FieldSpotLight lamp, bool wall)
    {
        Vector3 fromCenter = lamp.Position - center;
        float planeDistance = Vector3.Dot(fromCenter, normal);
        if (wall ? MathF.Abs(planeDistance) <= PlaneTolerance : planeDistance <= PlaneTolerance)
        {
            return false;
        }
        float closestU = Math.Clamp(Vector3.Dot(fromCenter, u), -halfWidth, halfWidth);
        float closestV = Math.Clamp(Vector3.Dot(fromCenter, v), -halfExtent, halfExtent);
        return (fromCenter - u * closestU - v * closestV).Length() < lamp.Range;
    }

    internal bool CanReceiveShadow(Vector3 lamp, Vector3[] caster)
    {
        float side = MathF.Sign(Vector3.Dot(lamp - center, normal));
        if (caster.All(point => Vector3.Dot(point - center, normal) * side < 0))
        {
            return false;
        }
        for (int index = 0; index < Corners.Length; index++)
        {
            Vector3 a = Corners[index] - lamp;
            Vector3 b = Corners[(index + 1) % Corners.Length] - lamp;
            Vector3 inward = Vector3.Cross(a, b) * -side;
            if (caster.All(point => Vector3.Dot(point - lamp, inward) < 0))
            {
                return false;
            }
        }
        return true;
    }

    internal void SubtractFrom(List<Vector3[]> polygons)
    {
        for (int index = polygons.Count - 1; index >= 0; index--)
        {
            Vector3[] polygon = polygons[index];
            if (!OverlapsCoplanar(polygon))
            {
                continue;
            }
            polygons.RemoveAt(index);
            // Retain the outside of each edge from the portion inside all previous edges.
            // The resulting convex polygons do not cover one another.
            (Vector3 Normal, float Distance)[] edges = [(u, halfWidth), (-u, halfWidth), (v, halfExtent), (-v, halfExtent)];
            foreach ((Vector3 axis, float extent) in edges)
            {
                List<Vector3> inside = [];
                List<Vector3> outside = [];
                Vector3 previous = polygon[^1];
                float previousDistance = Vector3.Dot(previous - center, axis) - extent;
                foreach (Vector3 point in polygon)
                {
                    float distance = Vector3.Dot(point - center, axis) - extent;
                    if ((previousDistance > 0) != (distance > 0))
                    {
                        Vector3 intersection = Vector3.Lerp(previous, point, previousDistance / (previousDistance - distance));
                        inside.Add(intersection);
                        outside.Add(intersection);
                    }
                    if (distance > 0)
                    {
                        outside.Add(point);
                    }
                    else
                    {
                        inside.Add(point);
                    }
                    previous = point;
                    previousDistance = distance;
                }
                if (outside.Count >= 3)
                {
                    polygons.Add(outside.ToArray());
                }
                if (inside.Count < 3)
                {
                    break;
                }
                polygon = inside.ToArray();
            }
        }
    }

    private bool OverlapsCoplanar(Vector3[] polygon)
    {
        float minU = float.PositiveInfinity;
        float maxU = float.NegativeInfinity;
        float minV = float.PositiveInfinity;
        float maxV = float.NegativeInfinity;
        foreach (Vector3 point in polygon)
        {
            Vector3 local = point - center;
            if (MathF.Abs(Vector3.Dot(local, normal)) > PlaneTolerance)
            {
                return false;
            }
            float coordinateU = Vector3.Dot(local, u);
            float coordinateV = Vector3.Dot(local, v);
            minU = MathF.Min(minU, coordinateU);
            maxU = MathF.Max(maxU, coordinateU);
            minV = MathF.Min(minV, coordinateV);
            maxV = MathF.Max(maxV, coordinateV);
        }
        return minU < halfWidth && maxU > -halfWidth && minV < halfExtent && maxV > -halfExtent;
    }
}
