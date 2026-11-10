using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldBridgeState(int Phase = 0, int Counter0 = 0, int Counter1 = 0, int Counter2 = 0, int DriftX = 0, int DriftZ = 0, Ff7WorldBridgeDeformation? Deformation = null);

public sealed partial class Ff7WorldRuntime
{
    private IReadOnlyList<Ff7WorldBridgeDefinition> bridgeDefinitions = [];
    private Ff7WorldBridgeGeometry bridgeGeometry = null!;
    private Ff7WorldBridgeState initialBridge = new();
    private readonly Dictionary<(int X, int Z), PcWorldMesh> deformedCollisionMeshes = [];

    public Ff7WorldBridgeDeformation? BridgeDeformation => input.Bridge?.Deformation;

    private void InitializeBridges(Ff7Installation installation)
    {
        bridgeDefinitions = Ff7PcExecutableData.LoadWorldBridges(installation);
        bridgeGeometry = new(bridgeDefinitions, inputMath);
        initialBridge = new(persistent[0x3F7], persistent[0x3F4], persistent[0x3F5], persistent[0x3F6]);
        input = input with { Bridge = initialBridge, BridgeCooldown = persistent[0x3B6] | persistent[0x3B7] << 8, CollisionRecovery = 2 };
    }

    private void ApplyBridgeDrift(ref int dx, ref int dz)
    {
        Ff7WorldBridgeState bridge = input.Bridge ?? initialBridge;
        dx += bridge.DriftX;
        dz += bridge.DriftZ;
    }

    private void PrepareBridgeTerrain(int chunkX, int chunkZ)
    {
        deformedCollisionMeshes.Clear();
        Ff7WorldBridgeState bridge = input.Bridge ?? initialBridge;
        Ff7WorldBridgeDefinition? definition = bridgeDefinitions.FirstOrDefault(value => value.ChunkX == chunkX && value.ChunkZ == chunkZ);
        int amplitude = definition is null ? 0 : BridgeCounter(bridge, definition.Index);
        if (definition is null || amplitude == 0 || Resources.World != 0)
        {
            input = input with { Bridge = bridge with { DriftX = 0, DriftZ = 0, Deformation = null } };
            return;
        }
        int phase = bridge.Phase << 6 & 4095;
        int tilt = amplitude * inputMath.Sine(phase) >> 17;
        Ff7WorldEntity player = RequireEntity(controlledModel);
        int localX = player.X & 8191;
        int localZ = player.Z & 8191;
        int dx;
        int dz;
        if (input.BridgeCooldown > 0)
        {
            int direction = inputMath.Direction(definition.CenterX - localX, definition.CenterZ - localZ);
            Vector3 displacement = inputMath.TransformRotation(new(0, 0, 40), 0, direction, 0);
            dx = unchecked((short)(int)displacement.X);
            dz = unchecked((short)(int)displacement.Z);
        }
        else
        {
            int strength = amplitude * Math.Max(0, 3500 - Math.Abs(localX - definition.CenterX) - Math.Abs(localZ - definition.CenterZ)) >> 12;
            int driftTilt = strength * inputMath.Cosine(phase) >> 16;
            Vector3 displacement = inputMath.TransformRotation(new(0, unchecked((short)(player.Y - 20000)), 0), 0, definition.Yaw, driftTilt);
            dx = (int)displacement.X >> 4;
            dz = (int)displacement.Z >> 4;
        }
        input = input with { Bridge = bridge with { Phase = unchecked(bridge.Phase + 1), DriftX = dx, DriftZ = dz, Deformation = new(definition.Index, tilt) } };
    }

    private PcWorldMesh BridgeCollisionMesh(int chunkX, int chunkZ, PcWorldMesh mesh)
    {
        Ff7WorldBridgeDeformation? deformation = input.Bridge?.Deformation;
        if (deformation is null)
        {
            return mesh;
        }
        Ff7WorldBridgeDefinition definition = bridgeDefinitions[deformation.Index];
        if (definition.ChunkX != chunkX || definition.ChunkZ != chunkZ)
        {
            return mesh;
        }
        if (!deformedCollisionMeshes.TryGetValue((chunkX, chunkZ), out PcWorldMesh? deformed))
        {
            deformed = mesh with { Vertices = mesh.Vertices.Select((vertex, index) => bridgeGeometry.Vertex(chunkX, chunkZ, index, vertex, deformation)).ToArray() };
            deformedCollisionMeshes.Add((chunkX, chunkZ), deformed);
        }
        return deformed;
    }

    private void AdvanceBridgeAudio()
    {
        Ff7WorldBridgeState bridge = input.Bridge ?? initialBridge;
        if (controlledModel >= 0 && RequireEntity(controlledModel) is { } player && (player.Terrain & 31) == 14 && (input.Horizontal != 0 || input.Vertical != 0 || (input.Held & 0xF000) != 0))
        {
            Ff7WorldBridgeDefinition? definition = bridgeDefinitions.FirstOrDefault(value => value.ChunkX == player.X >> 13 && value.ChunkZ == player.Z >> 13);
            if (definition is not null && BridgeCounter(bridge, definition.Index) < 100)
            {
                bridge = WithBridgeCounter(bridge, definition.Index, BridgeCounter(bridge, definition.Index) + 10);
            }
            PlayPrioritizedWorldSound(203);
        }
        for (int index = 0; index < 3; index++)
        {
            int counter = BridgeCounter(bridge, index);
            if (counter > 0)
            {
                counter--;
                bridge = WithBridgeCounter(bridge, index, counter);
            }
            if (counter == 1)
            {
                PlayPrioritizedWorldSound(-203);
            }
        }
        input = input with { Bridge = bridge };
    }

    private static int BridgeCounter(Ff7WorldBridgeState bridge, int index) => index switch { 0 => bridge.Counter0, 1 => bridge.Counter1, 2 => bridge.Counter2, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    private static Ff7WorldBridgeState WithBridgeCounter(Ff7WorldBridgeState bridge, int index, int value) => index switch { 0 => bridge with { Counter0 = value }, 1 => bridge with { Counter1 = value }, 2 => bridge with { Counter2 = value }, _ => throw new ArgumentOutOfRangeException(nameof(index)) };

    private static void ValidateBridgeState(Ff7WorldBridgeState? bridge)
    {
        if (bridge is not null && (bridge.Counter0 is < 0 or > 255 || bridge.Counter1 is < 0 or > 255 || bridge.Counter2 is < 0 or > 255 || bridge.DriftX is < -131072 or > 131072 || bridge.DriftZ is < -131072 or > 131072 || bridge.Deformation is { Index: < 0 or > 2 } or { Tilt: < -8 or > 7 }))
        {
            throw new InvalidDataException("FFVII world bridge state has corrupt native counters, drift or deformation.");
        }
    }
}
