using System.Numerics;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private readonly Dictionary<string, Vector4> actorBounds = [];

    /// <summary>Last rendered body bounds in output pixels, as left, top, right and bottom.</summary>
    public bool TryGetActorBounds(string actorId, out Vector4 bounds) => actorBounds.TryGetValue(actorId, out bounds);

    private void ObserveActorTriangle(string? actorId, PrimitiveVertex a, PrimitiveVertex b, PrimitiveVertex c)
    {
        if (actorId is null)
        {
            return;
        }
        ObserveActorPoint(actorId, new(a.X, a.Y));
        ObserveActorPoint(actorId, new(b.X, b.Y));
        ObserveActorPoint(actorId, new(c.X, c.Y));
    }

    private void ObserveActorPoint(string actorId, Vector2 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
        {
            return;
        }
        Vector4 added = new(point.X, point.Y, point.X, point.Y);
        if (actorBounds.TryGetValue(actorId, out Vector4 bounds))
        {
            added = new(Math.Min(bounds.X, point.X), Math.Min(bounds.Y, point.Y), Math.Max(bounds.Z, point.X), Math.Max(bounds.W, point.Y));
        }
        actorBounds[actorId] = added;
    }
}
