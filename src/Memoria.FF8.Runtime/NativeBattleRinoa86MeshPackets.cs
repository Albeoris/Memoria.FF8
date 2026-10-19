using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa86MeshContext(uint Flags, uint FarColor, int Fade, int DepthBias, ushort UOffset, ushort VOffset, NativeBattleTextureWindow WindowBefore, NativeBattleTextureWindow WindowAfter, int TableBaseBucket, byte DepthShift);

/// <summary>7807B0's reached normalized polygon stores, including its conditional pair of texture-window packets.</summary>
public static class NativeBattleRinoa86MeshPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch7807B0(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleRinoa86MeshContext context, NativeBattleSummonColors colors, Func<int, NativeBattleVector>? readSelectedVertex = null)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, (group, face, corner, offset) => EmitFace(group, face, corner, context, colors, offset));
    }

    public static NativeBattleGfGeometryBatch EmitFace(int group, ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleRinoa86MeshContext context, NativeBattleSummonColors colors, int byteOffset = 0)
    {
        NativeBattleEffectMeshWindow window = new(context.UOffset, context.VOffset, context.WindowBefore, context.WindowAfter);
        NativeBattleEffectMeshPacketContext operands = new(context.Flags, context.FarColor, context.Fade, null, null, 0, false, context.DepthBias, context.TableBaseBucket, context.DepthShift, window);
        return group switch
        {
            1 => NativeBattleEffectMeshPackets.EmitFlatQuad(face, readCorner, operands, colors, byteOffset),
            2 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, false, operands, colors, byteOffset),
            3 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, false, operands, colors, byteOffset),
            4 => NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, false, operands, colors, byteOffset),
            5 => NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, true, operands, colors, byteOffset),
            6 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, true, operands, colors, byteOffset),
            7 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, true, operands, colors, byteOffset),
            _ => throw new NotSupportedException($"Native Rinoa86 original resources do not reach polygon group {group}.")
        };
    }
}
