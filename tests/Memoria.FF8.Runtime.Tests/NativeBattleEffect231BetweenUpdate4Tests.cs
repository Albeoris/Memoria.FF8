using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect231BetweenUpdate4Tests
{
    [Fact]
    public void TwoSelectorsKeepCurrentOwnerAndCombineFractionWithIndependentRadialOffset()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = Position(frame, 1, 10, 20, 30);
        frame = Position(frame, 2, 10, 276, 286);
        frame = Fixed(frame, 0x54, 1024);
        frame = Fixed(frame, 0x5C, 16);
        frame = Fixed(frame, 0x60, -3);
        frame = Fixed(frame, 0x64, 128);
        List<short> selectors = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBetweenUpdate4.Update(frame, (current, selector) =>
        {
            Assert.Equal(Address(0x2000), WaterSchedulerFixture.ReadCoreDword(current, 0x27973E8));
            selectors.Add(selector);
            return selector == 4000 ? Address(0x2100) : Address(0x2200);
        });
        Assert.Equal(new short[] { 4000, -1 }, selectors);
        AssertPosition(result, 138, 145, 46);
        Assert.Equal(256u, result.ReadTemporaryDword(0xFC));
        Assert.Equal(JsonSerializer.Serialize(frame.Core), JsonSerializer.Serialize(result.Core));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.ReadTemporaryDword(0xF8), result.ReadTemporaryDword(0xF8));
        Assert.Equal(frame.Arena.Read(0x2100, 0x200), result.Arena.Read(0x2100, 0x200));
        Assert.Equal(frame.Arena.Read(0x2050, 0x3C), result.Arena.Read(0x2050, 0x3C));
    }

    [Theory]
    [InlineData(-3, 128)]
    [InlineData(3, -128)]
    public void SignedInterpolationTruncatesTowardZeroBeforeTheWrappedWordStore(short secondY, int fraction)
    {
        NativeBattleWaterExecutionFrame frame = Position(Create(), 1, 0, 0, 0);
        frame = Position(frame, 2, 0, secondY, 256);
        frame = Fixed(frame, 0x64, fraction);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBetweenUpdate4.Update(frame, Resolve);
        AssertPosition(result, fraction, -1, 0);
    }

    [Fact]
    public void NullBlockDoesNotReadSelectorsOrPublishDistance()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(Create(), 0, 0xA0, 0);
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = Enumerable.Repeat(false, 0x180).ToImmutableArray() } };
        Assert.Same(frame, NativeBattleCinematicBetweenUpdate4.Update(frame, (_, _) => throw new InvalidOperationException("The null block must not resolve a reference.")));
    }

    [Fact]
    public void UnknownSecondSelectorFailsWithoutChangingTheInput()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3002, 2) };
        string before = JsonSerializer.Serialize(frame);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicBetweenUpdate4.Update(frame, Resolve));
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void LargeFractionKeepsNativeUncheckedProductsAndWordWidths()
    {
        NativeBattleWaterExecutionFrame frame = Position(Create(), 1, 0, 0, -20000);
        frame = Position(frame, 2, 0, 0, 20000);
        frame = Fixed(frame, 0x64, 32767);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBetweenUpdate4.Update(frame, Resolve);
        AssertPosition(result, 8035, 0, -20000);
        Assert.Equal(40000u, result.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void CurrentReferencesAndJsonRestoreProduceTheSameNextUpdate()
    {
        NativeBattleWaterExecutionFrame frame = Fixed(Create(), 0x64, 128);
        frame = Position(frame, 1, 10, 20, 30);
        frame = Position(frame, 2, 10, 276, 286);
        frame = NativeBattleCinematicBetweenUpdate4.Update(frame, Resolve);
        frame = Position(frame, 2, 10, -236, 542);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame next = NativeBattleCinematicBetweenUpdate4.Update(frame, Resolve);
        NativeBattleWaterExecutionFrame restoredNext = NativeBattleCinematicBetweenUpdate4.Update(restored, Resolve);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restoredNext));
        AssertPosition(next, 266, -108, 30);
        Assert.Equal(512u, next.ReadTemporaryDword(0xFC));
    }

    private static NativeBattleWaterExecutionFrame Create()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x118));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xA0, Address(0x3000));
        byte[] block = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(block, 4000);
        BinaryPrimitives.WriteInt16LittleEndian(block.AsSpan(2), -1);
        return frame with { Arena = frame.Arena.Write(0x3000, block) };
    }

    private static uint Resolve(NativeBattleWaterExecutionFrame _, short selector) => selector switch
    {
        4000 => Address(0x2100),
        -1 => Address(0x2200),
        _ => throw new NotSupportedException("The synthetic reference has no current owner.")
    };

    private static NativeBattleWaterExecutionFrame Position(NativeBattleWaterExecutionFrame frame, int index, int x, int y, int z)
    {
        frame = WaterSchedulerFixture.SlotWord(frame, index, 0x94, unchecked((ushort)x));
        frame = WaterSchedulerFixture.SlotWord(frame, index, 0x96, unchecked((ushort)y));
        return WaterSchedulerFixture.SlotWord(frame, index, 0x98, unchecked((ushort)z));
    }

    private static NativeBattleWaterExecutionFrame Fixed(NativeBattleWaterExecutionFrame frame, int offset, int value) => WaterSchedulerFixture.SlotDword(frame, 0, offset, unchecked((uint)(value << 16)));

    private static void AssertPosition(NativeBattleWaterExecutionFrame frame, int x, int y, int z)
    {
        Assert.Equal(unchecked((ushort)x), WaterSchedulerFixture.ReadSlotWord(frame, 0, 0x94));
        Assert.Equal(unchecked((ushort)y), WaterSchedulerFixture.ReadSlotWord(frame, 0, 0x96));
        Assert.Equal(unchecked((ushort)z), WaterSchedulerFixture.ReadSlotWord(frame, 0, 0x98));
    }

    private static uint Address(int offset) => checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
