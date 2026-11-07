using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private sealed record AutoPlayFootStep(int TargetX, int TargetZ)
    {
        public int? OrdinaryStride { get; set; }
    }

    private AutoPlayFootStep? autoPlayFootStep;

    internal PlayerActionResult ApplyAutoPlayFootMove(int targetX, int targetZ, Func<PlayerActionResult> apply, out int? ordinaryStride)
    {
        AutoPlayFootStep step = new(targetX, targetZ);
        AutoPlayFootStep? previous = autoPlayFootStep;
        autoPlayFootStep = step;
        try
        {
            return apply();
        }
        finally
        {
            ordinaryStride = step.OrdinaryStride;
            autoPlayFootStep = previous;
        }
    }

    private bool TryAccelerateAutoPlayFootMove(WorldState before, NativeFootWorldRuntimeState foot, int nativeX, int nativeZ, int ordinaryX, int ordinaryZ, out int fastX, out int fastZ)
    {
        fastX = ordinaryX;
        fastZ = ordinaryZ;
        if (autoPlayFootStep is not { } step || foot.EntityType is not (0 or 1) || foot.StationTrains is not null || foot.ParkedCar is not null || before.NativeGardenRuntime is not null || before.NativeRagnarokRuntime is not null || before.EventUi is { ScriptMode: not 0 } || foot.GroundCache?.Alternatives is not { Length: > 0 } alternatives)
        {
            return false;
        }
        int nativeStrideSquared = nativeX * nativeX + nativeZ * nativeZ;
        if (nativeStrideSquared == 0)
        {
            return false;
        }
        long towardTarget = ((long)step.TargetX - before.X) * nativeX - ((long)step.TargetZ - before.Z) * nativeZ;
        // Leave the final native stride to the ordinary waypoint approach.
        int multiplier = (int)Math.Min(PlayerMovementSpeed.AutoPlayTranslationMultiplier, towardTarget / nativeStrideSquared - 1);
        if (multiplier < 2)
        {
            return false;
        }
        NativeWorldGroundIdentity identity = alternatives[0].CurrentGround;
        if (alternatives.Any(alternative => alternative.CurrentGround != identity || alternative.Entries is not { Length: > 0 } || alternative.Entries[0] != identity))
        {
            return false;
        }
        WorldTriangle triangle = assets.ResolveNativeGroundIdentity(identity, replacements);
        if (triangle.FieldEntrance || (triangle.VertexFlags & NativeWorldTerrain.PermissionMask(foot.EntityType)) == 0)
        {
            return false;
        }
        int radius = NativeWorldTerrain.ProbeRadius(foot.EntityType);
        int cellX = before.X >> 11;
        int cellZ = before.Z >> 11;
        NativeWorldGroundIdentity[] cache = alternatives[0].Entries;
        if (!ContainsAutoPlayFootprint(identity, cache, before.X, before.Z, radius, cellX, cellZ))
        {
            return false;
        }
        int ordinaryStride = (int)Math.Ceiling(Math.Sqrt(ordinaryX * ordinaryX + ordinaryZ * ordinaryZ));
        for (; multiplier >= 2; multiplier--)
        {
            int moveX = multiplier * nativeX;
            int moveZ = multiplier * nativeZ;
            int distance = (int)Math.Ceiling(Math.Sqrt(moveX * moveX + moveZ * moveZ));
            if (distance <= ordinaryStride || !ContainsAutoPlayFootprint(identity, cache, before.X + moveX, before.Z - moveZ, radius, cellX, cellZ) || !assets.WorldEvents.HasConstantPositionPredicates(before.X, before.Z, before.X + moveX, before.Z - moveZ) || HasNearbyAutoPlayWorldObject(foot, before.X, before.Z, distance))
            {
                continue;
            }
            // A single convex polygon contains both footprints and therefore the entire swept footprint.
            // The leading retained cache entry keeps native candidate zero on that polygon.
            step.OrdinaryStride = ordinaryStride;
            fastX = moveX;
            fastZ = moveZ;
            return true;
        }
        return false;
    }

    private bool ContainsAutoPlayFootprint(NativeWorldGroundIdentity identity, NativeWorldGroundIdentity[] cache, int x, int z, int radius, int cellX, int cellZ)
    {
        return ((x - radius) >> 11) == cellX && ((x + radius) >> 11) == cellX && ((z - radius) >> 11) == cellZ && ((z + radius) >> 11) == cellZ
            && assets.LookupNativeGround(x - radius, z - radius, replacements, cache)?.Identity == identity && assets.LookupNativeGround(x - radius, z + radius, replacements, cache)?.Identity == identity
            && assets.LookupNativeGround(x + radius, z - radius, replacements, cache)?.Identity == identity && assets.LookupNativeGround(x + radius, z + radius, replacements, cache)?.Identity == identity;
    }

    private static bool HasNearbyAutoPlayWorldObject(NativeFootWorldRuntimeState foot, int x, int z, int distance)
    {
        if (foot.NormalObjects is not { } process)
        {
            return false;
        }
        // The original allocated-shape event radius is wider than the native contact broad phase.
        long clearance = 3000 + distance;
        long clearanceSquared = clearance * clearance;
        bool Nearby(NativeWorldPathPosition position)
        {
            int dx = unchecked((short)(position.X - x));
            int dz = unchecked((short)(position.Z + z));
            return (long)dx * dx + (long)dz * dz <= clearanceSquared;
        }
        for (int index = 0; index < process.Allocation.Objects.Length; index++)
        {
            if (index != process.Allocation.ControlledObjectIndex && Nearby(process.Allocation.Objects[index].Position))
            {
                return true;
            }
        }
        if (process.CactuarBody is { } cactuar && Nearby(cactuar.Position))
        {
            return true;
        }
        return BankNearby(process.Primary) || BankNearby(process.Alternate);

        bool BankNearby(NativeTrainRailBank? bank)
        {
            if (bank is null)
            {
                return false;
            }
            for (int index = 0; index < bank.Nodes.Length; index++)
            {
                if (Nearby(bank.Presentation[index].Projection?.Source ?? bank.Nodes[index].Position))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
