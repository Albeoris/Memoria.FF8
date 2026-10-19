using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEntityPositionTests
{
    [Fact]
    public void EntityOffsetPreservesSentinelLaneAndWrapsSignedPositionBeforeBoneOne()
    {
        NativeBattleWaterExecutionFrame frame = Create(0x005F, 1, 0x7654, -2);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x60, 0xDEADBEEF);
        int reads = 0;
        bool updated = false;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEntityPosition.FromEntityOffset5F(frame, Program(8), actor =>
        {
            Assert.Equal((byte)5, actor);
            reads++;
            return new(short.MaxValue, 123, short.MinValue);
        }, (current, kind) =>
        {
            Assert.Equal((byte)1, kind);
            Assert.Equal((byte)1, current.Arena.Read(0x2018, 1)[0]);
            Assert.Equal(0x80000000u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x5C));
            Assert.Equal(0xDEADBEEFu, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x60));
            Assert.Equal(Address(0x1000), WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            updated = true;
            return current;
        });
        Assert.True(updated);
        Assert.Equal(2, reads);
        Assert.Equal(0x7FFE0000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
        Assert.Equal(Address(0x1008), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0x0060, 4, 65537, 65539)]
    [InlineData(0x8060, 8, 65537, 65537)]
    public void EntityJointUsesSignedFullSelectorCurrentPoseAndNativeOrbitWidth(ushort instruction, int width, int x, int z)
    {
        NativeBattleWaterExecutionFrame frame = Create(instruction, -1, -2, 0);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEntityPosition.FromEntityJoint60(frame, Program(8), (current, actor, selector) =>
        {
            Assert.Equal(5, actor);
            Assert.Equal((short)-1, selector);
            return new(current, new(65537, -65538, 65539, 0));
        }, actor =>
        {
            Assert.Equal(5, actor);
            return 256;
        }, (current, kind) =>
        {
            Assert.Equal((byte)3, kind);
            Assert.Equal(Address(0x1000 + width), WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return current;
        });
        Assert.Equal(unchecked((uint)(x << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(unchecked((uint)(-65538 << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x60));
        Assert.Equal(unchecked((uint)(z << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
    }

    [Fact]
    public void TargetJointAverageUsesSignedActorListAndOnlySelectedLanes()
    {
        NativeBattleWaterExecutionFrame frame = Create(0x506E, -123);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x41, [2]).Write(NativeBattleScriptedEffectMemory.Address + 0x48, [0xFF, 7]) };
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x60, 0xABCD0123);
        List<int> actors = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEntityPosition.FromTargetJointAverage6E(frame, Program(4), (current, actor, selector) =>
        {
            actors.Add(actor);
            Assert.Equal((short)-123, selector);
            return new(current, actor == -1 ? new(-9, int.MaxValue, 10, 0) : new(4, int.MaxValue, -3, 0));
        }, (current, kind) =>
        {
            Assert.Equal((byte)3, kind);
            Assert.Equal(Address(0x1000), WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return current;
        });
        Assert.Equal([-1, 7], actors);
        Assert.Equal(0xFFFE0000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(0xABCD0123u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x60));
        Assert.Equal(0x00030000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
    }

    [Theory]
    [InlineData(0x006E, false)]
    [InlineData(0x106E, true)]
    public void EmptyTargetListOnlyDividesForSelectedLane(ushort instruction, bool fails)
    {
        NativeBattleWaterExecutionFrame frame = Create(instruction, 0);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x41, [0]) };
        NativeBattleWaterExecutionFrame Execute() => NativeBattleCinematicEntityPosition.FromTargetJointAverage6E(frame, Program(4), (_, _, _) => throw new InvalidOperationException("No target should be read."), (current, _) => current);
        if (fails)
        {
            Assert.Throws<InvalidDataException>(Execute);
        }
        else
        {
            Assert.Equal(Address(0x1004), WaterSchedulerFixture.ReadCoreDword(Execute(), 0x2797450));
        }
    }

    [Fact]
    public void WaitUsesUnsignedActorMultiplierAndWordProductShift()
    {
        NativeBattleWaterExecutionFrame frame = Create(0x0140, unchecked((short)0xFFFD));
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0xCC + 5, [0xFE]) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEntityPosition.WaitScaledBySlot140(frame, Program(4));
        Assert.Equal(unchecked((ushort)(0xFE * 0xFFFD << 7)), result.ReadTemporaryWord(0x13E));
        Assert.Equal(Address(0x1004), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Create(ushort instruction, params short[] operands)
    {
        byte[] bytes = new byte[2 + 2 * operands.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, instruction);
        for (int index = 0; index < operands.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2 + 2 * index), operands[index]);
        }
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        return frame with { Arena = frame.Arena.Write(0x1000, bytes).Write(0x2018, [3]).Write(0x201B, [5]) };
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
    private static NativeBattleWaterScriptProgram Program(int length) => new(0x1000, length, Address(0x1000));
}
