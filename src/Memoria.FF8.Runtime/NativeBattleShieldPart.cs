using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The shared prim-model shield contract; Protect alone reaches offset800 and rotation8000 flags.</summary>
public static class NativeBattleShieldPart
{
    public static NativeBattleShieldPartDraw Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix placement, ImmutableArray<byte> blend, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool protect)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(blend, new([], 0), null);
        }
        ImmutableArray<byte> data = source.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4)));
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(geometry + 4));
        if (vertices is < 0 or > 4096)
        {
            throw new InvalidDataException("Native shield part exceeds its bounded original vertex shapes.");
        }
        ImmutableArray<byte> selected;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = data.AsSpan().Slice(checked(geometry + 12 + shape * vertices * 8), checked(vertices * 8)).ToArray().ToImmutableArray();
        }
        else
        {
            blend = NativeBattleGfMorphGeometry.Morph701390(data, geometry, node.Word40, node.Word42, node.Scalar38, blend);
            selected = blend.AsSpan().Slice(0, checked(vertices * 8)).ToArray().ToImmutableArray();
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = protect && (node.Header & 0x800) != 0 ? NativeBattleGfMatrices.RotateShort56C4F0(local, node.Vector8) : node.Vector8;
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(placement, offset);
        NativeBattleActionMatrix matrix = protect && (node.Header & 0x8000) != 0 ? local : NativeBattleGfMatrices.Multiply56C090(placement, local);
        matrix = matrix with { X = unchecked(x + placement.X), Y = unchecked(y + placement.Y), Z = unchecked(z + placement.Z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(data, geometry, selected, matrix, projection, new(flags, node.Scalar36 == 0 ? null : node.Color32, node.Scalar36, null, null, null, 17, 2), colors);
        return new(blend, batch, node.Scalar36 == 0 ? null : node.Color32);
    }
}
