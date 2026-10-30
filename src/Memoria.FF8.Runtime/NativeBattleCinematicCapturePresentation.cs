using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleCinematicCapturePresentation
{
    internal static NativeBattleGfGeometryBatch Depths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { UseSourceDepths: false } visual, VertexPayload4.Length: 4 } primitive ? attempt with { Primitive = primitive with { PilotGeometry = visual with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    internal static NativeBattlePilotVisualGeometry? Corners(NativeBattlePilotVisualIdentity? identity, Vector3[]? world, int count, ImmutableArray<int> depths, int primitive)
    {
        return identity is { } value && world is not null ? new(value with { Primitive = unchecked(value.Primitive * 65537 + primitive) }, world[0], world[1], world[2], count == 4 ? world[3] : default, (byte)count) { SourceDepths = new(depths[0], depths[1], depths[2], depths[3]), UseSourceDepths = true } : null;
    }

    internal static NativeBattleGfGeometryBatch Discrete(NativeBattleGfGeometryBatch batch, NativeBattlePilotVisualIdentity? identity)
    {
        if (identity is not { } value)
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt =>
        {
            if (attempt.Primitive is not { } primitive || primitive.Words.Length < 2)
            {
                return attempt;
            }
            uint opcode = primitive.Words[1] >> 24;
            NativeBattlePilotVisualGeometry? visual = primitive.PilotGeometry;
            if (visual is { ScreenSpace: false })
            {
                return attempt;
            }
            if (visual is { ScreenSpace: true })
            {
                visual = visual with { Identity = value with { Primitive = attempt.ByteOffsetRelative }, Continuous = false };
            }
            else if (opcode is 0x28 or 0x2A && primitive.Words.Length >= 6 || opcode is 0x2C or 0x2E && primitive.Words.Length >= 9)
            {
                int stride = opcode is 0x2C or 0x2E ? 2 : 1;
                Vector3 Point(int index)
                {
                    uint screen = primitive.Words[2 + index * stride];
                    return new(unchecked((short)screen), unchecked((short)(screen >> 16)), 0);
                }
                visual = new(value with { Primitive = attempt.ByteOffsetRelative }, Point(0), Point(1), Point(2), Point(3), 4, false) { ScreenSpace = true, ScreenScale = 8 };
            }
            else if (opcode is 0x64 or 0x66 && primitive.Words.Length >= 5)
            {
                uint xy = primitive.Words[2];
                uint size = primitive.Words[4];
                int x = unchecked((short)xy);
                int y = unchecked((short)(xy >> 16));
                int width = unchecked((ushort)size);
                int height = unchecked((ushort)(size >> 16));
                visual = new(value with { Primitive = attempt.ByteOffsetRelative }, new(x, y, 0), new(x + width, y, 0), new(x, y + height, 0), new(x + width, y + height, 0), 4, false) { ScreenSpace = true, ScreenScale = 8 };
            }
            return visual is null ? attempt : attempt with { Primitive = primitive with { PilotGeometry = visual } };
        }).ToImmutableArray() };
    }
}
