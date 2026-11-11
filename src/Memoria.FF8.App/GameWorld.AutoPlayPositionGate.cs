using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal bool WalkToAutoPlayPositionGate(NativePlayerPositionGate gate, Action onProgress, AutoPlayIdleDeadline? idleDeadline = null)
    {
        if (Field is null || Events.PlayerActor is not { } player)
        {
            return false;
        }
        FieldWalkmesh mesh = Field.Walkmesh;
        Vector2 source = new(player.X, player.Y);
        float stride = Events.AutoPlayMovementStepDistance;
        bool advanceFollowers = gate.OwnerObjectId != player.ObjectId && gate.Admits(player.X, player.Y, player.Z);
        var candidates = AutoPlayReachableTriangles(mesh, player.WalkmeshTriangle, Events.Scene.LockedTriangles)
            .Select(index => new WalkmeshPosition((mesh.Triangles[index].A + mesh.Triangles[index].B + mesh.Triangles[index].C) / 3, index))
            .Where(point => gate.Admits(point.Position.X, point.Position.Y, point.Position.Z))
            // Followers consume the party trail during movement callbacks. Once the leader
            // is inside the region, another real walk must carry the delayed party through it.
            .Where(point => !advanceFollowers || Vector2.DistanceSquared(source, new(point.Position.X, point.Position.Y)) > stride * stride)
            .OrderByDescending(point => gate.Admits(point.Position.X, point.Position.Y, point.Position.Z, stride))
            .ThenBy(point => (advanceFollowers ? -1 : 1) * Vector2.DistanceSquared(source, new(point.Position.X, point.Position.Y)))
            .ThenBy(point => point.Triangle);
        foreach (WalkmeshPosition candidate in candidates.Take(8))
        {
            Vector3 p = candidate.Position;
            AutoPlayTarget target = new("position-gate", AutoPlayTargetKind.Talk, p.X, p.Y, p.Z, candidate.Triangle);
            if (TryFindAutoPlayWalkingRoute(target, out AutoPlayWalkingRoute route, idleDeadline))
            {
                AutoPlayWalkResult result = WalkAutoPlayRoute(route, onProgress);
                return result.Interrupted || result.Arrived;
            }
        }
        return false;
    }
}
