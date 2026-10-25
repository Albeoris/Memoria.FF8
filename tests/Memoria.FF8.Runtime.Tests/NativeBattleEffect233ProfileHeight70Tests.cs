using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect233ProfileHeight70Tests
{
    [Fact]
    public void ThresholdEqualityContinuesAndSignedHeightUsesBothReferencesUnderRestoredOwner()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = ProfileWord(frame, 0x62, 35);
        frame = ProfileWord(frame, 0x64, 36);
        frame = ProfileWord(frame, 0x20, 999);
        frame = ProfileWord(frame, 0x22, -7);
        List<short> selectors = [];
        List<uint> owners = [];
        bool updated = false;
        NativeBattleCinematicProfileHeight70Services services = new((current, selector) =>
        {
            selectors.Add(selector);
            owners.Add(WaterSchedulerFixture.ReadCoreDword(current, 0x27973E8));
            return selectors.Count == 1 ? Address(0x2100) : Address(0x2200);
        }, ReadProfileWord, (current, kind) =>
        {
            Assert.Equal((byte)3, kind);
            Assert.Equal(Address(0x2000), WaterSchedulerFixture.ReadCoreDword(current, 0x27973E8));
            Assert.Equal(Address(0x1000), WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            Assert.Equal(unchecked((uint)(-14 << 16)), WaterSchedulerFixture.ReadSlotDword(current, 0, 0x60));
            updated = true;
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicProfileHeight70.Execute(frame, Program, services);
        Assert.True(updated);
        Assert.Equal(new short[] { -123, -1 }, selectors);
        Assert.Equal(new uint[] { Address(0x2000), Address(0x2100) }, owners);
        Assert.Equal(Address(0x1004), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(WaterSchedulerFixture.ReadSlotDword(frame, 0, 0x5C), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(WaterSchedulerFixture.ReadSlotDword(frame, 0, 0x64), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
    }

    [Theory]
    [InlineData(34)]
    [InlineData(-1)]
    public void OutsideSignedRadiusProducesZeroHeightWithoutReadingUndefinedThresholds(short radius)
    {
        NativeBattleWaterExecutionFrame frame = ProfileWord(Create(), 0x9E, radius);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x4000, 0x9E) };
        int reads = 0;
        NativeBattleCinematicProfileHeight70Services services = Services((current, address) =>
        {
            reads++;
            Assert.Equal(Address(0x409E), address);
            return ReadProfileWord(current, address);
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicProfileHeight70.Execute(frame, Program, services);
        Assert.Equal(1, reads);
        Assert.Equal(unchecked((uint)(-7 << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x60));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NullOrEqualRadiusWithoutStrictThresholdRejectsUnproducedCpuLocal(bool nullProfile)
    {
        NativeBattleWaterExecutionFrame frame = Create();
        if (nullProfile)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 1, 0xBC, 0);
        }
        else
        {
            for (int offset = 0x62; offset <= 0x9E; offset += 2)
            {
                frame = ProfileWord(frame, offset, 35);
            }
        }
        string before = JsonSerializer.Serialize(frame);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicProfileHeight70.Execute(frame, Program, Services()));
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void WrappedSignedSquareUsesEstablishedNativeIntegerSqrtResult()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 30000);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, unchecked((ushort)-30000));
        frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x94, unchecked((ushort)-30000));
        for (int index = 0; index < 3; index++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x98, 0);
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x96, 0);
        }
        frame = ProfileWord(frame, 0x62, 1);
        frame = ProfileWord(frame, 0x20, 5);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicProfileHeight70.Execute(frame, Program, Services());
        Assert.Equal(0x00050000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x60));
    }

    [Theory]
    [InlineData(0x9E)]
    [InlineData(0x62)]
    [InlineData(0x20)]
    public void ConsumedUnknownProfileWordDoesNotAcquireDefaultBytes(int offset)
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x4000 + offset, 2) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicProfileHeight70.Execute(frame, Program, Services()));
    }

    [Fact]
    public void CurrentProfileMutationAndJsonRestoreProduceIdenticalNextHeight()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicProfileHeight70.Execute(Create(), Program, Services());
        frame = ProfileWord(frame, 0x20, 123);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame next = NativeBattleCinematicProfileHeight70.Execute(frame, Program, Services());
        NativeBattleWaterExecutionFrame nextRestored = NativeBattleCinematicProfileHeight70.Execute(restored, Program, Services());
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(nextRestored));
        Assert.Equal(116u << 16, WaterSchedulerFixture.ReadSlotDword(next, 0, 0x60));
        Assert.Equal(Address(0x1008), WaterSchedulerFixture.ReadCoreDword(next, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Create()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x70);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        byte[] program = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(program, 0x70);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), -123);
        program.AsSpan(0, 4).CopyTo(program.AsSpan(4));
        frame = frame with { Arena = frame.Arena.Write(0x1000, program).Write(0x2018, [3]) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 30);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 40);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 3);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, unchecked((ushort)-10));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 4);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x9C, 0xFFFF);
        frame = WaterSchedulerFixture.SlotDword(frame, 1, 0xBC, Address(0x4000));
        frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x94, 6);
        frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x96, 3);
        frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x98, 8);
        frame = ProfileWord(frame, 0x9E, 100);
        frame = ProfileWord(frame, 0x62, 36);
        return ProfileWord(frame, 0x20, 5);
    }

    private static NativeBattleCinematicProfileHeight70Services Services(Func<NativeBattleWaterExecutionFrame, uint, short>? read = null) => new((_, selector) => selector == -123 ? Address(0x2100) : selector == -1 ? Address(0x2200) : throw new NotSupportedException("Synthetic selector has no current slot owner."), read ?? ReadProfileWord, (current, _) => current);

    private static short ReadProfileWord(NativeBattleWaterExecutionFrame frame, uint address)
    {
        if (address < Address(0x4000) || address > Address(0x409E))
        {
            throw new NotSupportedException("Synthetic profile read leaves its explicit current owner.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(checked((int)(address - Address(0))), 2));
    }

    private static NativeBattleWaterExecutionFrame ProfileWord(NativeBattleWaterExecutionFrame frame, int offset, short value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(0x4000 + offset, bytes) };
    }

    private static NativeBattleWaterScriptProgram Program => new(0x1000, 8, Address(0x1000));
    private static uint Address(int offset) => checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
