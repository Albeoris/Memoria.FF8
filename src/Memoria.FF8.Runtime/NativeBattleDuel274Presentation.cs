using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleDuel274Presentation
{
    internal static NativeBattlePilotVisualIdentity Identity(NativeBattleModulePoolTask task, uint resource)
    {
        return NativeBattlePilotVisualLifetimes.Identity(274, task) with { ResourceGeneration = resource };
    }

    internal static NativeBattleModulePacketBatch Depths(NativeBattleModulePacketBatch batch)
    {
        NativeBattleGeometryPrimitive Capture(NativeBattleGeometryPrimitive primitive)
        {
            if (primitive.PilotGeometry is not { } geometry)
            {
                return primitive;
            }
            System.Numerics.Vector4 depths = primitive.VertexPayload4.Length == 4 ? new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]) : System.Numerics.Vector4.Zero;
            return primitive with { PilotGeometry = geometry with { SourceDepths = depths, UseSourceDepths = true } };
        }
        return batch with { Primitives = batch.Primitives.Select(item => item with { Primitive = Capture(item.Primitive) }).ToImmutableArray(), CapturedWrites = batch.CapturedWrites is { } writes ? NativeBattleCinematicActorPresentation.NativeDepths(writes) : null };
    }

    internal static NativeBattleGfGeometryBatch Screen(NativeBattleGfGeometryBatch batch, NativeBattlePilotVisualIdentity? identity)
    {
        if (identity is not { } owner)
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive && ((primitive.Words[1] >> 24) & 0xFF) is >= 0x20 and < 0x80 ? attempt with { Primitive = primitive with { PilotGeometry = Capture(primitive, owner with { Primitive = attempt.ByteOffsetRelative }) } } : attempt).ToImmutableArray() };
    }

    private static NativeBattlePilotVisualGeometry Capture(NativeBattleGeometryPrimitive primitive, NativeBattlePilotVisualIdentity identity)
    {
        System.Numerics.Vector3 Point(int word)
        {
            uint value = primitive.Words[word];
            return new(unchecked((short)value) / 8f, unchecked((short)(value >> 16)) / 8f, 0);
        }
        return new(identity, Point(2), Point(4), Point(6), Point(8), 4, false) { ScreenSpace = true, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true };
    }
}
