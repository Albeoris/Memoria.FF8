using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco99EnvironmentTests
{
    [Fact]
    public void Hide_and_restore_use_only_four_physical_rows_and_preserve_native_bit_sources_before_sound()
    {
        NativeBattleChoco99EnvironmentFrame source = Frame(0x71F310, 1, 5);
        byte[] context = source.Context.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(0x30), 0xDEADBEEF);
        source = source with { Context = source.Context with { Data = context.ToImmutableArray() }, Task = source.Task.WriteWord(0xAC, 0x1234).WriteWord(0x24, ushort.MaxValue) };
        List<uint> sounds = [];
        NativeBattleChoco99EnvironmentStep hidden = NativeBattleChoco99Environment.Advance(source, (current, sound) =>
        {
            Assert.Equal(new byte[] { 5, 4, 1, 0 }, current.Stage.Groups.Select(group => group.Flags));
            Assert.Equal(0x106, current.Models[3].Flags0);
            Assert.Equal((ushort)0x1234, current.Task.ReadWord(0xAC));
            Assert.Equal(new NativeBattleChoco99EnvironmentSound(0x1529FAC, 0, 128), sound);
            sounds.Add(sound.Descriptor);
            return current;
        });
        Assert.False(hidden.ReadyToComplete);
        Assert.Equal((ushort)36, hidden.Frame.Task.ReadWord(0xAC));
        Assert.Equal((ushort)0, hidden.Frame.Task.ReadWord(0x24));
        Assert.Equal((byte)2, hidden.Frame.Task.ReadByte(0x29));
        Assert.Equal(0x102u, U32(hidden.Frame.Context, 0x2C));
        Assert.Equal(0xDEADBEEFu, U32(hidden.Frame.Context, 0x30));
        Assert.Equal(0x106u, U32(hidden.Frame.Context, 0x34));
        Assert.Equal(0x802u, U32(hidden.Frame.Context, 0x38));
        Assert.Equal(source.Models.Take(3), hidden.Frame.Models.Take(3));
        ImmutableArray<NativeBattleGraphicsModel>.Builder changed = hidden.Frame.Models.ToBuilder();
        changed[3] = changed[3] with { Flags0 = (ushort)(changed[3].Flags0 | 0x4000) };
        changed[5] = changed[5] with { Flags0 = (ushort)(changed[5].Flags0 & ~6) };
        NativeBattleChoco99EnvironmentFrame restore = hidden.Frame with { Task = Task(0x721460, 1), Models = changed.ToImmutable(), Context = hidden.Frame.Context.WriteWord(0x40, 10) };
        NativeBattleChoco99EnvironmentStep restored = NativeBattleChoco99Environment.Advance(restore, (current, sound) =>
        {
            Assert.Equal(new byte[] { 7, 6, 1, 0 }, current.Stage.Groups.Select(group => group.Flags));
            Assert.Equal(0x4102, current.Models[3].Flags0);
            Assert.Equal(changed[5], current.Models[5]);
            Assert.Equal(new NativeBattleChoco99EnvironmentSound(0x1529FB0, 0, 128), sound);
            sounds.Add(sound.Descriptor);
            return current;
        });
        Assert.True(restored.ReadyToComplete);
        Assert.Equal(source.Models.Take(3), restored.Frame.Models.Take(3));
        Assert.Equal(new uint[] { 0x1529FAC, 0x1529FB0 }, sounds);
        Assert.Equal(source.Stage.Groups.Select(group => (group.ColorDepth, group.ColorFlags)), restored.Frame.Stage.Groups.Select(group => (group.ColorDepth, group.ColorFlags)));
    }

    [Theory]
    [InlineData(0x721010u, 13, 1536)]
    [InlineData(0x721100u, 25, 0)]
    public void Fades_preserve_initial_write_timing_and_all_independent_cold_next_states(uint callback, int callbacks, int finalDepth)
    {
        NativeBattleChoco99EnvironmentFrame frame = Frame(callback, 0, 0);
        for (int index = 0; index < callbacks; index++)
        {
            NativeBattleChoco99EnvironmentFrame cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco99EnvironmentFrame>(NativeBattleTestSnapshots.Json(frame))!;
            NativeBattleChoco99EnvironmentStep step = Advance(frame);
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(Advance(cold)));
            Assert.Equal(index == callbacks - 1, step.ReadyToComplete);
            if (index == 0)
            {
                Assert.All(step.Frame.Stage.Groups, group => Assert.Equal(callback == 0x721010 ? 0 : 99, group.ColorDepth));
                Assert.All(step.Frame.Stage.Groups, group => Assert.Equal(callback == 0x721010 ? 0xAB000000u : 0xAB123456u, group.ColorFlags));
            }
            frame = step.Frame;
        }
        Assert.All(frame.Stage.Groups, group => Assert.Equal(finalDepth, group.ColorDepth));
        NativeBattleChoco99EnvironmentStep held = Advance(frame with { Task = frame.Task.WriteByte(0x28, 1) });
        Assert.False(held.ReadyToComplete);
        Assert.Equal(frame.Stage, held.Frame.Stage);
    }

    [Theory]
    [InlineData(0x71F310u, 4, 5)]
    [InlineData(0x721460u, 9, 10)]
    [InlineData(0x7215A0u, 7, 8)]
    public void Request_writes_only_pending_mode_and_waits_on_current_mode40(uint callback, int current, int requested)
    {
        NativeBattleChoco99EnvironmentFrame frame = Frame(callback, 0, (ushort)current);
        NativeBattleChoco99EnvironmentStep next = Advance(frame);
        Assert.Equal((ushort)requested, next.Frame.Context.Word(0x44));
        Assert.Equal((ushort)current, next.Frame.Context.Word(0x40));
        Assert.Equal((byte)1, next.Frame.Task.ReadByte(0x29));
        NativeBattleChoco99EnvironmentStep waiting = Advance(next.Frame with { Context = next.Frame.Context.WriteWord(0x42, (ushort)requested) });
        Assert.Equal((byte)1, waiting.Frame.Task.ReadByte(0x29));
        Assert.Equal(next.Frame.Stage, waiting.Frame.Stage);
        Assert.Equal(next.Frame.Models, waiting.Frame.Models);
    }

    [Fact]
    public void Environment_freeze_observes_rejected_request_then_unfreezes_on_accepted_mode7()
    {
        NativeBattleChoco99EnvironmentFrame frame = Frame(0x71F310, 3, 5);
        NativeBattleChoco99EnvironmentStep held = Advance(frame);
        Assert.Equal((1u, 1u), (held.Frame.FreezePrimary, held.Frame.FreezeSecondary));
        Assert.False(held.ReadyToComplete);
        NativeBattleChoco99EnvironmentStep next = Advance(held.Frame with { Context = held.Frame.Context.WriteWord(0x40, 6).WriteWord(0x44, 6) });
        Assert.Equal((0u, 0u), (next.Frame.FreezePrimary, next.Frame.FreezeSecondary));
        Assert.Equal((ushort)7, next.Frame.Context.Word(0x44));
        Assert.True(next.ReadyToComplete);
    }

    [Theory]
    [InlineData(0x71F310u)]
    [InlineData(0x7215A0u)]
    public void Countdown_wraps_word_before_signed_comparison_and_counter_is_cumulative(uint callback)
    {
        NativeBattleChoco99EnvironmentFrame frame = Frame(callback, 2, 0);
        frame = frame with { Task = frame.Task.WriteWord(0xAC, 0).WriteWord(0x24, 0x7FFF) };
        NativeBattleChoco99EnvironmentStep step = Advance(frame);
        Assert.Equal(ushort.MaxValue, step.Frame.Task.ReadWord(0xAC));
        Assert.Equal((ushort)0x8000, step.Frame.Task.ReadWord(0x24));
        Assert.Equal((byte)3, step.Frame.Task.ReadByte(0x29));
        Assert.False(step.ReadyToComplete);
    }

    private static NativeBattleChoco99EnvironmentStep Advance(NativeBattleChoco99EnvironmentFrame frame) => NativeBattleChoco99Environment.Advance(frame, (_, _) => throw new InvalidOperationException("This phase must not emit a sound."));
    private static uint U32(NativeBattleChoco97Context context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Data.AsSpan().Slice(offset));
    private static NativeBattleModulePoolTask Task(uint callback, byte phase) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[0xB0 - 12].ToImmutableArray()).WriteByte(0x29, phase);
    private static NativeBattleChoco99EnvironmentFrame Frame(uint callback, byte phase, ushort mode)
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        byte[] flags = [7, 6, 3, 2];
        stage = stage with { Groups = stage.Groups.Select((group, index) => group with { Flags = flags[index], ColorDepth = 99, ColorFlags = 0xAB123456 }).ToImmutableArray() };
        ushort[] actors = [0xF02, 0xA02, 0x402, 0x102, 0xFF04, 0x106, 0x802];
        return new(Task(callback, phase), NativeBattleChoco99Context.Clear718670().WriteWord(0x40, mode).WriteWord(0x44, mode), stage, actors.Select((actor, index) => new NativeBattleGraphicsModel(index, actor)).ToImmutableArray(), 0, 0);
    }
}
