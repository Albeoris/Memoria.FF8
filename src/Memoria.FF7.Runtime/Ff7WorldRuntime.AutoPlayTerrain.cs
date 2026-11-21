using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldTerrainPoint(int X, int Z, int Height, int Terrain, Ff7WorldTerrainReference Triangle, PcWorldTriangle Face);

public sealed partial class Ff7WorldRuntime
{
    internal bool? ReadAutoPlayCurrentModeTerrainEligibility(Ff7AutoPlayWorldScriptObservation target)
    {
        if (Resources.World == 0 && target.Complete && entities.TryGetValue(controlledModel, out Ff7WorldEntity? aircraft) && IsAutoPlayAircraft(aircraft) && (aircraft.Flags & 128) != 0 && vehicle is not { DismountPhase: 2 })
        {
            return false;
        }
        Ff7WorldBridgeState bridge = input.Bridge ?? initialBridge;
        if (Resources.World != 0 || !target.Complete || !entities.TryGetValue(controlledModel, out Ff7WorldEntity? player) || (player.Flags & 128) != 0 || player.AttachedModel is not null || vehicle is { FlightState: not 0 } or { DismountPhase: not 0 } || vehicle?.TinyBroncoFlightEnabled == true || !HasInactiveWorldBridgeTerrain(bridge, deformedCollisionMeshes.Count))
        {
            return null;
        }
        uint mask = 0;
        for (int previousTerrain = 0; previousTerrain < 32; previousTerrain++)
        {
            mask |= ReadNativeTerrainMask(previousTerrain, controlledModel, false, controlledModel, 0);
        }
        return ReadAutoPlayTerrainEligibility(target.Faces.Select(face => face.Terrain), mask == uint.MaxValue ? null : mask);
    }

    internal static bool HasInactiveWorldBridgeTerrain(Ff7WorldBridgeState bridge, int deformedMeshes)
    {
        return bridge.Counter0 == 0 && bridge.Counter1 == 0 && bridge.Counter2 == 0 && bridge.DriftX == 0 && bridge.DriftZ == 0 && bridge.Deformation is null && deformedMeshes == 0;
    }

    internal static bool? ReadAutoPlayTerrainEligibility(IEnumerable<int> terrain, uint? mask)
    {
        int[] faces = terrain.ToArray();
        if (mask is null || faces.Length == 0 || faces.Any(value => value is < 0 or > 31))
        {
            return null;
        }
        return faces.Any(value => (mask.Value & (1u << value)) != 0);
    }

    /// <summary>Samples the native terrain selector for the current controlled model without changing its execution cache. A point sample does not prove that a complete movement or collision footprint can reach it.</summary>
    internal Ff7AutoPlayWorldTerrainPoint? ReadAutoPlayTerrain(int x, int z, Ff7WorldEntity? projectedPlayer = null)
    {
        if (controlledModel < 0)
        {
            return null;
        }
        Ff7WorldEntity player = projectedPlayer ?? RequireEntity(controlledModel);
        bool dismount = IsWorldChocobo(player.Model) && chocobo is { DismountPhase: 2 };
        Ff7WorldInputState retainedInput = input;
        try
        {
            (int Height, int Terrain)? sampled = QueryTerrain(x, z, unchecked((short)player.Y), player.Terrain, player.Model, dismount, out Ff7WorldTerrainReference? reference);
            if (sampled is not { } point || reference is null)
            {
                return null;
            }
            PcWorldMesh mesh = GetWorldCollisionMesh(reference.ChunkX, reference.ChunkZ);
            return new(x, z, point.Height, point.Terrain, reference, mesh.Triangles[reference.Triangle]);
        }
        finally
        {
            // QueryTerrain's overlap-selection cache affects later native collision decisions.
            // Keep that execution state exact; decoded and deformed resource mesh caches may warm.
            input = retainedInput;
        }
    }

    internal Ff7WorldTerrainReference? ReadAutoPlayAdviceGround(Ff7AutoPlayWorldMovementState source)
    {
        if (!IsAutoPlayAircraft(source.Player))
        {
            return source.Input.GroundTriangle;
        }
        Ff7WorldInputState retained = input;
        try
        {
            input = source.Input;
            return ReadAutoPlayTerrain(source.Player.X, source.Player.Z, source.Player)?.Triangle;
        }
        finally
        {
            input = retained;
        }
    }

    /// <summary>Reads the same world-state remapping and current bridge deformation used by native collision.</summary>
    internal PcWorldMesh ReadAutoPlayCollisionMesh(int chunkX, int chunkZ)
    {
        return GetWorldCollisionMesh(chunkX, chunkZ);
    }
}
