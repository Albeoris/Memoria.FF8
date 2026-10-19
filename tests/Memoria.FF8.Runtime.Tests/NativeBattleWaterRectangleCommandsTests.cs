using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterRectangleCommandsTests
{
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);
    private static readonly NativeBattleWater222Resources Resources = new([new("current0", new byte[0x1799C].ToImmutableArray()), new("current1", new byte[0x26258].ToImmutableArray())], new(new("tables", new byte[0x70].ToImmutableArray()), new("directory", new byte[0x1C].ToImmutableArray()), new("owners", new byte[0x14].ToImmutableArray())));
    private static readonly NativeBattleEffectHeapAllocation Map = new("map", new byte[16].ToImmutableArray());

    [Fact]
    public void SharedGetterDoesNotReadShapeOrDirectoryCountAndOnlySelectionPublishesDirectory()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x25, 1).WriteTemporaryDword(0xFC, 0xA5A5A5A5);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x5020, 4).MarkUnknown(0x50A0, 0x40) };
        Assert.Equal(Address(0x50A0), NativeBattleWaterShapeSelection.ResolveB657E0(frame, Resources, Map, 1));
        Assert.Equal(0xA5A5A5A5u, frame.ReadTemporaryDword(0xFC));
        (NativeBattleWaterExecutionFrame selected, uint address) = NativeBattleWaterShapeSelection.SelectB66230(frame, Resources, Map, 1);
        Assert.Equal(Address(0x50A0), address);
        Assert.Equal(Address(0x5020), selected.ReadTemporaryDword(0xFC));
        frame = Dword(frame, 0x5028, 0x100);
        Assert.Equal(Address(0x5120), NativeBattleWaterShapeSelection.ResolveB657E0(frame, Resources, Map, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterShapeSelection.ResolveB657E0(frame, Resources, Map, -1));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterShapeSelection.ResolveB657E0(frame, Resources, Map, 16));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterShapeSelection.ResolveB657E0(frame, Resources, Map with { Data = Enumerable.Repeat((byte)2, 16).ToImmutableArray() }, 1));
    }

    [Theory]
    [InlineData(0x24A, 1)]
    [InlineData(0x3C, 2)]
    public void ShapeRegistrationHasExactPartialWritesAndFinalDispatchKind(ushort instruction, byte kind)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 1);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xFFFFFFFF);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC0, 0x12345678);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x50A0, 0x40) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(0x02FFFFFFu, SlotDword(result, 0xCC));
        Assert.Equal(0u, SlotDword(result, 0xBC));
        Assert.Equal(0x12345678u, SlotDword(result, 0xC0));
        Assert.Equal(Address(0x50A0), SlotDword(result, 0xD8));
        Assert.Equal(kind, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal(new byte[] { 0, 0xFF }, result.Core.Read(0x2797554, 2));
        Assert.Equal(0x1004u, Pc(result));
    }

    [Theory]
    [InlineData(0x0056)]
    [InlineData(0xFE56)]
    public void Shape56_reads_third_word_and_registers_kind9_without_overwriting_the_depth_low_word(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 1);
        frame = frame with { Arena = WriteWord(frame.Arena, 0x1004, 0xFEDC) };
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xFFFFFFFF);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC0, 0x76543210);
        NativeBattleWaterExecutionFrame result = NativeBattleWaterRectangleCommands.Execute(frame, new(0x1000, 6, 0x1000), Bank, 0, Resources, Map);
        Assert.Equal(0x02FFFFFFu, SlotDword(result, 0xCC));
        Assert.Equal(0u, SlotDword(result, 0xBC));
        Assert.Equal(Address(0x50A0), SlotDword(result, 0xD8));
        Assert.Equal((ushort)0xFEDC, SlotWord(result, 0xC2));
        Assert.Equal((ushort)0x3210, SlotWord(result, 0xC0));
        Assert.Equal((byte)9, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal(0x1006u, Pc(result));
        Assert.Equal(new byte[] { 0, 0xFF }, result.Core.Read(0x2797554, 2));
    }

    [Fact]
    public void VelocityClearTouchesOnlyNineDwordsAndRebuildsAccelerationFlagsWithoutCache()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x2F);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2068, 36).MarkUnknown(0x208C, 6).MarkUnknown(0x2018, 1).Write(0x2064, new byte[] { 1, 2, 3, 4 }) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(new byte[36], result.Arena.Read(0x2068, 36));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Arena.Read(0x2064, 4));
        Assert.Equal((byte)0, result.Arena.Read(0x201A, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x208C, 6));
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void SequenceRegistrationSeparatesDirectoryAndFrameAndClearsStaticBit()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x25, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xCA, 0x8400);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xFFFFFFFF);
        frame = Dword(frame, 0x50A0, 0x300);
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(0x2EFFFFFFu, SlotDword(result, 0xCC));
        Assert.Equal(Address(0x5020), SlotDword(result, 0xE4));
        Assert.Equal(Address(0x50A0), SlotDword(result, 0xD0));
        Assert.Equal(Address(0x50A0), SlotDword(result, 0xD4));
        Assert.Equal(Address(0x5320), SlotDword(result, 0xD8));
        Assert.Equal((ushort)0x400, SlotWord(result, 0xCA));
        Assert.Equal((ushort)0x400, SlotWord(result, 0xDC));
        Assert.Equal((byte)5, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal((byte)9, result.Arena.Read(0x201E, 1)[0]);
        Assert.Equal(Address(0x5020), result.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void SequenceSelectionUsesCurrentArenaAfterItsOwnAliasedScalarStores()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x25, 1);
        frame = Dword(frame, 0x5028, unchecked(Address(0x20D0) - Address(0x5020)));
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(Address(0x20D0), SlotDword(result, 0xD0));
        Assert.Equal(unchecked(Address(0x5020) + Address(0x20D0)), SlotDword(result, 0xD8));
    }

    [Fact]
    public void TerminalSequenceRemovesDrawAndReturnsBeforeInvalidRectangleOrMatrixReads()
    {
        NativeBattleWaterExecutionFrame frame = Selected();
        frame = Dword(frame, 0x50A0, 0xFFFFFFFF);
        frame = frame with { Arena = frame.Arena.Write(0x50A4, new byte[] { 0, 4, 1 }).MarkUnknown(0x209C, 2).MarkUnknown(0x2094, 6) };
        frame = frame.WriteTemporaryDword(0, 0);
        NativeBattleWaterExecutionFrame result = Draw(frame);
        Assert.Equal(Address(0x501F), SlotDword(result, 0xD8));
        Assert.Equal((byte)0, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal((byte)0xFF, result.Core.Read(0x2797554, 1)[0]);
        Assert.Equal(uint.MaxValue, result.ReadTemporaryDword(0));
        Assert.Equal(SlotDword(frame, 0xD0), SlotDword(result, 0xD0));
        Assert.Equal(Pc(frame), Pc(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilterOrNegativeSpeedSkipsUnknownAnimationAndUsesNonidentityBasisColumnScale(bool filter)
    {
        NativeBattleWaterExecutionFrame frame = Selected();
        frame = frame.WriteTemporaryByte(0x145, filter ? (byte)1 : (byte)0);
        if (filter)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x20CA, 2) };
        }
        else
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xCA, 0x8000);
        }
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x20D0, 8).MarkUnknown(0x20DC, 2), Core = frame.Core.MarkUnknown(0x279799A, 14) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 512);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, unchecked((ushort)-256));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 333);
        frame = frame with { Arena = frame.Arena.Write(0x20DE, new byte[] { 1 }) };
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler56CD50(new(256, 700, -100));
        frame = WriteMatrix(frame, 0x2797988, basis, false);
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Multiply56C090(basis, NativeBattleGfMatrices.Euler56CD50(new(0, 0, 333))), 8192, -4096, 4096);
        int draws = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleWaterRectangleCommands.AdvanceDraw5(frame, (_, selector) =>
        {
            Assert.Equal((ushort)0, selector);
            return 0x27979E8;
        }, (value, matrix) =>
        {
            draws++;
            Assert.Equal(expected.Rotation, matrix.Rotation);
            Assert.Equal((110, -180, 330), (matrix.X, matrix.Y, matrix.Z));
            Assert.Equal(8192u, value.ReadTemporaryDword(0xD0));
            Assert.Equal(unchecked((uint)-4096), value.ReadTemporaryDword(0xD4));
            Assert.Equal(4096u, value.ReadTemporaryDword(0xD8));
            Assert.Equal(Address(0x5320), value.ReadTemporaryDword(0x90));
            Assert.Equal(0xFFFF8001u, value.ReadTemporaryDword(0x94));
            Assert.Equal(0xFFFF8002u, value.ReadTemporaryDword(0x9C));
            Assert.Throws<NotSupportedException>(() => value.ReadTemporaryWord(0xF2));
            return value;
        });
        Assert.Equal(1, draws);
        Assert.Equal(frame.ReadTemporaryDword(0xF4), result.ReadTemporaryDword(0xF4));
        Assert.Equal(frame.ReadTemporaryDword(0xF8), result.ReadTemporaryDword(0xF8));
        Assert.Equal(frame.ReadTemporaryDword(0xFC), result.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void WrappedTimerUsesNarrowedSignAndDoesNotReadSequenceWhenItBecomesPositive()
    {
        NativeBattleWaterExecutionFrame frame = Selected();
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xCA, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xDC, 0x8000);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x20D0, 8) };
        NativeBattleWaterExecutionFrame result = Draw(frame, (_, _) => { });
        Assert.Equal((ushort)0x7FFF, SlotWord(result, 0xDC));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void SequenceAdvancesOnlyOnceAndRestartStillDrawsSelectedFrame(byte command, bool restart)
    {
        NativeBattleWaterExecutionFrame frame = Selected();
        frame = frame with { Arena = frame.Arena.Write(0x50A4, new byte[] { 0, 4, command }) };
        int draws = 0;
        NativeBattleWaterExecutionFrame result = Draw(frame, (current, _) =>
        {
            draws++;
            Assert.Equal(Address(0x5320), current.ReadTemporaryDword(0x90));
        });
        Assert.Equal(1, draws);
        Assert.Equal(restart ? Address(0x50A0) : Address(0x50A8), SlotDword(result, 0xD0));
        Assert.Equal((ushort)0x400, SlotWord(result, 0xDC));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Draw(restored, (_, _) => { })));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, ushort operand = 0)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279773C, 0x186C8CC);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A68, Address(0x5000));
        frame = frame with { Arena = WriteWord(frame.Arena, 0x1002, operand) };
        frame = Dword(frame, 0x500C, 0x20);
        frame = Dword(frame, 0x5028, 0x80);
        frame = Dword(frame, 0x50A0, 0x300);
        return frame;
    }
    private static NativeBattleWaterExecutionFrame Selected()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x25, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xCA, 0x400);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0x8001);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x9A, 0x8002);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 10);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 20);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 30);
        frame = WriteMatrix(frame, 0x27979E8, NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)) with { X = 100, Y = -200, Z = 300 }, true);
        frame = WriteMatrix(frame, 0x2797968, NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)), false);
        return Execute(frame);
    }
    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame) => NativeBattleWaterRectangleCommands.Execute(frame, new(0x1000, 4, 0x1000), Bank, 0, Resources, Map);
    private static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, Action<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix>? observe = null) => NativeBattleWaterRectangleCommands.AdvanceDraw5(frame, (_, _) => 0x27979E8, (value, matrix) =>
    {
        if (observe is null)
        {
            throw new Xunit.Sdk.XunitException("Unexpected rectangle draw.");
        }
        observe(value, matrix);
        return value;
    });
    private static NativeBattleWaterExecutionFrame WriteMatrix(NativeBattleWaterExecutionFrame frame, uint address, NativeBattleActionMatrix matrix, bool translation)
    {
        for (int i = 0; i < 9; i++)
        {
            frame = WaterSchedulerFixture.CoreWord(frame, address + (uint)(2 * i), unchecked((ushort)matrix.Rotation[i]));
        }
        if (translation)
        {
            frame = WaterSchedulerFixture.CoreDword(frame, address + 20, unchecked((uint)matrix.X));
            frame = WaterSchedulerFixture.CoreDword(frame, address + 24, unchecked((uint)matrix.Y));
            frame = WaterSchedulerFixture.CoreDword(frame, address + 28, unchecked((uint)matrix.Z));
        }
        return frame;
    }
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint SlotDword(NativeBattleWaterExecutionFrame frame, int offset) => WaterSchedulerFixture.ReadSlotDword(frame, 0, offset);
    private static ushort SlotWord(NativeBattleWaterExecutionFrame frame, int offset) => WaterSchedulerFixture.ReadSlotWord(frame, 0, offset);
    private static NativeBattleEffectArena WriteWord(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleWaterExecutionFrame Dword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }
}
