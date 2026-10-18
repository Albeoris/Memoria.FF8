using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaScrollingTaskTests
{
    [Fact]
    public void Spawn_preserves_unwritten_payload_and_paused_backdrop_draws_without_growth()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture();
        NativeBattleEffectTaskPool warm = frame.Pool with { Slots = frame.Pool.Slots.SetItem(0, frame.Pool.Slots[0] with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }) };
        frame = NativeBattleShivaScrollingTask.Initialize5C6FA0(frame with { Pool = warm });
        NativeBattleModulePoolTask task = frame.Pool.Slots[0];
        Assert.Equal((ushort)0xA5A5, task.ReadWord(0x0E));
        Assert.Equal((ushort)0xA5A5, task.ReadWord(0x16));
        List<NativeBattleShivaScrollingDraw> draws = [];
        NativeBattleShivaEarlyTaskStep paused = NativeBattleShivaScrollingTask.Advance5C6FF0(frame, 0, 0x200, Identity(), new(0, 0xD2A50C, []), (current, draw) =>
        {
            draws.Add(draw);
            return current;
        });
        Assert.Equal(task, paused.Frame.Pool.Slots[0]);
        Assert.Equal(0, paused.Result);
        NativeBattleShivaScrollingDraw command = Assert.Single(draws);
        Assert.Null(command.Context.Fade);
        Assert.Equal(0x33u, command.Context.Flags);
        Assert.Equal(-2500, command.Matrix.Z);
    }

    [Fact]
    public void Negative_velocity_uses_signed_division_and_release_follows_the_last_draw()
    {
        NativeBattleShivaEarlyTaskFrame frame = NativeBattleShivaScrollingTask.Initialize5C6FA0(Fixture());
        NativeBattleModulePoolTask task = frame.Pool.Slots[0].WriteWord(0x1E, unchecked((ushort)-5)).WriteWord(0x22, unchecked((ushort)-5));
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(0, task) } };
        NativeBattleShivaEarlyTaskStep growing = NativeBattleShivaScrollingTask.Advance5C6FF0(frame, 0, 0, Identity(), new(0, 0xD2A50C, []), (current, _) => current);
        Assert.Equal(unchecked((ushort)-4), growing.Frame.Pool.Slots[0].ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)-4), growing.Frame.Pool.Slots[0].ReadWord(0x22));
        Assert.Equal((ushort)2725, growing.Frame.Pool.Slots[0].ReadWord(0x1C));
        task = growing.Frame.Pool.Slots[0].WriteWord(0x0C, 29);
        frame = growing.Frame with { Pool = growing.Frame.Pool with { Slots = growing.Frame.Pool.Slots.SetItem(0, task) } };
        int draws = 0;
        NativeBattleShivaEarlyTaskStep completed = NativeBattleShivaScrollingTask.Advance5C6FF0(frame, 0, 0, Identity(), new(0, 0xD2A50C, []), (current, draw) =>
        {
            draws++;
            Assert.Equal(29 << 4, draw.Context.VOffset);
            Assert.Equal(7 << 9, draw.Context.Fade);
            Assert.Equal((ushort)29, current.Pool.Slots[0].ReadWord(0x0C));
            return current;
        });
        Assert.Equal(1, draws);
        Assert.Equal(2, completed.Result);
        Assert.Equal((ushort)30, completed.Frame.Pool.Slots[0].ReadWord(0x0C));
    }

    private static NativeBattleShivaEarlyTaskFrame Fixture() => new(NativeBattleEffectMemory.FromColdProcess(), NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100), new(1), NativeBattleShivaProcess.FromColdProcess().Geometry);
    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
}
