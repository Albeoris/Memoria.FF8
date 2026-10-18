using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco100DepthPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch(ImmutableArray<byte> source, int geometryOffset, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleModuleMeshContext operands, short depthBias, NativeBattleSummonColors colors)
    {
        if (geometryOffset < 0 || source.IsDefault || geometryOffset > source.Length - 8 || selectedVertices.IsDefault)
        {
            throw new InvalidDataException("Native Choco depth mesh requires its actual selected geometry owner.");
        }
        ImmutableArray<byte> mesh = source.AsSpan().Slice(geometryOffset).ToArray().ToImmutableArray();
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, operands.Flags, ReadVertex, (group, face, readCorner, offset) => EmitFace(group, face, readCorner, operands, depthBias, colors, offset));

        NativeBattleVector ReadVertex(int offset)
        {
            if (offset < 0 || offset > selectedVertices.Length - 6)
            {
                throw new InvalidDataException("Native Choco depth mesh vertex leaves its current selected owner.");
            }
            ReadOnlySpan<byte> vertex = selectedVertices.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(4)));
        }
    }

    public static NativeBattleGfGeometryBatch EmitFace(int group, ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleModuleMeshContext operands, short depthBias, NativeBattleSummonColors colors, int byteOffset = 0)
    {
        uint uv = (operands.Flags & 0x1000) != 0 ? operands.UvOffset ?? throw new NotSupportedException("Native Choco depth mesh requires its actual packed UV owner.") : 0;
        NativeBattleEffectMeshPacketContext context = new(operands.Flags, operands.FarColor, operands.Fade, operands.TexturePage, operands.Clut, uv, true, depthBias, 17, 2, ScreenMaximumX: 320, ScreenMaximumY: 216, MinimumDepth: 16);
        return group switch
        {
            2 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, false, context, colors, byteOffset),
            4 => NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, false, context, colors, byteOffset),
            5 => NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, true, context, colors, byteOffset),
            _ => throw new NotSupportedException("Native Choco depth mesh group requires its own original polygon leaf.")
        };
    }
}
