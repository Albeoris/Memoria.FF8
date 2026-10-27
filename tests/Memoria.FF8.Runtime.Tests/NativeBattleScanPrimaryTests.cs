using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanPrimaryTests
{
    [Fact]
    public void Timeline_uses_actual_ordered_children_and_each_wrapped_countdown_expires_only_after_zero()
    {
        var fixture = Fixture();
        NativeBattleScanPrimaryFrame frame = Write(fixture.Frame, fixture.Address, task => task.WriteWord(0x24, 0xFFFF));
        frame = Tick(frame, fixture);
        Assert.Equal((ushort)0, Read(frame, fixture.Address).ReadWord(0x24));
        Assert.Equal((ushort)0x7825, frame.Models[4].Flags0);
        Assert.Equal((ushort)1, frame.Highlight269AA90);
        Assert.Equal((byte)2, Read(frame, fixture.Address).ReadByte(0x28));
        Assert.Equal((ushort)0, frame.Scan.Effect.Pools[2].Slots[1].ReadWord(0x68));
        for (int index = 0; index < 15; index++)
        {
            frame = Tick(frame, fixture);
        }
        Assert.Equal((byte)1, Read(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)0, Read(frame, fixture.Address).ReadWord(0x66));
        frame = Tick(frame, fixture);
        Assert.Equal((byte)2, Read(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)16, Read(frame, fixture.Address).ReadWord(0x66));
        for (int index = 0; index < 17; index++)
        {
            frame = Tick(frame, fixture);
        }
        Assert.Equal((byte)3, Read(frame, fixture.Address).ReadByte(0x29));
        for (int index = 0; index < 33; index++)
        {
            frame = Tick(frame, fixture);
        }
        Assert.Equal((byte)4, Read(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)0, frame.Highlight269AA90);
        frame = Tick(frame, fixture);
        Assert.Equal((ushort)1, frame.Scan.Effect.Pools[2].Slots[5].ReadWord(0x68));
        for (int index = 0; index < 9; index++)
        {
            frame = Tick(frame, fixture);
        }
        Assert.Equal((byte)6, Read(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)8, Read(frame, fixture.Address).ReadByte(0x28));
        Assert.Equal(new uint[] { 0x84D4B0, 0x84E810, 0x84D5B0, 0x84ED90, 0x84EFF0, 0x84E810, 0x84F860 }, Live(frame, 2).Select(task => task.Callback));
        Assert.Equal(new uint[] { 0x84F300, 0x84F430 }, Live(frame, 4).Select(task => task.Callback));
        Assert.All(Live(frame, 2).Skip(1).Concat(Live(frame, 4)), task =>
        {
            Assert.Equal(fixture.Address, task.ReadDword(0x18));
            Assert.Equal(0x269AAE0u, task.ReadDword(0x10));
            Assert.Equal((byte)4, task.ReadByte(0x2D));
        });
        NativeBattleScanPrimaryFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleScanPrimaryFrame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Tick(frame, fixture)), NativeBattleTestSnapshots.Json(Tick(restored, fixture)));
        Assert.Equal((byte)6, Read(Tick(frame, fixture), fixture.Address).ReadByte(0x29));
    }

    [Theory]
    [InlineData(0x8000, 0x7FFF, 1)]
    [InlineData(0, 16, 2)]
    [InlineData(0xFFFF, 16, 2)]
    public void Timer_subtraction_wraps_WORD_before_signed_comparison(ushort before, ushort after, byte phase)
    {
        var fixture = Fixture();
        NativeBattleScanPrimaryFrame frame = Write(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 1).WriteWord(0x66, before));
        frame = Tick(frame, fixture);
        Assert.Equal(after, Read(frame, fixture.Address).ReadWord(0x66));
        Assert.Equal(phase, Read(frame, fixture.Address).ReadByte(0x29));
    }

    [Theory]
    [InlineData(0, null, 0x850A30u)]
    [InlineData(1, -8192, 0x84E810u)]
    [InlineData(1, -8191, 0u)]
    public void Exit_branch_waits_actual_children_and_uses_group_and_camera_boundary(ushort lastGroup, int? angle, uint conditional)
    {
        var fixture = Fixture();
        NativeBattleScanPrimaryFrame frame = fixture.Frame with { CameraAngle3A = angle is int value ? (short)value : null };
        frame = Write(frame, 0x269AAE0, task => task.WriteWord(0x58, lastGroup));
        frame = Write(frame, fixture.Address, task => task.WriteByte(0x29, 6));
        frame = frame with { Models = frame.Models.SetItem(4, frame.Models[4] with { Flags0 = 0xFFFF }) };
        NativeBattleScanPrimaryStep step = NativeBattleScanPrimary.Advance84D4B0(frame, fixture.Address, fixture.Packets);
        frame = step.Frame;
        Assert.Equal(0, step.Result);
        Assert.Equal((byte)7, Read(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)0x87FF, frame.Models[4].Flags0);
        uint[] expected = conditional == 0 ? [0x8507F0] : [conditional, 0x8507F0];
        Assert.Equal(expected, Live(frame, 2).Skip(1).Select(task => task.Callback));
        if (conditional == 0x84E810)
        {
            Assert.Equal((ushort)3, frame.Scan.Effect.Pools[2].Slots[1].ReadWord(0x68));
        }
        NativeBattleScanPrimaryFrame waiting = Tick(frame, fixture);
        Assert.Equal((byte)7, Read(waiting, fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)expected.Length, Read(waiting, fixture.Address).ReadByte(0x28));
        NativeBattleEffectHierarchy hierarchy = new(waiting.Scan.Effect.Pools);
        foreach (int slot in Enumerable.Range(1, expected.Length))
        {
            hierarchy = hierarchy.Complete8DC530(hierarchy.Address(2, slot));
        }
        waiting = waiting with { Scan = waiting.Scan with { Effect = waiting.Scan.Effect with { Pools = hierarchy.Pools } } };
        NativeBattleScanPrimaryStep completed = NativeBattleScanPrimary.Advance84D4B0(waiting, fixture.Address, fixture.Packets);
        Assert.Equal(2, completed.Result);
        Assert.Equal((byte)8, Read(completed.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)1, Read(completed.Frame, fixture.Address).Flags);
        Assert.Equal((byte)0, Read(completed.Frame, fixture.Parent).ReadByte(0x28));
        Assert.Equal((byte)1, Read(completed.Frame, 0x269AAE0).ReadByte(0x28));
        NativeBattleEffectHierarchy retired = new NativeBattleEffectHierarchy(completed.Frame.Scan.Effect.Pools).Advance(2, (current, address) => new(current, address == fixture.Address ? completed.Result : 0)).Hierarchy;
        Assert.Equal((ushort)0, retired.Read(fixture.Address).Flags);
        Assert.Equal((byte)0, retired.Read(fixture.Parent).ReadByte(0x28));
    }

    [Fact]
    public void Exit_group_comparison_sign_extends_byte_and_word_before_spawning_last_group_camera()
    {
        var fixture = Fixture();
        NativeBattleScanPrimaryFrame frame = Write(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 6).WriteByte(0x2A, 0xFF));
        frame = Write(frame, 0x269AAE0, task => task.WriteWord(0x58, 0xFFFF));
        frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Parameters = frame.Scan.Effect.Parameters! with { HeaderIndex7C = 1 } } } };
        frame = Tick(frame, fixture);
        Assert.Equal(new uint[] { 0x850A30, 0x8507F0 }, Live(frame, 2).Skip(1).Select(task => task.Callback));
        Assert.Equal((byte)0xFF, frame.Scan.Effect.Pools[2].Slots[1].ReadByte(0x2A));
        Assert.Equal((byte)4, frame.Scan.Effect.Pools[2].Slots[1].ReadByte(0x2D));
    }

    [Fact]
    public void Busy_phase6_does_not_read_camera_clear_flags_or_invent_completion()
    {
        var fixture = Fixture();
        NativeBattleScanPrimaryFrame frame = Tick(fixture.Frame, fixture);
        frame = Write(frame, fixture.Address, task => task.WriteByte(0x29, 6));
        frame = frame with { CameraAngle3A = null };
        NativeBattleScanPrimaryStep step = NativeBattleScanPrimary.Advance84D4B0(frame, fixture.Address, fixture.Packets);
        Assert.Equal(0, step.Result);
        Assert.Equal((byte)6, Read(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)0x7825, step.Frame.Models[4].Flags0);
        Assert.Equal((byte)2, Read(step.Frame, fixture.Address).ReadByte(0x28));
    }

    private static NativeBattleScanPrimaryFrame Tick(NativeBattleScanPrimaryFrame frame, (NativeBattleScanPrimaryFrame Frame, uint Address, uint Parent, NativeBattleActionPacketsState Packets) fixture) => NativeBattleScanPrimary.Advance84D4B0(frame, fixture.Address, fixture.Packets).Frame;
    private static IEnumerable<NativeBattleModulePoolTask> Live(NativeBattleScanPrimaryFrame frame, int pool) => frame.Scan.Effect.Pools[pool].Slots.Where(task => (task.Flags & 1) != 0);
    private static NativeBattleModulePoolTask Read(NativeBattleScanPrimaryFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);

    private static NativeBattleScanPrimaryFrame Write(NativeBattleScanPrimaryFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(address, change(hierarchy.Read(address)));
        return frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
    }

    private static (NativeBattleScanPrimaryFrame Frame, uint Address, uint Parent, NativeBattleActionPacketsState Packets) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        NativeBattleEffectHierarchyAppend parent = hierarchy.Append8DC540(1, 0x84D230, 0x58, 0x269AAE0, (_, _, _) => new(2, 4));
        NativeBattleEffectHierarchyAppend child = parent.Hierarchy.Append8DC540(2, 0x84D4B0, 0x78, parent.Address, (_, _, _) => new(2, 4));
        scan = scan with { Effect = scan.Effect with { Pools = child.Hierarchy.Pools } };
        return (new(scan, Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0x25)).ToImmutableArray(), 0, null), child.Address, parent.Address, packets);
    }
}
