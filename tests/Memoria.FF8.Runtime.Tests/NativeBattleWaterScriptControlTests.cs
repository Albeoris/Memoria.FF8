using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterScriptControlTests
{
    private const int Slot = 0x158;
    private const int Code = 0x1000;
    private const uint Pc = 0xFFFFFFFC;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x100, 0x158, 1, Slot);

    [Theory]
    [InlineData(10, 6u)]
    [InlineData(-10, 0xFFFFFFF2u)]
    public void JumpUsesSignedRelativeAndWrapsWithoutDereferencingNextPointer(short delta, uint expected)
    {
        NativeBattleWaterScriptControlResult result = Run(Create(0x02, unchecked((ushort)delta)));
        Assert.Equal(expected, result.NextInstructionPointer);
        Assert.Null(result.YieldWait);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    [InlineData(2, -1)]
    public void CallReturnUseRawSignedDepthAcrossStreamsWithoutTwoEntryLimit(int stream, int oldDepth)
    {
        NativeBattleEffectArena arena = Create(0x03, 8).Write(Slot + 0x44 + stream, new byte[] { unchecked((byte)oldDepth) });
        NativeBattleWaterScriptControlResult called = Run(arena, stream: stream);
        Assert.Equal((byte)(oldDepth + 1), called.Arena.Read(Slot + 0x44 + stream, 1)[0]);
        int stack = Slot + 0x24 + 4 * (oldDepth + 2 * stream);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(called.Arena.Read(stack, 4)));
        arena = called.Arena.Write(Code, new byte[] { 4, 0 });
        NativeBattleWaterScriptControlResult returned = Run(arena, length: 2, stream: stream);
        Assert.Equal(0u, returned.NextInstructionPointer);
        Assert.Equal(unchecked((byte)oldDepth), returned.Arena.Read(Slot + 0x44 + stream, 1)[0]);
    }

    [Fact]
    public void CallReadsRelativeBeforeOverlappingStackWriteAndReturnRejectsUnownedStack()
    {
        NativeBattleEffectArena arena = Create(0x03, 12).Write(Slot + 0x44, [8]);
        NativeBattleWaterScriptControlResult result = Run(arena);
        Assert.Equal(8u, result.NextInstructionPointer);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, result.Arena.Read(Slot + 0x44, 4));
        arena = PutWord(PutWord(Create(), Slot + 0x22, 3), Slot + 0x24, 8);
        result = NativeBattleWaterScriptControl.Execute(arena, Bank, 0, new(Slot + 0x22, 4, 100), 100, 0, 0);
        Assert.Equal(108u, result.NextInstructionPointer);
        Assert.Equal(104u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(Slot + 0x24, 4)));
        arena = Create(4).Write(Slot + 0x44, [0x80]);
        Assert.Throws<NotSupportedException>(() => Run(arena));
        arena = Create(3, 2).Write(Slot + 0x44, [0x7F]);
        Assert.Throws<NotSupportedException>(() => Run(arena));
    }

    [Theory]
    [InlineData(0, 0x8001)]
    [InlineData(1, 0x0001)]
    [InlineData(6, 0x8001)]
    [InlineData(15, 0x8001)]
    public void HeaderFlagMutationIncludesSignedMaskAndNativeDefaultModes(int mode, int expected)
    {
        NativeBattleWaterScriptControlResult result = Run(Create((ushort)((mode << 12) | 5), 0x8000), flags: 1);
        Assert.Equal((ushort)expected, result.HeaderFlagsWord2);
        Assert.Equal(0u, result.NextInstructionPointer);
        Assert.Null(result.YieldWait);
    }

    [Theory]
    [InlineData(2, 0, 2u)]
    [InlineData(2, 1, 0xFFFFFFF4u)]
    [InlineData(3, 0, 0xFFFFFFF4u)]
    [InlineData(3, 1, 2u)]
    public void HeaderFlagBranchesConsumeOnlyTakenRelativeAndPreserveFlags(int mode, ushort flags, uint expected)
    {
        NativeBattleWaterScriptControlResult result = Run(Create((ushort)((mode << 12) | 5), 1, unchecked((ushort)-8)), flags: flags);
        Assert.Equal(expected, result.NextInstructionPointer);
        Assert.Equal(flags, result.HeaderFlagsWord2);
        NativeBattleEffectArena missing = Create((ushort)((mode << 12) | 5), 1).MarkUnknown(Code + 4, 2);
        if (expected == 2)
        {
            Assert.Equal(expected, Run(missing, length: 4, flags: flags).NextInstructionPointer);
            Assert.Equal(expected, Run(missing, flags: flags).NextInstructionPointer);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => Run(missing, length: 4, flags: flags));
            Assert.Throws<NotSupportedException>(() => Run(missing, flags: flags));
        }
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(5, 0)]
    public void FlagWaitPreservesPointerAndLiteralZeroWait(int mode, ushort flags)
    {
        NativeBattleEffectArena arena = Create((ushort)((mode << 12) | 5), 1);
        NativeBattleWaterScriptControlResult waiting = Run(arena, length: 4, flags: flags);
        Assert.Equal((ushort?)0, waiting.YieldWait);
        Assert.Equal(Pc, waiting.NextInstructionPointer);
        NativeBattleWaterScriptControlResult advancing = Run(arena, length: 4, flags: (ushort)(flags ^ 1));
        Assert.Null(advancing.YieldWait);
        Assert.Equal(0u, advancing.NextInstructionPointer);
        arena = PutWord(arena, Slot + 0xC8, 0xFFFF);
        Assert.Equal((ushort?)0xFFFF, Run(arena, flags: flags).YieldWait);
    }

    [Fact]
    public void DirectWaitZeroIsNotContinueAndHighInstructionBitsEncodeWait()
    {
        Assert.Equal((ushort?)0, Run(Create(9)).YieldWait);
        NativeBattleWaterScriptControlResult result = Run(Create(0xFE09));
        Assert.Equal((ushort?)0x3F80, result.YieldWait);
        Assert.Equal(0xFFFFFFFEu, result.NextInstructionPointer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CounterLoadReadsOnlyLowByteAndLoopDefersRelativeOnZero(int index)
    {
        NativeBattleEffectArena arena = Create((ushort)((index << 14) | 0x1E), 0xAB01).MarkUnknown(Code + 3, 1);
        NativeBattleWaterScriptControlResult loaded = Run(arena);
        Assert.Equal((byte)1, loaded.Arena.Read(Slot + 0x20 + index, 1)[0]);
        arena = loaded.Arena.Write(Code, new byte[] { 0x1F, (byte)(index << 6) }).MarkUnknown(Code + 2, 2);
        NativeBattleWaterScriptControlResult exhausted = Run(arena, length: 2);
        Assert.Equal(0u, exhausted.NextInstructionPointer);
        Assert.Equal((byte)0, exhausted.Arena.Read(Slot + 0x20 + index, 1)[0]);
        arena = Create((ushort)((index << 14) | 0x1F), unchecked((ushort)-2));
        NativeBattleWaterScriptControlResult wrapped = Run(arena);
        Assert.Equal((byte)0xFF, wrapped.Arena.Read(Slot + 0x20 + index, 1)[0]);
        Assert.Equal(0xFFFFFFFAu, wrapped.NextInstructionPointer);
    }

    [Theory]
    [InlineData(0x33, 0x12)]
    [InlineData(0xBC, 0xCA)]
    public void SingleWordStoresPreserveUnknownUnconsumedSlotFields(ushort opcode, int field)
    {
        NativeBattleEffectArena arena = Create(opcode, 0xFEDC).MarkUnknown(Slot, 0x100);
        NativeBattleWaterScriptControlResult result = Run(arena);
        Assert.Equal((ushort)0xFEDC, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(Slot + field, 2)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Slot + field + 2, 1));
        Assert.Equal(0u, result.NextInstructionPointer);
    }

    [Theory]
    [InlineData(0x0074, 0xFF00, 0x0013, 0xFF13)]
    [InlineData(0x8074, 0xFF00, 0x0013, 0xFFEC)]
    [InlineData(0x8074, 0x1234, 0x8001, 0x7FFE)]
    public void SlotFlagsPreserveOddAndThenOrReplacement(ushort instruction, ushort old, ushort mask, ushort expected)
    {
        NativeBattleEffectArena arena = PutWord(Create(instruction, mask), Slot + 0x4C, old);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(Run(arena).Arena.Read(Slot + 0x4C, 2)));
    }

    [Theory]
    [InlineData(-4, -3, true)]
    [InlineData(-4, -4, false)]
    [InlineData(-4, -5, false)]
    public void CacheThresholdUsesSignedNegativeByteOffset(short threshold, short value, bool advances)
    {
        NativeBattleEffectArena arena = PutWord(Create(0xE7, unchecked((ushort)-4), unchecked((ushort)threshold)), Slot + 0x88, unchecked((ushort)value));
        NativeBattleWaterScriptControlResult result = Run(arena);
        Assert.Equal(advances ? 2u : Pc, result.NextInstructionPointer);
        Assert.Equal(advances ? null : (ushort?)0, result.YieldWait);
        Assert.Throws<NotSupportedException>(() => Run(Create(0xE7, unchecked((ushort)-0x90), 0)));
    }

    [Theory]
    [InlineData(0x0079, -6, true)]
    [InlineData(0x0079, -5, false)]
    [InlineData(0x8079, -4, true)]
    [InlineData(0x8079, -5, false)]
    public void PositionThresholdUsesStrictDirectionAndDefersBranchOperand(ushort instruction, short value, bool passes)
    {
        NativeBattleEffectArena arena = PutDword(Create(instruction, unchecked((ushort)-5), unchecked((ushort)-8)), Slot + 0x50, unchecked((uint)(value << 16)));
        NativeBattleWaterScriptControlResult result = Run(arena);
        Assert.Equal(passes ? 2u : 0xFFFFFFF4u, result.NextInstructionPointer);
        arena = arena.MarkUnknown(Code + 4, 2);
        if (passes)
        {
            Assert.Equal(2u, Run(arena, length: 4).NextInstructionPointer);
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => Run(arena));
        }
    }

    [Fact]
    public void CurrentArenaProgramAndHeaderFlagsSurviveJsonContinuation()
    {
        NativeBattleEffectArena arena = Create(5, 1);
        NativeBattleWaterScriptControlResult first = Run(arena, flags: 0x8000);
        first = first with { Arena = first.Arena.Write(Code, new byte[] { 5, 0x10, 0, 0x80 }) };
        NativeBattleWaterScriptControlResult restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterScriptControlResult>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleWaterScriptControlResult next = Run(restored.Arena, flags: restored.HeaderFlagsWord2);
        Assert.Equal((ushort)1, next.HeaderFlagsWord2);
        Assert.Equal(NativeBattleTestSnapshots.Json(Run(first.Arena, flags: first.HeaderFlagsWord2)), NativeBattleTestSnapshots.Json(next));
        Assert.Throws<NotSupportedException>(() => Run(Create(0)));
        Assert.Throws<InvalidDataException>(() => Run(Create(2), stream: 3));
    }

    private static NativeBattleWaterScriptControlResult Run(NativeBattleEffectArena arena, int length = 6, int stream = 0, ushort flags = 0) => NativeBattleWaterScriptControl.Execute(arena, Bank, 0, new(Code, length, Pc), Pc, stream, flags);

    private static NativeBattleEffectArena Create(params ushort[] words)
    {
        NativeBattleEffectArena arena = PutDword(new NativeBattleEffectArena([]), 0x11C, 0x58);
        for (int i = 0; i < words.Length; i++)
        {
            arena = PutWord(arena, Code + 2 * i, words[i]);
        }
        return arena;
    }

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
