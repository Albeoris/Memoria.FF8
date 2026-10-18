using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterScriptSchedulerTests
{
    [Theory]
    [InlineData(true, 0x80001000u, 0x1000u)]
    [InlineData(false, 0x1000u, 0x80001000u)]
    public void PassUsesSignedPcAndDoesNotConsumeSkippedWaits(bool pre, uint eligible, uint skipped)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0, eligible);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 4, skipped);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x200E, 4) };
        int calls = 0;
        frame = NativeBattleWaterScriptScheduler.RunScriptPass(pre, frame, (current, pc) =>
        {
            Assert.Equal(eligible, pc);
            return 0xFE09;
        }, (current, selector) =>
        {
            calls++;
            Assert.Equal((ushort)9, selector);
            Assert.Equal((ushort)0xFE09, current.ReadTemporaryWord(0x14A));
            return WaterSchedulerFixture.CoreDword(current.WriteTemporaryWord(0x13E, 3), 0x2797450, 0);
        });
        Assert.Equal(1, calls);
        Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0));
        Assert.Equal(skipped, WaterSchedulerFixture.ReadSlotDword(frame, 0, 4));
        Assert.Equal((ushort)0, frame.ReadTemporaryWord(0x13E));
        Assert.Equal(pre ? 0x12345678u : 0xA5A5A5A5u, frame.ReadTemporaryDword(0x14C));
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(1, 0xFFFE)]
    public void PositiveWaitSubtractsTickAndAlwaysDefersThisPass(ushort initial, ushort expected)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(WaterSchedulerFixture.Create(), 0, 0, 0x1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC, initial);
        frame = NativeBattleWaterScriptScheduler.RunScriptPass(false, frame, (_, _) => throw new Xunit.Sdk.XunitException("Deferred wait fetched an instruction."), (_, _) => throw new Xunit.Sdk.XunitException("Deferred wait executed an instruction."));
        Assert.Equal(expected, WaterSchedulerFixture.ReadSlotWord(frame, 0, 0xC));
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0));
    }

    [Theory]
    [InlineData(0xFFFB, 2, 0xFFFA)]
    [InlineData(0x8000, 0xFFFF, 0x7FFC)]
    public void ZeroYieldContinuesWithoutSignRetestAndNonzeroYieldAccumulatesWrappingDebt(ushort initial, ushort yielded, ushort expected)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(WaterSchedulerFixture.Create(), 0, 0, 0x1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC, initial);
        int executed = 0;
        List<uint> fetched = [];
        frame = NativeBattleWaterScriptScheduler.RunScriptPass(false, frame, (_, pc) =>
        {
            fetched.Add(pc);
            return 9;
        }, (current, _) =>
        {
            executed++;
            current = WaterSchedulerFixture.CoreDword(current, 0x2797450, 0x90001000);
            return current.WriteTemporaryWord(0x13E, executed == 1 ? (ushort)0 : yielded);
        });
        Assert.Equal(new uint[] { 0x1000, 0x90001000 }, fetched);
        Assert.Equal(expected, WaterSchedulerFixture.ReadSlotWord(frame, 0, 0xC));
        Assert.Equal(0x90001000u, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0));
        Assert.Equal((ushort)0, frame.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void YieldReloadsChangedStreamCurrentSlotAndTickThenStoresPcThere()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(WaterSchedulerFixture.Create(), 0, 0, 0x1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0xE, 5);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0xC8, 10);
        frame = NativeBattleWaterScriptScheduler.RunScriptPass(false, frame, (_, _) => 9, (current, _) =>
        {
            current = WaterSchedulerFixture.CoreDword(current, 0x27973E8, NativeBattleEffectNodeMemory.ArenaAddress + 0x2100);
            current = WaterSchedulerFixture.CoreDword(current, 0x2797450, 777);
            return current.WriteTemporaryByte(0x143, 1).WriteTemporaryWord(0x13E, 4);
        });
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0));
        Assert.Equal(777u, WaterSchedulerFixture.ReadSlotDword(frame, 1, 4));
        Assert.Equal((ushort)0xFFFF, WaterSchedulerFixture.ReadSlotWord(frame, 1, 0xE));
        Assert.Equal((byte)3, frame.ReadTemporaryByte(0x143));
    }

    [Fact]
    public void FilterHonorsHighBitAndRawCopyPreservesUnknownValidity()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x145, 1);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0, 0x1000);
        frame = WaterSchedulerFixture.SlotDword(frame, 1, 0, 0x80001000);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0, 0x81, 0xFF }) };
        bool[] known = frame.TemporaryFrame.Known.ToArray();
        known[0x139] = false;
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = known.ToImmutableArray() } };
        int calls = 0;
        frame = NativeBattleWaterScriptScheduler.RunScriptPass(true, frame, (_, pc) =>
        {
            Assert.Equal(0x80001000u, pc);
            return 9;
        }, (current, _) =>
        {
            calls++;
            return current.WriteTemporaryWord(0x13E, 3);
        });
        Assert.Equal(1, calls);
        Assert.False(frame.TemporaryFrame.Known[0x14D]);
        Assert.Equal(frame.TemporaryFrame.Bytes[0x139], frame.TemporaryFrame.Bytes[0x14D]);
    }

    [Fact]
    public void StopRemovalRereadsShiftedOrderAndJsonNextMatches()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0, 1, 0xFF }).Write(0x2797554, new byte[] { 0x80, 1, 0xFF, 0xAB }) };
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797080, 2);
        for (int index = 0; index < 2; index++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, index, 0, 0x1000u + (uint)(2 * index));
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x12, (ushort)(10 + index));
            frame = WaterSchedulerFixture.CoreWord(frame, 0x2797204 + (uint)(2 * index), (ushort)(10 + index));
        }
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        List<byte> visits = [];
        frame = NativeBattleWaterScriptScheduler.RunScriptPass(false, frame, (_, _) => 0, (current, _) =>
        {
            visits.Add(current.ReadTemporaryByte(0x142));
            return NativeBattleWaterStopCommand.StopRemoveAA5120(current);
        });
        restored = NativeBattleWaterScriptScheduler.RunScriptPass(false, restored, (_, _) => 0, (current, _) => NativeBattleWaterStopCommand.StopRemoveAA5120(current));
        Assert.Equal(new byte[] { 0, 1 }, visits);
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(0x2797080, 2)));
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xAB }, frame.Core.Read(0x2797554, 4));
        Assert.Equal(new byte[4], frame.Core.Read(0x2797204, 4));
    }

    [Fact]
    public void DiagnosticBudgetDoesNotManufactureYieldOrPcAdvance()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(WaterSchedulerFixture.Create(), 0, 0, 0x1000);
        int calls = 0;
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterScriptScheduler.RunScriptPass(false, frame, (_, _) => 9, (current, _) =>
        {
            calls++;
            Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            Assert.Equal((ushort)0, current.ReadTemporaryWord(0x13E));
            return current;
        }, 2));
        Assert.Equal(2, calls);
    }
}

