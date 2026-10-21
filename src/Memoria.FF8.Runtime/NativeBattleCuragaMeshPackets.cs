using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaMeshContext(uint Flags, uint? FarColor, int? Fade, ushort? TexturePage, ushort? Clut, uint? UvOffset, int TableBaseBucket, byte DepthShift);

/// <summary>87EEB0's geometry dispatcher and its normalized flat, textured and Gouraud leaves.</summary>
public static partial class NativeBattleCuragaMeshPackets
{
    private static readonly string[] Leaves = ["87EFF0", "87F200", "87F480", "87F6C0", "87F960", "87FB90", "87FE50", "8800B0"];

    public static NativeBattleGfGeometryBatch EmitBatch87EEB0(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleCuragaMeshContext context, Func<int, NativeBattleVector>? readSelectedVertex = null, NativeBattleSummonColors? colors = null, Action<uint>? observeFirstThreeFlags = null)
    {
        if (context.DepthShift > 31)
        {
            throw new InvalidDataException("Native87EEB0 requires its actual depth shift.");
        }
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, (group, face, project, offset) => group switch
        {
            1 => EmitF4_87F200(face, project, context, colors, offset),
            2 => EmitFt3_87F480(face, project, context, colors, offset),
            3 or 7 => EmitQuad(face, project, group == 7, context, colors, offset),
            4 or 5 => EmitGouraud(face, project, group == 5, context, colors, offset),
            _ => throw new NotSupportedException($"Native87EEB0 reached original{Leaves[group]} group{group}; its exact packet leaf is not implemented.")
        }, observeFirstThreeFlags);
    }

    public static NativeBattleGfGeometryBatch EmitFt4_87F6C0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return EmitQuad(face, readCorner, false, context, colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitGt4_8800B0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return EmitQuad(face, readCorner, true, context, colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitF4_87F200(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return NativeBattleEffectMeshPackets.EmitFlatQuad(face, readCorner, Normalize(context, false), colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitFt3_87F480(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, false, Normalize(context, true), colors, byteOffset);
    }

    private static NativeBattleGfGeometryBatch EmitQuad(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool gouraud, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors, int byteOffset)
    {
        return NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, gouraud, Normalize(context, true), colors, byteOffset);
    }

    private static NativeBattleEffectMeshPacketContext Normalize(NativeBattleCuragaMeshContext context, bool textured)
    {
        uint uvOffset = textured && (context.Flags & 0x1000) != 0 ? context.UvOffset ?? throw new NotSupportedException("Native87EEB0 requires its preserved UV-offset DWORD18.") : 0;
        return new(context.Flags, context.FarColor, context.Fade, context.TexturePage, context.Clut, uvOffset, true, null, context.TableBaseBucket, context.DepthShift);
    }

}
