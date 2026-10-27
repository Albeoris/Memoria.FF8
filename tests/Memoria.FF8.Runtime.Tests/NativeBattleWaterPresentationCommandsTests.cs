using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterPresentationCommandsTests
{
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);

    [Theory]
    [InlineData(0x43, false)]
    [InlineData(0x44, true)]
    public void RegistrationPreservesInlineUnknownPayloadAndUsesExactPartialClear(ushort instruction, bool clearActorCache)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0xFF15, 3);
        frame = frame with { Arena = frame.Arena.Write(0x2000 + 0xBC, Enumerable.Repeat((byte)0xA5, 16).ToArray()).MarkUnknown(0x1006, 6).MarkUnknown(0x2000 + 0xD0, 0x30), Core = frame.Core.Write(0x2797554, new byte[] { 0x81, 0xFF }) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal((byte)0x15, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal(Address(0x1006), SlotDword(result, 0xB8));
        Assert.Equal(0u, SlotDword(result, 0xBC));
        Assert.Equal(clearActorCache ? 0u : 0xA5A5A5A5u, SlotDword(result, 0xC0));
        Assert.Equal(clearActorCache ? 0u : 0xA5A5A5A5u, SlotDword(result, 0xC4));
        Assert.Equal(0xA5A5A5A5u, SlotDword(result, 0xC8));
        Assert.Equal(new byte[] { 0x81, 0, 0xFF }, result.Core.Read(0x2797554, 3));
        Assert.Equal(Address(0x100C), Pc(result));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1006, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x20D0, 1));
    }

    [Fact]
    public void ExistingFlaggedDrawEntryIsNotDuplicatedAndSignedCountWrapsCurrentPc()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x43, 6, 0xFFFF);
        frame = frame with { Core = frame.Core.Write(0x2797554, new byte[] { 0x80, 0xFF }) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(new byte[] { 0x80, 0xFF }, result.Core.Read(0x2797554, 2));
        Assert.Equal(Address(0x1004), Pc(result));
        uint start = 0xFFFFFFFC;
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, start);
        result = NativeBattleWaterPresentationCommands.Execute(frame, new(0x1000, 6, start), Bank, 0, Services());
        Assert.Equal(0u, Pc(result));
        Assert.Equal(2u, SlotDword(result, 0xB8));
    }

    [Fact]
    public void DrawRemovalUsesFullRequestedByteAndLeavesSlotsAndActiveOrderUntouched()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xD0);
        frame = frame with { Core = frame.Core.Write(0x2797554, new byte[] { 0x80, 0x81, 0xFF }).Write(0x2797454, new byte[] { 0x80, 1, 0xFF }) };
        byte[] slot = frame.Arena.Read(0x2000, 0x100);
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(new byte[] { 0x81, 0xFF }, result.Core.Read(0x2797554, 2));
        Assert.Equal(frame.Core.Read(0x2797454, 3), result.Core.Read(0x2797454, 3));
        Assert.Equal(slot, result.Arena.Read(0x2000, 0x100));
        frame = frame.WriteTemporaryByte(0x142, 0x80);
        Assert.Equal(frame.Core.Read(0x2797554, 3), Execute(frame).Core.Read(0x2797554, 3));
        frame = frame with { Core = frame.Core.Write(0x2797554, new byte[] { 0xFF }).MarkUnknown(0x2797555, 127) };
        Assert.Equal((byte)0xFF, Execute(frame).Core.Read(0x2797554, 1)[0]);
        frame = frame with { Core = frame.Core.Write(0x2797554, Enumerable.Repeat((byte)1, 128).ToArray()) };
        Assert.Throws<NotSupportedException>(() => Execute(frame));
    }

    [Fact]
    public void TypeChangeReadsOnlyLowOperandByteUpdatesBeforeAdvanceAndDoesNotCache()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x5A, 0xFF00, 0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x5C, unchecked((uint)(-123 << 16)));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x60, 456 << 16);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x64, 789 << 16);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1003, 1).MarkUnknown(0x208C, 6), Core = frame.Core.MarkUnknown(0x2797454, 128) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal((byte)0, result.Arena.Read(0x2018, 1)[0]);
        Assert.Equal((ushort)0, SlotWord(result, 0xB0));
        Assert.Equal(new short[] { -123, 456, 789 }, Coordinates(result));
        Assert.Equal(Address(0x1006), Pc(result));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x208C, 6));
    }

    [Fact]
    public void CachedActorPositionIsSignedAndSelectedUpdateIsFollowedByPositionCache()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x5E);
        frame = frame with { Arena = frame.Arena.Write(0x201B, new byte[] { 2 }) };
        short[] position = [-32768, -2, 32767];
        for (int i = 0; i < 3; i++)
        {
            frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x94u + (uint)(2 * i), unchecked((ushort)position[i]));
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50 + 4 * i, (uint)((10 + i) << 16));
        }
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(position, Coordinates(result));
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(unchecked((uint)(position[i] << 16)), SlotDword(result, 0x5C + 4 * i));
            Assert.Equal((ushort)(10 + i), SlotWord(result, 0x8C + 2 * i));
        }
        Assert.Equal(Address(0x1002), Pc(result));
    }

    [Fact]
    public void TypeThreeChangeUsesActualReferenceLookupWithoutRefreshingPositionCache()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x5A, 3, 0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x14, 99);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 99);
        for (int i = 0; i < 3; i++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94 + 2 * i, (ushort)(10 + i));
        }
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 1, 0xFF }), Arena = frame.Arena.MarkUnknown(0x208C, 6) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(new short[] { 10, 11, 12 }, Coordinates(result));
        Assert.Equal((ushort)0xFFFF, SlotWord(result, 0xB0));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x208C, 6));
    }

    [Theory]
    [InlineData(0, 0, 0x0001, 0x8000, 0x8001, true)]
    [InlineData(1, 0x0002, 0x0002, 0xFFFF, 0xFFFF, false)]
    [InlineData(1, 0x0002, 0x0001, 0xFFFF, 0xFFFE, true)]
    [InlineData(8, 0, 0x8001, 0xFFFF, 0x7FFE, true)]
    public void ActorFlagsUseCapturedPriorFlagsAndPreserveActualUpperWord(int mode, int prior, int mask, int current, int expected, bool written)
    {
        NativeBattleWaterExecutionFrame frame = FlagsFixture((ushort)((mode << 12) | 0x45), (ushort)mask, (ushort)prior, (ushort)current);
        int writes = 0;
        NativeBattleWaterPresentationServices services = Services() with
        {
            ReadActorFlags = (value, pointer) =>
            {
                Assert.Equal(Address(0x6000), pointer);
                return BinaryPrimitives.ReadUInt16LittleEndian(value.Arena.Read(0x6000, 2));
            },
            WriteActorFlags = (value, pointer, flags) =>
            {
                writes++;
                return value with { Arena = WriteWord(value.Arena, 0x6000, flags) };
            }
        };
        NativeBattleWaterExecutionFrame result = Execute(frame, services);
        Assert.Equal((ushort)expected, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(0x6000, 2)));
        Assert.Equal(new byte[] { 0xA5, 0x5A }, result.Arena.Read(0x6002, 2));
        Assert.Equal(written ? 1 : 0, writes);
        Assert.Equal(Address(0x1004), Pc(result));
    }

    [Fact]
    public void ConditionalFlagNoWriteDefersCurrentFlagsButUnsupportedModeStillConsumesCapturedOwners()
    {
        NativeBattleWaterExecutionFrame frame = FlagsFixture(0x1045, 2, 2, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x6000, 4) };
        Assert.Equal(Address(0x1004), Pc(Execute(frame)));
        frame = FlagsFixture(0x2045, 0, 0, 0);
        Assert.Throws<NotSupportedException>(() => Execute(frame));
        frame = frame with { Core = frame.Core.MarkUnknown(NativeBattleScriptedEffectMemory.Address + 0x52, 2) };
        Assert.Throws<NotSupportedException>(() => Execute(frame));
        frame = Fixture(0x45, 0);
        frame = frame with { Arena = frame.Arena.Write(0x201B, new byte[] { 7 }) };
        Assert.Throws<NotSupportedException>(() => Execute(frame));
    }

    [Fact]
    public void SoundUsesCurrentRelativeDescriptorUnsignedHighIndexAndReloadsPcAfterCallback()
    {
        ushort instruction = (ushort)((127 << 9) | 0x2B);
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0x8000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797754, Address(0x6000));
        frame = ArenaDword(frame, 0x61FC, 0x240);
        int calls = 0;
        NativeBattleWaterPresentationServices services = Services() with { PlaySound501330 = (value, descriptor, argument, pan) =>
        {
            calls++;
            Assert.Equal(Address(0x6240), descriptor);
            Assert.Equal(short.MinValue, argument);
            Assert.Equal((byte)0x80, pan);
            return WaterSchedulerFixture.CoreDword(value, 0x2797450, 0xFFFFFFFE);
        } };
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = Execute(frame, services);
        Assert.Equal(1, calls);
        Assert.Equal(2u, Pc(result));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Execute(restored, services)));
        frame = ArenaDword(frame, 0x61FC, 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        Execute(frame, services with { PlaySound501330 = (value, descriptor, _, _) =>
        {
            Assert.Equal(Address(0x6100), descriptor);
            return value;
        } });
    }

    private static NativeBattleWaterExecutionFrame FlagsFixture(ushort instruction, ushort mask, ushort prior, ushort flags)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, mask);
        frame = frame with { Arena = frame.Arena.Write(0x201B, new byte[] { 2 }).Write(0x6002, new byte[] { 0xA5, 0x5A }) };
        frame = WaterSchedulerFixture.CoreDword(frame, NativeBattleScriptedEffectMemory.Address + 0x68, Address(0x6000));
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x52, prior);
        return frame with { Arena = WriteWord(frame.Arena, 0x6000, flags) };
    }

    private static NativeBattleWaterExecutionFrame Fixture(params ushort[] words)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, words[0]).WriteTemporaryByte(0x142, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        frame = frame with { Core = frame.Core.Write(0x2797554, new byte[] { 0xFF, 0xFF, 0xFF }) };
        for (int i = 0; i < words.Length; i++)
        {
            frame = frame with { Arena = WriteWord(frame.Arena, 0x1000 + 2 * i, words[i]) };
        }
        return frame;
    }

    private static NativeBattleWaterPresentationServices Services() => new((_, _) => throw new Xunit.Sdk.XunitException("Unexpected actor flags read."), (_, _, _) => throw new Xunit.Sdk.XunitException("Unexpected actor flags write."), (_, _, _, _) => throw new Xunit.Sdk.XunitException("Unexpected sound call."));
    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterPresentationServices? services = null) => NativeBattleWaterPresentationCommands.Execute(frame, new(0x1000, 0x100, Address(0x1000)), Bank, 0, services ?? Services());
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
    private static uint SlotDword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(0x2000 + offset, 4));
    private static ushort SlotWord(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(0x2000 + offset, 2));
    private static short[] Coordinates(NativeBattleWaterExecutionFrame frame) => Enumerable.Range(0, 3).Select(i => unchecked((short)SlotWord(frame, 0x94 + 2 * i))).ToArray();
    private static NativeBattleEffectArena WriteWord(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] data = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(data, value);
        return arena.Write(offset, data);
    }
    private static NativeBattleWaterExecutionFrame ArenaDword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        return frame with { Arena = frame.Arena.Write(offset, data) };
    }
}
