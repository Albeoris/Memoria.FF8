using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleSlot280288Presentation
{
    internal static NativeBattlePilotVisualIdentity Task(ushort effect, NativeBattleModulePoolTask task, uint source, int workspace = 0)
    {
        if (effect is not (280 or 288))
        {
            throw new InvalidDataException("Slot presentation requires its selected family owner.");
        }
        return NativeBattlePilotVisualLifetimes.Identity(effect, task, workspace) with { ResourceGeneration = source };
    }

    internal static NativeBattleGfGeometryBatch Depths(ushort effect, NativeBattleGfGeometryBatch batch)
    {
        if (!batch.Attempts.Any(attempt => Captured(effect, attempt.Primitive)))
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => Captured(effect, attempt.Primitive) ? attempt with { Primitive = Depths(attempt.Primitive!) } : attempt).ToImmutableArray() };
    }

    internal static NativeBattleModulePacketBatch Depths(ushort effect, NativeBattleModulePacketBatch batch)
    {
        NativeBattleGfGeometryBatch? writes = batch.CapturedWrites is { } captured ? Depths(effect, captured) : null;
        bool capturedPrimitive = batch.Primitives.Any(item => Captured(effect, item.Primitive));
        if (!capturedPrimitive && ReferenceEquals(writes, batch.CapturedWrites))
        {
            return batch;
        }
        return batch with
        {
            Primitives = capturedPrimitive ? batch.Primitives.Select(item => Captured(effect, item.Primitive) ? item with { Primitive = Depths(item.Primitive) } : item).ToImmutableArray() : batch.Primitives,
            CapturedWrites = writes
        };
    }

    private static bool Captured(ushort effect, NativeBattleGeometryPrimitive? primitive)
    {
        return primitive is { Words.Length: > 2, PilotGeometry: { UseSourceDepths: false } geometry } && geometry.Identity.Effect == effect;
    }

    private static NativeBattleGeometryPrimitive Depths(NativeBattleGeometryPrimitive primitive)
    {
        Vector4 depths = primitive.VertexPayload4.Length switch
        {
            0 => Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new InvalidDataException("Slot capture requires its actual empty or four-lane native primitive depth payload.")
        };
        return primitive with { PilotGeometry = primitive.PilotGeometry! with { SourceDepths = depths, UseSourceDepths = true } };
    }
}
