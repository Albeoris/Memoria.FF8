using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>8131A0 and relocated recovery leaves over the same normalized physical FT3/FT4/GT4 core.</summary>
public static class NativeBattleRecoveryMeshPackets
{
    public static NativeBattleEsunaMeshOperands Prepare8CF4C0(ushort currentUvCounter, sbyte sourceIndex, short sourceWord3A, short fade, uint? farColor)
    {
        uint flags = sourceWord3A == 0 ? 0x30u : 0;
        if (fade != 0)
        {
            flags |= 0xC0;
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        ushort next = currentUvCounter;
        ushort vertical = 0;
        NativeBattleTextureWindow window = full;
        if (sourceIndex == 0)
        {
            vertical = (ushort)((unchecked((byte)currentUvCounter) + 8) & 0x7F);
            next = vertical;
            window = new(0, 0, 128, 128);
        }
        return new(next, new(flags, fade != 0 ? farColor : null, fade != 0 ? fade : null, 0, 0, vertical, full, window, 17, 2));
    }

    public static NativeBattleGfGeometryBatch EmitBatch8131A0(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleFullLifeMeshContext context, NativeBattleSummonColors? colors = null)
    {
        using NativeBattleGfSourceReader reader = new(mesh);
        return EmitBatch8131A0(reader, matrix, projection, context, colors);
    }

    internal static NativeBattleGfGeometryBatch EmitBatch8131A0(NativeBattleGfSourceReader mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleFullLifeMeshContext context, NativeBattleSummonColors? colors = null)
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
