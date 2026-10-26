using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187OpeningGridTests
{
    [Fact]
    public void WrappedTextureEdgesCropCellsAndKeepUnusedUvWordsUntouched()
    {
        NativeBattleEffect187OpeningGridStep step = Draw(255, false);
        Assert.Equal(1024, step.Batch.Attempts.Length);
        Assert.Equal(40960, step.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt first = step.Batch.Attempts[0];
        NativeBattleGfGeometryAttempt next = step.Batch.Attempts[1];
        Assert.Equal((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(first.Data.AsSpan(12, 2)));
        Assert.Equal((ushort)0xFF00, BinaryPrimitives.ReadUInt16LittleEndian(next.Data.AsSpan(12, 2)));
        Assert.Equal((ushort)0x3FD4, BinaryPrimitives.ReadUInt16LittleEndian(first.Data.AsSpan(14, 2)));
        Assert.Equal((ushort)0xB6, BinaryPrimitives.ReadUInt16LittleEndian(first.Data.AsSpan(22, 2)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[31]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[39]);
        Assert.Throws<NotSupportedException>(() => step.Context120.Read(0x5A, 2));
        Assert.False(step.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void OpeningGridDoesNotIntroduceAnAreaRejection()
    {
        NativeBattleEffect187OpeningGridStep step = Draw(0, false);
        Assert.All(step.Batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
        Assert.Equal(0x2E404040u, BinaryPrimitives.ReadUInt32LittleEndian(step.Batch.Attempts[0].Data.AsSpan(4, 4)));
        Assert.Equal(step.Memory.GeometryRegisters!.Otz, step.Memory.GeometryRegisters.Mac0);
    }

    [Fact]
    public void NearRejectedCellsReuseAllocationCursorAndPublishOnlyPrefix()
    {
        NativeBattleEffect187OpeningGridStep step = Draw(0, true);
        Assert.Equal(0, step.Batch.AllocationBytes);
        Assert.Equal(1024, step.Batch.Attempts.Length);
        Assert.All(step.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        });
        Assert.Throws<NotSupportedException>(() => step.Context120.Read(0x4C, 4));
        Assert.NotEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(step.Context120.Read(0x50, 4)) & 0x60000);
    }

    private static NativeBattleEffect187OpeningGridStep Draw(int phase, bool near)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleActionMatrix matrix = near ? new(new short[9].ToImmutableArray(), 0, 0, 0, 4096) : NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 120).Write(0, BitConverter.GetBytes(256)).Write(4, BitConverter.GetBytes(32)).Write(8, BitConverter.GetBytes(32)).Write(12, BitConverter.GetBytes(phase)).Write(16, BitConverter.GetBytes(phase)).Write(0x74, BitConverter.GetBytes(0x404040u));
        return NativeBattleEffect187OpeningGrid.Draw647B10(memory, context, new(null, 100, 160, 108));
    }
}
