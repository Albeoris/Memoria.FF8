using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicShapeSelectionTests
{
    private const uint Static = 0x1874894;
    private const uint Map = Static + 0x100;
    private const uint Manager = 0x2797690;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x1FA8, 0x858, 8, 0x2000);
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 4, 0x1000);

    [Fact]
    public void Resolver_consumes_current_pointer_header_and_directory_entry_instead_of_immutable_file_bytes()
    {
        var fixture = Fixture();
        NativeBattleWaterExecutionFrame frame = fixture.Frame;
        Assert.Equal((Address(0x5040), Address(0x5140)), NativeBattleWaterShapeSelection.ResolveCinematic(frame, fixture.Resources, 2));
        frame = ArenaDword(frame, 0x500C, 0x80);
        frame = ArenaDword(frame, 0x508C, 0x60);
        Assert.Equal((Address(0x5080), Address(0x50E0)), NativeBattleWaterShapeSelection.ResolveCinematic(frame, fixture.Resources, 2));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, Address(0x7000));
        frame = ArenaDword(frame, 0x700C, 0x80);
        frame = ArenaDword(frame, 0x708C, 0x200);
        Assert.Equal((Address(0x7080), Address(0x7280)), NativeBattleWaterShapeSelection.ResolveCinematic(frame, fixture.Resources, 2));
        Assert.Equal(new byte[] { 0xFF }, fixture.Resources.Files[0].Bytes);
    }

    [Fact]
    public void Positive_selection_does_not_consume_directory_count_or_shape_payload()
    {
        var fixture = Fixture();
        NativeBattleWaterExecutionFrame frame = fixture.Frame with { Arena = fixture.Frame.Arena.MarkUnknown(0x5040, 4).MarkUnknown(0x5140, 0x100) };
        Assert.Equal((Address(0x5040), Address(0x5140)), NativeBattleWaterShapeSelection.ResolveCinematic(frame, fixture.Resources, 2));
    }

    [Fact]
    public void Signed_negative_index_reads_actual_static_owner_before_map_and_uses_current_source_three()
    {
        var fixture = Fixture();
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(fixture.Frame, 0x2798A74, Address(0x7000));
        frame = ArenaDword(frame, 0x700C, 0x40);
        frame = ArenaDword(frame, 0x7040, 0x60);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x7044, 4) };
        Assert.Equal((Address(0x7040), Address(0x70A0)), NativeBattleWaterShapeSelection.ResolveCinematic(frame, fixture.Resources, -1));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterShapeSelection.ResolveCinematic(frame, fixture.Resources, short.MinValue));
    }

    [Fact]
    public void Current_mapping_owner_mismatch_fails_before_consuming_any_file_header()
    {
        var fixture = Fixture();
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(fixture.Frame, Manager + 0xAC, Map + 1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x500C, 4) };
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => NativeBattleWaterShapeSelection.ResolveCinematic(frame, fixture.Resources, 2));
        Assert.Contains("mapping owner", exception.Message);
    }

    [Fact]
    public void Raw003C_delegate_preserves_exact_registration_partial_writes_without_loading_shape_payload()
    {
        var fixture = Fixture();
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(fixture.Frame, 0, 0xCC, uint.MaxValue);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, uint.MaxValue);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC0, 0x12345678);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x5140, 0x100) };
        int resolves = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleWaterRectangleCommands.Execute(frame, Program, Bank, 0, (current, index) =>
        {
            resolves++;
            Assert.Equal((short)2, index);
            Assert.Equal(0x02FFFFFFu, WaterSchedulerFixture.ReadSlotDword(current, 0, 0xCC));
            Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0xBC));
            return NativeBattleWaterShapeSelection.ResolveCinematic(current, fixture.Resources, index).Selected;
        }, (_, _) => throw new InvalidOperationException("Shape command consulted a sequence selector."));
        Assert.Equal(1, resolves);
        Assert.Equal(Address(0x5140), WaterSchedulerFixture.ReadSlotDword(result, 0, 0xD8));
        Assert.Equal(0x12345678u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xC0));
        Assert.Equal((byte)2, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal(new byte[] { 0, 0xFF }, result.Core.Read(0x2797554, 2));
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void Raw003C_existing_draw_order_entry_is_preserved_and_independent_snapshot_repeats_registration()
    {
        var fixture = Fixture();
        NativeBattleWaterExecutionFrame frame = fixture.Frame with { Core = fixture.Frame.Core.Write(0x2797554, [0x80, 0xFF]) };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleCinematicResources restoredResources = JsonSerializer.Deserialize<NativeBattleCinematicResources>(JsonSerializer.Serialize(fixture.Resources))!;
        NativeBattleWaterExecutionFrame first = Execute(frame, fixture.Resources);
        NativeBattleWaterExecutionFrame next = Execute(restored, restoredResources);
        Assert.Equal(new byte[] { 0x80, 0xFF }, first.Core.Read(0x2797554, 2));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(next));
    }

    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicResources resources) => NativeBattleWaterRectangleCommands.Execute(frame, Program, Bank, 0, (current, index) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, index).Selected, (_, _) => throw new InvalidOperationException());

    private static (NativeBattleWaterExecutionFrame Frame, NativeBattleCinematicResources Resources) Fixture()
    {
        byte[] tables = new byte[0x200];
        BitConverter.GetBytes(0x100u).CopyTo(tables, 0x10);
        tables[0xFF] = 3;
        tables[0x102] = 1;
        NativeVibrationBank vibration = new(new string('0', 64), [0], new byte[4].ToImmutableArray());
        NativeBattleCinematicResources resources = new(Static, new("static", tables.ToImmutableArray()), new(1, Static, Static + 0x20, Static + 0x40, Static + 0x48, Static + 0x50, Static + 0x51), vibration) { Files = [new(1, "source", [0xFF])] };
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x003C).WriteTemporaryWord(0x13E, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xAC, Map);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, Address(0x5000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x1002, [2, 0]) };
        frame = ArenaDword(frame, 0x500C, 0x40);
        frame = ArenaDword(frame, 0x504C, 0x100);
        return (frame, resources);
    }

    private static NativeBattleWaterExecutionFrame ArenaDword(NativeBattleWaterExecutionFrame frame, int offset, uint value) => frame with { Arena = frame.Arena.Write(offset, BitConverter.GetBytes(value)) };
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
}
