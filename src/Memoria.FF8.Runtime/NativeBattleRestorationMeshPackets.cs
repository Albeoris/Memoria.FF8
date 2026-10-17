using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEsunaMeshContext(uint Flags, uint? FarColor, int? Fade, int DepthBias, ushort UOffset, ushort VOffset, NativeBattleTextureWindow WindowBefore, NativeBattleTextureWindow WindowAfter, int TableBaseBucket, byte DepthShift);
public sealed record NativeBattleFullLifeMeshContext(uint Flags, uint? FarColor, int? Fade, ushort? TexturePage, ushort? Clut, uint? UvOffset, int TableBaseBucket, byte DepthShift);
public sealed record NativeBattleEsunaMeshPreparation(NativeBattleModulePoolTask Task, NativeBattleEsunaMeshContext Context);

/// <summary>The original restoration context operands and thin wrappers over the normalized physical mesh packet leaves.</summary>
public static class NativeBattleRestorationMeshPackets
{
    public static NativeBattleEsunaMeshPreparation Prepare88E090(NativeBattleModulePoolTask task, sbyte sourceIndex, short sourceWord3A, short fade, uint? farColor)
    {
        if (task.Callback != NativeBattleRestorationProfile.Esuna.GenericCallback)
        {
            throw new InvalidDataException("Native88E090 requires its own original generic context task.");
        }
        uint flags = sourceWord3A == 0 ? 0x30u : 0;
        if (fade != 0)
        {
            flags |= 0xC0;
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleTextureWindow window = full;
        ushort vertical = 0;
        if (sourceIndex is 1 or 2)
        {
            vertical = (ushort)((unchecked((byte)task.ReadWord(0x34 + 0x2A)) + 2) & 0x7F);
            task = task.WriteWord(0x34 + 0x2A, vertical);
            window = new(0, 0, 128, 128);
        }
        return new(task, new(flags, fade != 0 ? farColor : null, fade != 0 ? fade : null, 0, 0, vertical, full, window, 17, 2));
    }

    public static NativeBattleGfGeometryBatch EmitFt3_88E820(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEsunaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, false, Normalize(context), colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitFt4_88ECA0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEsunaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, false, Normalize(context), colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitGt4_88FC50(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEsunaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, true, Normalize(context), colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitF4_8846C0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleFullLifeMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return NativeBattleEffectMeshPackets.EmitFlatQuad(face, readCorner, Normalize(context, false), colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitFt4_884B80(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleFullLifeMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, false, Normalize(context, true), colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitBatch88E240(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEsunaMeshContext context, NativeBattleSummonColors? colors = null, Func<int, NativeBattleVector>? readSelectedVertex = null)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, (group, face, readCorner, offset) => group switch
        {
            2 => EmitFt3_88E820(face, readCorner, context, colors, offset),
            3 => EmitFt4_88ECA0(face, readCorner, context, colors, offset),
            7 => EmitGt4_88FC50(face, readCorner, context, colors, offset),
            _ => throw new NotSupportedException($"Native88E240 group{group} requires its own original polygon leaf.")
        });
    }

    public static NativeBattleGfGeometryBatch EmitBatch884370(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleFullLifeMeshContext context, NativeBattleSummonColors? colors = null, Func<int, NativeBattleVector>? readSelectedVertex = null)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, (group, face, readCorner, offset) => group switch
        {
            1 => EmitF4_8846C0(face, readCorner, context, colors, offset),
            3 => EmitFt4_884B80(face, readCorner, context, colors, offset),
            _ => throw new NotSupportedException($"Native884370 group{group} requires its own original polygon leaf.")
        });
    }

    private static NativeBattleEffectMeshPacketContext Normalize(NativeBattleEsunaMeshContext context)
    {
        return new(context.Flags, context.FarColor, context.Fade, null, null, 0, false, context.DepthBias, context.TableBaseBucket, context.DepthShift, new(context.UOffset, context.VOffset, context.WindowBefore, context.WindowAfter));
    }

    private static NativeBattleEffectMeshPacketContext Normalize(NativeBattleFullLifeMeshContext context, bool textured)
    {
        uint uv = textured && (context.Flags & 0x1000) != 0 ? context.UvOffset ?? throw new NotSupportedException("Native884370 requires its preserved UV-offset DWORD18.") : 0;
        return new(context.Flags, context.FarColor, context.Fade, context.TexturePage, context.Clut, uv, true, null, context.TableBaseBucket, context.DepthShift);
    }

}
