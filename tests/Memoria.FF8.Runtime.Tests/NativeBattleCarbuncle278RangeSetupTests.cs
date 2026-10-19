using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278RangeSetupTests
{
    [Theory]
    [InlineData(20, -1425, 0, 96)]
    [InlineData(40, 169, 0, 64)]
    [InlineData(41, 90, 273, 56)]
    [InlineData(54, 90, 3822, 80)]
    public void Window_uses_current_signed_bound_words_inclusive_twenty_step_branch_and_native_UV_byte(short age, short upper, int fade, ushort offset)
    {
        byte[] bounds = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(bounds, -1504);
        BinaryPrimitives.WriteInt16LittleEndian(bounds.AsSpan(2), 1594);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0, bounds) };
        NativeBattleCarbuncle278RangePhase phase = NativeBattleCarbuncle278RangeSetup.Read6825A5(memory, age);
        Assert.Equal((short)-1504, phase.Minimum);
        Assert.Equal(upper, phase.Maximum);
        Assert.Equal(fade, phase.Fade);
        Assert.Equal(offset, phase.VOffset);
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278RangeSetup.Read6825A5(memory with { Arena = memory.Arena.MarkUnknown(2, 1) }, age));
    }

    [Fact]
    public void Rings_spawn_only_at_three_unpaused_phases_and_keep_signed_scaled_height()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508148, 0x20DFAB8 + 0x1EA4, 0x1C, 9);
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60), new(0, 0, 3, 0, 0, 0), null) { Pools = [pool] };
        NativeBattleCarbuncle278Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeCrtRandomState.FromColdProcess());
        for (int index = 0; index < 3; index++)
        {
            frame = NativeBattleCarbuncle278RangeSetup.Spawn6826B7(frame, 0x20E1A64, new(-100, -51, 0, 0, index * 6), 700);
            NativeBattleModulePoolTask task = frame.Effect.Pools[0].Slots[index];
            Assert.Equal(unchecked((ushort)-35), task.ReadWord(0xE));
            Assert.Equal((ushort)0, task.ReadWord(0xC));
            Assert.Equal((ushort)2048, task.ReadWord(0x10));
            Assert.Equal((ushort)256, task.ReadWord(0x12));
            Assert.Equal(unchecked((ushort)-8), task.ReadWord(0x14));
            Assert.Equal((ushort)0, task.ReadWord(0x16));
            Assert.Equal(0x20E1A64u, task.ReadDword(0x18));
            Assert.Equal(task.Payload, frame.Memory.Arena!.Read(0x1EA4 + index * 28 + 12, 16));
        }
        Assert.Same(frame, NativeBattleCarbuncle278RangeSetup.Spawn6826B7(frame, 1, new(0, 0, 0, 0, 18), 0));
        NativeBattleCarbuncle278Frame paused = frame with { Memory = frame.Memory with { Carbuncle278 = frame.Memory.Carbuncle278! with { Pause8114 = 1 } } };
        Assert.Same(paused, NativeBattleCarbuncle278RangeSetup.Spawn6826B7(paused, 1, new(0, 0, 0, 0, 0), 0));
    }

    [Fact]
    public void Ring_acceleration_is_applied_before_scale_with_native_floor_and_release_after_draw()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6831D0, new byte[16].ToImmutableArray());
        task = task.WriteWord(0xC, 19).WriteWord(0x10, 1000).WriteWord(0x12, 20).WriteWord(0x14, unchecked((ushort)-8));
        NativeBattleCarbuncle278ChildStep result = NativeBattleCarbuncle278RangeSetup.Finish6831D0(task, false);
        Assert.Equal((ushort)16, result.Task.ReadWord(0x12));
        Assert.Equal((ushort)1016, result.Task.ReadWord(0x10));
        Assert.Equal((ushort)20, result.Task.ReadWord(0xC));
        Assert.Equal(0, result.Result);
        Assert.Equal(2, NativeBattleCarbuncle278RangeSetup.Finish6831D0(result.Task, false).Result);
        Assert.Same(task, NativeBattleCarbuncle278RangeSetup.Finish6831D0(task, true).Task);
    }
}
