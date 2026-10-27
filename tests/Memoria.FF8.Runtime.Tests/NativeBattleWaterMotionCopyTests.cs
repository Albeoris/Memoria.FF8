using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterScalarReferenceCommandTests
{
    [Theory]
    [InlineData(0x0061, 0x50)]
    [InlineData(0x0062, 0x68)]
    [InlineData(0x0261, 0x68)]
    [InlineData(0xFE62, 0x68)]
    public void Motion_copy_uses_full_raw_comparison_skips_unselected_fields_and_reloads_PC_after_ordered_services(ushort instruction, int offset)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0x25, -2).WriteTemporaryWord(0x13E, 0x1234);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Reference, 0x100).MarkUnknown(Slot + (offset == 0x50 ? 0x68 : 0x50), 24) };
        frame = SlotDword(frame, Reference, offset, 0x12345678);
        frame = SlotDword(frame, Reference, offset + 8, 0x89ABCDEF);
        frame = SlotDword(frame, Reference, offset + 20, 0xFFFFFFFF);
        List<string> calls = [];
        NativeBattleWaterScalarReferenceServices services = Services() with
        {
            ResolveSlot = (_, selector) =>
            {
                Assert.Equal((short)-2, selector);
                calls.Add("resolve");
                return ArenaBase + Reference;
            },
            CachePosition = current =>
            {
                calls.Add("cache");
                Assert.Equal(0x12345678u, WaterSchedulerFixture.ReadSlotDword(current, 0, offset));
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0x10000);
            },
            UpdateSelected = current =>
            {
                calls.Add("update");
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0x20000);
            }
        };
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = Run(frame, services);
        Assert.Equal(new[] { "resolve", "cache", "update" }, calls);
        Assert.Equal(0x89ABCDEFu, WaterSchedulerFixture.ReadSlotDword(result, 0, offset + 8));
        Assert.Equal(0xFFFFFFFFu, WaterSchedulerFixture.ReadSlotDword(result, 0, offset + 20));
        Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(result, 0, offset + 4));
        Assert.Equal(0x20006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Slot + (offset == 0x50 ? 0x68 : 0x50), 24));
        calls.Clear();
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(replayFrame, services)));
    }

    [Fact]
    public void Reference_resolution_precedes_the_mask_operand_read()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x62, 0, -1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        NativeBattleWaterScalarReferenceServices services = Services() with { ResolveSlot = (_, selector) => throw new InvalidOperationException("selector first: " + selector) };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Run(frame, services));
        Assert.Equal("selector first: -1", error.Message);
    }
}
