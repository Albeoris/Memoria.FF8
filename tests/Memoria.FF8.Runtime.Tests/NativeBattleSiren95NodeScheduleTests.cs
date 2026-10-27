using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95NodeScheduleTests
{
    [Fact]
    public void Final_node_wrapper_delays_without_reading_source_or_pose_before_its_signed_clock_boundary()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var added = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x258BA90, 0x73F900, 0x2A4, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        var frame = Put(added.Frame, added.Address, Read(added.Frame, added.Address).WriteWord(0x24, 44).WriteWord(0x298, 45));
        NativeBattleSiren95NodeServices services = new(fixture.Inputs.Packets, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), new(null, 216, 160, 108), 0, NativeBattleGteColorInterpolation.CodeConstants45F270, null);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        var step = NativeBattleSiren95Nodes.Advance745180(frame, added.Address, ordering, services);
        Assert.Equal((ushort)45, Read(step.Frame, added.Address).ReadWord(0x24));
        Assert.Equal((byte)0, Read(step.Frame, added.Address).ReadByte(0x29));
        Assert.Same(ordering, step.Ordering);
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Stage_completion_skips_schedule_reads_and_preserves_random()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var added = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x744F90, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        NativeBattleSiren95Frame frame = added.Frame;
        NativeBattleModulePoolTask task = Read(frame, added.Address).WriteByte(0x29, 2);
        uint director = frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        frame = Put(frame, added.Address, task);
        frame = frame with { Memory = frame.Memory with { Siren95 = frame.Memory.Siren95!.Write(director + 0x40, BitConverter.GetBytes((short)6)) } };
        var step = NativeBattleSiren95ScheduledNodes.Advance744F90(frame, added.Address, new(fixture.Inputs.Packets, (_, _, _) => throw new InvalidOperationException()));
        Assert.True(step.Complete);
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal((byte)3, Read(step.Frame, added.Address).ReadByte(0x29));
    }

    [Fact]
    public void Due_schedule_spawns_mode3_child_with_exact_two_random_draws_and_restores_identically()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var added = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x744F90, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        NativeBattleSiren95Frame frame = Put(added.Frame, added.Address, Read(added.Frame, added.Address).WriteByte(0x29, 2).WriteWord(0x24, 23));
        byte[] schedule = new byte[20];
        BinaryPrimitives.WriteInt16LittleEndian(schedule, 23);
        BinaryPrimitives.WriteInt16LittleEndian(schedule.AsSpan(2), 1024);
        BinaryPrimitives.WriteInt16LittleEndian(schedule.AsSpan(4), -50);
        BinaryPrimitives.WriteInt16LittleEndian(schedule.AsSpan(6), 1000);
        BinaryPrimitives.WriteInt16LittleEndian(schedule.AsSpan(10), 32767);
        NativeBattleSiren95NodeScheduleServices services = new(fixture.Inputs.Packets, (_, pointer, length) => schedule.AsSpan(checked((int)(pointer - 0x15339D8)), length).ToArray());
        NativeCrtRandomState random = frame.Random.Draw(out ushort jitter).Draw(out ushort height);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSiren95Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var step = NativeBattleSiren95ScheduledNodes.Advance744F90(frame, added.Address, services);
        NativeBattleModulePoolTask child = Assert.Single(step.Frame.Effect.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x745180);
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal(0x16A40ECu, child.ReadDword(0x30));
        Assert.Equal((ushort)3, child.ReadWord(0x29C));
        Assert.Equal((ushort)10, child.ReadWord(0x29E));
        int angle = (1024 + (jitter & 63) - 32) & 4095;
        Assert.Equal(unchecked((ushort)(-(NativeBattleCameraMath.Sine(angle) * 1000 / 4096))), child.ReadWord(0x288));
        Assert.Equal(unchecked((ushort)(-434 - (height & 63))), child.ReadWord(0x28A));
        Assert.Equal((ushort)1, Read(step.Frame, added.Address).ReadWord(0xDC));
        Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleSiren95ScheduledNodes.Advance744F90(restored, added.Address, services)));
    }

    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
