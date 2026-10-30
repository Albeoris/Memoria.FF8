using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect312Presentation
{
    internal static Vector3 Loaded(NativeBattleGeometryRegisterStep step, int slot, NativeBattleCameraProjection projection)
    {
        byte[] bytes = new byte[6];
        if (slot == 0)
        {
            ImmutableArray<byte> source = step.Colors.NormalInput ?? throw new NotSupportedException("Capture requires the actually loaded V0.");
            for (int index = 0; index < 6; index++)
            {
                if (step.Colors.NormalInputKnown is { } known && !known[index])
                {
                    throw new NotSupportedException("Capture requires the consumed loaded coordinates.");
                }
                bytes[index] = source[index];
            }
        }
        else
        {
            ImmutableArray<byte?> source = slot == 1 ? step.Geometry.V1 : step.Geometry.V2;
            for (int index = 0; index < 6; index++)
            {
                bytes[index] = source[index] ?? throw new NotSupportedException("Capture requires the consumed loaded coordinates.");
            }
        }
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(NativeBattleGeometry.CaptureMatrix(step.Geometry), point), projection);
    }

    internal static NativeBattleGeometryPrimitive Attach(NativeBattleGeometryPrimitive primitive, NativeBattlePilotVisualIdentity identity, IReadOnlyList<Vector3> corners)
    {
        if (corners.Count is not (3 or 4) || primitive.VertexPayload4.Length != 4)
        {
            throw new InvalidDataException("Capture requires the actual corners and native FIFO payload.");
        }
        return primitive with { PilotGeometry = new(identity, corners[0], corners[1], corners[2], corners.Count == 4 ? corners[3] : default, checked((byte)corners.Count)) { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } };
    }

    internal static NativeBattlePilotVisualIdentity Segment(NativeBattleEffect312Frame frame, uint bank, int first, int second, int segment, int pass)
    {
        NativeBattlePilotVisualIdentity firstIdentity = NativeBattlePilotVisualLifetimes.Identity(312, frame.State.Storage, bank, first, unchecked(segment * 2 + pass));
        return firstIdentity with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(312, frame.State.Storage, bank, second).Generation };
    }
    internal static NativeBattleGfGeometryBatch PreserveNativeDepths(NativeBattleGfGeometryBatch batch)
    {
        if (!batch.Attempts.Any(attempt => attempt.Primitive?.PilotGeometry is { Identity.Effect: 312, UseSourceDepths: false }))
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { Identity.Effect: 312, UseSourceDepths: false } geometry } primitive && primitive.Words.Length > 2 ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = Payload(primitive), UseSourceDepths = true } } } : attempt).ToImmutableArray() };

        static System.Numerics.Vector4 Payload(NativeBattleGeometryPrimitive primitive)
        {
            return primitive.VertexPayload4.Length switch
            {
                0 => System.Numerics.Vector4.Zero,
                4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
                _ => throw new NotSupportedException("Effect312 capture requires its actual empty or four-lane native depth payload.")
            };
        }
    }

}