internal static class WaterSchedulerFixture
{
    public static NativeBattleWaterExecutionFrame Create()
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess();
        core = core.Write(0x2797454, Enumerable.Repeat((byte)0xFF, 128).ToArray()).Write(0x2797454, new byte[] { 0 });
        core = core.Write(0x2797554, Enumerable.Repeat((byte)0xFF, 128).ToArray());
        NativeBattleWaterExecutionFrame frame = new(core, NativeBattleEffectArena.Clear(), new(0x1D9A000, Enumerable.Repeat((byte)0xA5, 0x180).ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(1));
        frame = CoreDword(frame, 0x27973EC, 0x2797690);
        frame = CoreDword(frame, 0x2797720, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        frame = CoreDword(frame, 0x27973E8, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        frame = CoreDword(frame, 0x279744C, NativeBattleScriptedEffectMemory.Address);
        frame = CoreWord(frame, 0x2797080, 1);
        frame = frame.WriteTemporaryDword(0x138, 0x12345678).WriteTemporaryByte(0x145, 0).WriteTemporaryByte(0x142, 0).WriteTemporaryByte(0x143, 0).WriteTemporaryByte(0x144, 0);
        frame = SlotWord(frame, 0, 0xC8, 3);
        return SlotWord(frame, 1, 0xC8, 3);
    }

    public static uint ReadCoreDword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(address, 4));
    public static uint ReadSlotDword(NativeBattleWaterExecutionFrame frame, int index, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(0x2000 + 0x100 * index + offset, 4));
    public static ushort ReadSlotWord(NativeBattleWaterExecutionFrame frame, int index, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(0x2000 + 0x100 * index + offset, 2));

    public static NativeBattleWaterExecutionFrame CoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }

    public static NativeBattleWaterExecutionFrame CoreWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }

    public static NativeBattleWaterExecutionFrame SlotDword(NativeBattleWaterExecutionFrame frame, int index, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(0x2000 + 0x100 * index + offset, bytes) };
    }

    public static NativeBattleWaterExecutionFrame SlotWord(NativeBattleWaterExecutionFrame frame, int index, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(0x2000 + 0x100 * index + offset, bytes) };
    }
}
