using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88MeshContext(uint Flags, uint FarColor, int Fade, int DepthBias, ushort UOffset, ushort VOffset, NativeBattleTextureWindow WindowBefore, NativeBattleTextureWindow WindowAfter, int TableBaseBucket, byte DepthShift);

/// <summary>76A650's reached normalized polygon stores, including its conditional pair of texture-window packets.</summary>
public static class NativeBattleRinoa88MeshPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch76A650(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleRinoa88MeshContext context, NativeBattleSummonColors colors, Func<int, NativeBattleVector>? readSelectedVertex = null)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, (group, face, corner, offset) => EmitFace(group, face, corner, context, colors, offset));
    }

    public static NativeBattleGfGeometryBatch EmitFace(int group, ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleRinoa88MeshContext context, NativeBattleSummonColors colors, int byteOffset = 0)
    {
        NativeBattleEffectMeshWindow window = new(context.UOffset, context.VOffset, context.WindowBefore, context.WindowAfter);
        NativeBattleEffectMeshPacketContext operands = new(context.Flags, context.FarColor, context.Fade, null, null, 0, false, context.DepthBias, context.TableBaseBucket, context.DepthShift, window);
        return group switch
        {
            3 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, false, operands, colors, byteOffset),
            7 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, true, operands, colors, byteOffset),
            _ => throw new NotSupportedException($"Native Rinoa88 original resources do not reach polygon group {group}.")
        };
    }
}
