using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>8131A0 and relocated recovery leaves over the same normalized physical FT3/FT4/GT4 core.</summary>
public static class NativeBattleRecoveryMeshPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch8131A0(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleFullLifeMeshContext context, NativeBattleSummonColors? colors = null)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, null, (group, face, readCorner, offset) => group switch
        {
            2 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, false, NativeBattleRestorationMeshPackets.Normalize(context, true), colors, offset),
            3 => NativeBattleRestorationMeshPackets.EmitFt4_884B80(face, readCorner, context, colors, offset),
            7 => NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, true, NativeBattleRestorationMeshPackets.Normalize(context, true), colors, offset),
            _ => throw new NotSupportedException($"Native8131A0 group{group} requires its own reached original packet leaf.")
        });
    }
}
