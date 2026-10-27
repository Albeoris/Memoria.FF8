using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanResultTaskTests
{
    [Theory]
    [InlineData(0, 2, 1, 1)]
    [InlineData(1, 2, 2, 3)]
    [InlineData(0x7FFF, 2, 0x8000, 1)]
    public void Alternate_result_advances_pages_then_finishes_instead_of_wrapping(ushort page, ushort count, ushort expectedPage, byte expectedPhase)
    {
        var fixture = Fixture(0x850FD0);
        NativeBattleScanResultTaskFrame frame = Modify(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 2).WriteWord(0x6C, page).WriteWord(0x6A, count).WriteWord(0x66, 17));
        NativeBattleScanResultTaskStep next = NativeBattleScanResultTask.Advance850FD0(frame, fixture.Address, 0x20, fixture.Services);
        Assert.Equal(expectedPage, Task(next.Frame, fixture.Address).ReadWord(0x6C));
        Assert.Equal(expectedPhase, Task(next.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)17, Task(next.Frame, fixture.Address).ReadWord(0x66));
        Assert.Equal(0, next.Result);
        if (expectedPhase == 3)
        {
            NativeBattleScanResultTaskFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleScanResultTaskFrame>(NativeBattleTestSnapshots.Json(next.Frame))!;
            NativeBattleScanResultTaskStep closed = NativeBattleScanResultTask.Advance850FD0(restored, fixture.Address, 0x20, fixture.Services);
            Assert.Equal(2, closed.Result);
            Assert.Equal((ushort)1, closed.Frame.Complete269D93A);
        }
    }

    [Fact]
    public void Alternate_result_last_page_timer_finishes_and_rejects_the_normal_entry()
    {
        var fixture = Fixture(0x850FD0);
        NativeBattleScanResultTaskFrame frame = Modify(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 2).WriteWord(0x6A, 1).WriteWord(0x66, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0, fixture.Services));
        NativeBattleScanResultTaskStep next = NativeBattleScanResultTask.Advance850FD0(frame, fixture.Address, 0, fixture.Services);
        Assert.Equal((byte)3, Task(next.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)1, Task(next.Frame, fixture.Address).ReadWord(0x6C));
        Assert.Equal((ushort)0, Task(next.Frame, fixture.Address).ReadWord(0x66));
    }

    [Fact]
    public void First_phase_builds_actual_pages_and_does_not_initialize_retained_page_word()
    {
        var fixture = Fixture();
        NativeBattleScanResultTaskFrame frame = Modify(fixture.Frame, fixture.Address, task => task.WriteWord(0x6C, 7));
        NativeBattleScanResultTaskStep result = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0x1234, fixture.Services);
        Assert.Equal(1u, result.Frame.Results.Count);
        Assert.Equal((ushort)1, Task(result.Frame, fixture.Address).ReadWord(0x6A));
        Assert.Equal((ushort)7, Task(result.Frame, fixture.Address).ReadWord(0x6C));
        Assert.Equal((ushort)0, result.Frame.Complete269D93A);
        Assert.Equal((byte)1, Task(result.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(0x1234u, Task(result.Frame, fixture.Address).ReadDword(0x5C));
        Assert.Equal(0u, Task(result.Frame, fixture.Address).ReadDword(0x60));
    }

    [Fact]
    public void Page_open_uses_real_pointer_rect_speed_and_exact_forty_five_callback_timer()
    {
        var fixture = Fixture();
        NativeBattleScanResultTaskFrame frame = NativeBattleScanResultTask.Advance84FAF0(fixture.Frame, fixture.Address, 0, fixture.Services).Frame;
        frame = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0, fixture.Services).Frame;
        NativeBattleScanByteOwner row = frame.Text.Windows.Row(1);
        Assert.Equal(NativeBattleScanResults.PageB68810(frame.Results, 0, 0), row.ReadDword(8));
        Assert.Equal(new NativeBattleScanRect(16, 180, 296, 28).ToBytes(), row.Bytes.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal((ushort)0, row.ReadWord(0x10));
        Assert.Equal((ushort)4096, row.ReadWord(0x1E));
        Assert.Equal((byte)1, row.ReadByte(0x16));
        Assert.Equal((ushort)45, Task(frame, fixture.Address).ReadWord(0x66));
        for (int callback = 1; callback < 45; callback++)
        {
            frame = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0, fixture.Services).Frame;
            Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        }
        frame = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0, fixture.Services).Frame;
        Assert.Equal((byte)1, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x66));
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x6C));
    }

    [Fact]
    public void Rising_twenty_advances_once_without_timer_decrement_but_repeated_held_twenty_counts_down()
    {
        var fixture = Fixture();
        NativeBattleScanResultTaskFrame frame = Modify(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 2).WriteWord(0x6A, 3).WriteWord(0x66, 45));
        frame = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0x20, fixture.Services).Frame;
        Assert.Equal((ushort)1, Task(frame, fixture.Address).ReadWord(0x6C));
        Assert.Equal((ushort)45, Task(frame, fixture.Address).ReadWord(0x66));
        Assert.Equal((byte)1, Task(frame, fixture.Address).ReadByte(0x29));
        frame = Modify(frame, fixture.Address, task => task.WriteByte(0x29, 2));
        frame = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0x20, fixture.Services).Frame;
        Assert.Equal((ushort)1, Task(frame, fixture.Address).ReadWord(0x6C));
        Assert.Equal((ushort)44, Task(frame, fixture.Address).ReadWord(0x66));
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(0x20u, Task(frame, fixture.Address).ReadDword(0x60));
    }

    [Fact]
    public void Held_forty_has_priority_and_closes_only_in_following_phase_with_actual_completion()
    {
        var fixture = Fixture();
        NativeBattleScanResultTaskFrame frame = Modify(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 2).WriteWord(0x66, 17));
        NativeBattleScanResultTaskStep dismiss = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0x60, fixture.Services);
        Assert.Equal(0, dismiss.Result);
        Assert.Equal((byte)3, Task(dismiss.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)17, Task(dismiss.Frame, fixture.Address).ReadWord(0x66));
        Assert.Equal((ushort)9, dismiss.Frame.Complete269D93A);
        NativeBattleScanResultTaskStep closed = NativeBattleScanResultTask.Advance84FAF0(dismiss.Frame, fixture.Address, 0x40, fixture.Services);
        Assert.Equal(2, closed.Result);
        Assert.Equal((ushort)1, closed.Frame.Complete269D93A);
        Assert.Equal((ushort)0xF000, closed.Frame.Text.Windows.Row(1).ReadWord(0x1E));
        Assert.Equal(0u, closed.Frame.Text.Windows.Row(1).ReadDword(8));
        Assert.Equal((byte)4, Task(closed.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(closed.Frame.Scan.Effect.Pools).Read(closed.Frame.Scan.Effect.Pools[0].BankAddress).ReadByte(0x28));
    }

    [Theory]
    [InlineData(0x7FFF, 3, 0x8000)]
    [InlineData(2, 3, 0)]
    public void Page_increment_comparison_is_signed_word_and_wrapping(ushort page, ushort count, ushort expected)
    {
        var fixture = Fixture();
        NativeBattleScanResultTaskFrame frame = Modify(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 2).WriteWord(0x6C, page).WriteWord(0x6A, count));
        frame = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0x20, fixture.Services).Frame;
        Assert.Equal(expected, Task(frame, fixture.Address).ReadWord(0x6C));
    }

    [Fact]
    public void Independent_page_timer_JSON_continuation_keeps_input_edges_and_text_state()
    {
        var fixture = Fixture();
        NativeBattleScanResultTaskFrame frame = NativeBattleScanResultTask.Advance84FAF0(fixture.Frame, fixture.Address, 0, fixture.Services).Frame;
        frame = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0, fixture.Services).Frame;
        NativeBattleScanResultTaskFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleScanResultTaskFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleScanResultTaskStep left = NativeBattleScanResultTask.Advance84FAF0(frame, fixture.Address, 0x20, fixture.Services);
        NativeBattleScanResultTaskStep right = NativeBattleScanResultTask.Advance84FAF0(restored, fixture.Address, 0x20, fixture.Services);
        Assert.Equal(NativeBattleTestSnapshots.Json(left), NativeBattleTestSnapshots.Json(right));
    }

    private static NativeBattleModulePoolTask Task(NativeBattleScanResultTaskFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);
    private static NativeBattleScanResultTaskFrame Modify(NativeBattleScanResultTaskFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(address, change(hierarchy.Read(address)));
        return frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
    }

    private static (NativeBattleScanResultTaskFrame Frame, uint Address, NativeBattleScanResultServices Services) Fixture(uint callback = 0x84FAF0)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(0, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(0, 1, 1, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(2, callback, 0x78, hierarchy.Address(0, 0), (_, _, _) => new(0, 0));
        scan = scan with { Effect = scan.Effect with { Pools = appended.Hierarchy.Pools } };
        ScanResultTestSource source = new();
        return (new(scan, NativeBattleScanResultMemory.FromColdProcess(), ScanTextFixture.Frame(), 9), appended.Address, source.Services);
    }
}
