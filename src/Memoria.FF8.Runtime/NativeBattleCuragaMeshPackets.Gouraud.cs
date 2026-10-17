namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaMeshPackets
{
    public static NativeBattleGfGeometryBatch EmitG3_87F960(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return EmitGouraud(face, readCorner, false, context, colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitG4_87FB90(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return EmitGouraud(face, readCorner, true, context, colors, byteOffset);
    }

    private static NativeBattleGfGeometryBatch EmitGouraud(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool quad, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors, int byteOffset)
    {
        return NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, quad, Normalize(context, false), colors, byteOffset);
    }
}
