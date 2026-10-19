using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87MeshContext(uint Flags, uint FarColor, int Fade, int DepthBias, int TableBaseBucket, byte DepthShift);

/// <summary>776920's normalized eight polygon stores with original fade and depth operands.</summary>
public static class NativeBattleRinoa87MeshPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch776920(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleRinoa87MeshContext context, NativeBattleSummonColors colors, Func<int, NativeBattleVector>? readSelectedVertex = null)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, (group, face, corner, offset) => EmitFace(group, face, corner, context, colors, offset));
    }

    public static NativeBattleGfGeometryBatch EmitFace(int group, ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleRinoa87MeshContext context, NativeBattleSummonColors colors, int byteOffset = 0)
    {
        NativeBattleEffectMeshPacketContext operands = new(context.Flags, context.FarColor, context.Fade, null, null, 0, false, context.DepthBias, context.TableBaseBucket, context.DepthShift);
        return group switch
        {
            0 => NativeBattleEffectMeshPackets.EmitFlat(face, readCorner, false, operands, colors, byteOffset),
            1 => NativeBattleEffectMeshPackets.EmitFlatQuad(face, readCorner, operands, colors, byteOffset),
            2 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, false, operands, colors, byteOffset),
            3 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, false, operands, colors, byteOffset),
            4 => NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, false, operands, colors, byteOffset),
            5 => NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, true, operands, colors, byteOffset),
            6 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, true, operands, colors, byteOffset),
            7 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, true, operands, colors, byteOffset),
            _ => throw new NotSupportedException($"Native776920 polygon group {group} leaves its actual table.")
        };
    }
}
