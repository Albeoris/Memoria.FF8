using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect32Presentation
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NativeBattleTaskBankOwner, Dictionary<(uint Bank, int Slot), long>> births = new();

    internal static NativeBattleModulePoolTask OwnerAt(NativeBattleOriginalEffectState effect, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            long relative = (long)address - pool.BankAddress;
            if (relative >= 0 && relative < (long)pool.Stride * pool.Slots.Length)
            {
                return pool.Slots[checked((int)(relative / pool.Stride))];
            }
        }
        throw new InvalidOperationException("Presentation requires its actual allocated physical task owner.");
    }

    internal static void Allocated(NativeBattleOriginalEffectState effect, uint ownerAddress, uint bank, int slot)
    {
        NativeBattleTaskBankOwner owner = OwnerAt(effect, ownerAddress).RequireOwner();
        births.GetValue(owner, _ => [])[(bank, slot)] = NativeBattlePilotVisualLifetimes.EpochFor(new object());
    }

    internal static NativeBattlePilotVisualIdentity? TaskInstance(NativeBattlePilotVisualIdentity? parent, NativeBattleOriginalEffectState effect, uint ownerAddress, uint bank, int slot, int primitive = 0)
    {
        if (parent is not { } original)
        {
            return null;
        }
        NativeBattleModulePoolTask task = OwnerAt(effect, ownerAddress);
        Dictionary<(uint Bank, int Slot), long> rows = births.GetValue(task.RequireOwner(), _ => []);
        if (!rows.TryGetValue((bank, slot), out long birth))
        {
            birth = NativeBattlePilotVisualLifetimes.EpochFor(new object());
            rows.Add((bank, slot), birth);
        }
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(original.Effect, task);
        return new NativeBattlePilotVisualIdentity(original.Effect, bank, slot, birth, primitive) { ResourceGeneration = unchecked(owner.Generation * 397 ^ original.ResourceGeneration) };
    }

    internal static NativeBattleGfGeometryBatch Depths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { UseSourceDepths: false } geometry } primitive && primitive.VertexPayload4.Length == 4 ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    internal static long ResourceEpoch(NativeBattleResourcePointer pointer) => NativeBattlePilotVisualLifetimes.EpochFor((object?)pointer.Cpu ?? pointer);

    internal static NativeBattlePilotVisualIdentity? Resource(NativeBattlePilotVisualIdentity? identity, uint resource, int primitive = 0)
    {
        return identity is { } owner ? owner with { ResourceGeneration = unchecked(owner.ResourceGeneration * 397 ^ resource), Primitive = unchecked(owner.Primitive * 397 + primitive) } : null;
    }

    internal static Vector3 World(NativeBattleActionMatrix matrix, NativeBattleVector point, NativeBattleCameraProjection projection)
    {
        return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
    }

    internal static Vector3 DeformedWorld(NativeBattleActionMatrix matrix, NativeBattleVector point, uint before, uint after, NativeBattleCameraProjection projection)
    {
        Vector3 camera = NativeBattlePilotVisualGeometry.Transform(matrix, point);
        float focal = projection.FocalLength ?? throw new InvalidOperationException("Deformed presentation requires its actual focal length.");
        float scale = Math.Max(camera.Z, focal / 2) / (focal * 8);
        camera.X += (unchecked((short)after) - unchecked((short)before)) * scale;
        camera.Y += (unchecked((short)(after >> 16)) - unchecked((short)(before >> 16))) * scale;
        return NativeBattlePilotVisualGeometry.CameraToWorld(camera, projection);
    }

    internal static NativeBattleGeometryPrimitive Capture(NativeBattleGeometryPrimitive primitive, NativeBattlePilotVisualIdentity? identity, IReadOnlyList<Vector3>? world)
    {
        if (identity is not { } owner || world is null)
        {
            return primitive;
        }
        return primitive with { PilotGeometry = new(owner with { Primitive = unchecked(owner.Primitive * 397 + (int)(primitive.Words[1] >> 24 & 0xFF)) }, world[0], world[1], world[2], world.Count == 4 ? world[3] : default, checked((byte)world.Count)) { SourceDepths = primitive.VertexPayload4.Length == 4 ? new Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]) : Vector4.Zero, UseSourceDepths = true } };
    }

    internal static ImmutableArray<NativeBattlePrimitiveReference> Record(ImmutableArray<NativeBattlePrimitiveReference> prior, string bank, byte buffer, int start, NativeBattleModulePacketBatch batch)
    {
        if (batch.CapturedWrites is { } writes)
        {
            return NativeBattleEffectPresentationPackets.Record(prior, bank, buffer, start, writes);
        }
        return prior.AddRange(batch.Primitives.Select(item => new NativeBattlePrimitiveReference(bank, buffer, checked(start + item.ByteOffset))));
    }
}
