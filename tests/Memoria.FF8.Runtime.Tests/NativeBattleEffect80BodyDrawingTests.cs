using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect80BodyDrawingTests
{
    [Fact]
    public void Native_C4_context_reads_C0_depth_without_reading_the_D0_variant()
    {
        NativeBattleEffectArena context = Context().Write(0xC0, BitConverter.GetBytes((short)-32));
        Assert.Throws<NotSupportedException>(() => context.Read(0xD0, 2));
        NativeBattleEffect79BodyDrawingStep result = Draw(context);
        Assert.Equal((short)-32, BinaryPrimitives.ReadInt16LittleEndian(result.Temporary168.Read(0xA6, 2)));
        Assert.Empty(result.Batch.Attempts);
        Assert.False(result.Memory.ScriptedLighting!.ColorMatrix!.Known[18]);
        Assert.False(result.Memory.ScriptedLighting.ColorMatrix.Known[19]);
    }

    [Fact]
    public void Unknown_native_C0_is_not_replaced_by_the_other_variant_depth()
    {
        NativeBattleEffectArena context = Context().MarkUnknown(0xC0, 2).Write(0xD0, BitConverter.GetBytes((short)123));
        Assert.Throws<NotSupportedException>(() => Draw(context));
    }

    private static NativeBattleEffect79BodyDrawingStep Draw(NativeBattleEffectArena context)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity, true, true, null),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown()
        };
        return NativeBattleEffect79BodyDrawing.Draw(memory, context, NativeBattleEffectArena.Clear().MarkUnknown(0, 168), (_, length) => NativeBattleEffectArena.Clear().Write(0, new byte[length]), 0x1000, [], 0x123456, 0, new(null, 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 0, NativeBattleGteColorInterpolation.CodeConstants45F270, depthFieldOffset: 0xC0);
    }

    private static NativeBattleEffectArena Context()
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0xC4, NativeBattleEffectArena.Length - 0xC4);
        context = context.Write(0, NativeBattlePackedMatrix32.FromMatrix(identity, null).Bytes.AsSpan()).MarkUnknown(18, 2);
        return context.Write(0x40, NativeBattlePackedMatrix32.FromMatrix(identity with { Z = 8000 }, null).Bytes.AsSpan()).MarkUnknown(0x52, 2);
    }
}
