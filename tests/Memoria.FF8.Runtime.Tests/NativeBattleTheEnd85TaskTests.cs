using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85TaskTests
{
    [Fact]
    public void Initial_emitter_uses_age_before_increment_and_emits_special_indices_even_after_stage_three()
    {
        List<ushort> kinds = [];
        NativeBattleTheEnd85TaskServices services = Services(spawn: (input, descriptor, callback, size, initialize) =>
        {
            Assert.Equal(0x25BC898u, descriptor);
            Assert.Equal(0x788580u, callback);
            Assert.Equal(228, size);
            kinds.Add(initialize(Task(callback, size)).ReadWord(222));
            return input with { Task = input.Task.WriteByte(40, unchecked((byte)(input.Task.ReadByte(40) + 1))) };
        });
        NativeBattleTheEnd85TaskInput input = Input(0x788420, 228, 3) with { Task = Task(0x788420, 228).WriteWord(36, 15) };
        NativeBattleTheEnd85TaskStep step = NativeBattleTheEnd85Particles.Advance(input, services);
        Assert.Equal((byte)0, step.Input.Task.ReadByte(41));
        step = NativeBattleTheEnd85Particles.Advance(step.Input, services);
        Assert.Equal((byte)1, step.Input.Task.ReadByte(41));
        foreach (ushort selector in new ushort[] { 0, 8, 14 })
        {
            input = step.Input with { Task = step.Input.Task.WriteWord(220, selector) };
            NativeBattleTheEnd85TaskStep special = NativeBattleTheEnd85Particles.Advance(input, services);
            Assert.False(special.Complete);
            Assert.Equal((byte)1, special.Input.Task.ReadByte(41));
            step = special;
        }
        Assert.Equal(new ushort[] { 0, 1, 2 }, kinds);
        step = NativeBattleTheEnd85Particles.Advance(step.Input with { Task = step.Input.Task.WriteWord(220, 15) }, services);
        Assert.Equal((byte)2, step.Input.Task.ReadByte(41));
        Assert.Equal((byte)1, step.Input.Task.ReadByte(38));
        Assert.False(step.Complete);
        Assert.Equal((byte)3, step.Input.Task.ReadByte(40));
        step = NativeBattleTheEnd85Particles.Advance(step.Input with { Task = step.Input.Task.WriteByte(40, 0) }, services);
        Assert.True(step.Complete);
    }

    [Fact]
    public void Model_initialization_and_draw_order_matches_native_and_stage_six_skips_animation_draw()
    {
        List<string> calls = [];
        NativeBattleTheEnd85TaskServices services = Services(draw: (input, callback, parameter) =>
        {
            calls.Add("draw");
            Assert.Equal(0x785660u, callback);
            Assert.Equal(4096u, input.Task.ReadDword(80));
            Assert.Equal((ushort)1024, input.Task.ReadWord(136));
            return new(input, 1);
        }) with
        {
            InitializeModel = input => { calls.Add("initialize"); return input; },
            AppendActors = input => { calls.Add("actors"); return input; }
        };
        NativeBattleTheEnd85TaskStep step = NativeBattleTheEnd85Actors.Advance(Input(0x785560, 732, 6), services);
        Assert.Equal(new[] { "initialize", "draw", "actors" }, calls);
        calls.Clear();
        step = NativeBattleTheEnd85Actors.Advance(step.Input, services);
        Assert.True(step.Complete);
        Assert.Empty(calls);
        Assert.Equal((ushort)2, step.Input.Task.ReadWord(36));
    }

    [Theory]
    [InlineData(0, 7, 0)]
    [InlineData(0, 8, 1)]
    [InlineData(1, 13, 0)]
    [InlineData(1, 14, 1)]
    [InlineData(0, 65535, 0)]
    public void Orbit_actor_delay_compares_signed_age(int variant, int age, int phase)
    {
        NativeBattleTheEnd85TaskInput input = Input(0x787B50, 732, 0);
        input = input with { Task = input.Task.WriteWord(138, (ushort)variant).WriteWord(36, (ushort)age) };
        NativeBattleTheEnd85TaskStep step = NativeBattleTheEnd85Actors.Advance(input, Services());
        Assert.Equal((byte)phase, step.Input.Task.ReadByte(41));
    }

    [Fact]
    public void Environmental_fade_uses_unsigned_stage_and_draws_countdown_transition_tick()
    {
        List<short> heights = [];
        NativeBattleTheEnd85TaskServices services = Services(draw: (input, callback, parameter) =>
        {
            heights.Add(parameter);
            return new(input, 1);
        });
        NativeBattleTheEnd85TaskInput input = Input(0x78D410, 96, 65535);
        input = input with { Task = input.Task.WriteByte(41, 1).WriteWord(30, 24) };
        NativeBattleTheEnd85TaskStep step = NativeBattleTheEnd85ProgramTasks.Advance(input, services);
        Assert.Equal((ushort)4, step.Input.Task.ReadWord(92));
        Assert.Equal((byte)2, step.Input.Task.ReadByte(41));
        for (int i = 0; i < 4; i++)
        {
            step = NativeBattleTheEnd85ProgramTasks.Advance(step.Input, services);
        }
        Assert.Equal((byte)3, step.Input.Task.ReadByte(41));
        Assert.Equal(5, heights.Count);
        for (int i = 0; i < 6; i++)
        {
            step = NativeBattleTheEnd85ProgramTasks.Advance(step.Input, services);
        }
        Assert.True(step.Complete);
        Assert.Equal(new short[] { 24, 24, 24, 24, 24, 20, 16, 12, 8, 4 }, heights);
    }

    [Fact]
    public void Secondary_spawner_uses_twenty_native_random_draws_and_preserves_vector_padding()
    {
        NativeBattleModulePoolTask? child = null;
        NativeBattleTheEnd85TaskInput input = Input(0x78CC00, 228, 0);
        NativeBattleTheEnd85TaskServices services = Services(spawn: (current, descriptor, callback, size, initialize) =>
        {
            Assert.Equal(0x78CFA0u, callback);
            child = initialize(Task(callback, size).WriteWord(90, 0xA55A));
            return current with { Task = current.Task.WriteByte(40, 1) };
        });
        NativeBattleTheEnd85TaskInput result = NativeBattleTheEnd85Particles.Spawn78CED0(input, services);
        Assert.Equal(new NativeCrtRandomState(2503793637), result.Random);
        Assert.NotNull(child);
        Assert.Equal((short)-16343, unchecked((short)child.ReadWord(84)));
        Assert.Equal((short)-1059, unchecked((short)child.ReadWord(86)));
        Assert.Equal((short)-1858, unchecked((short)child.ReadWord(88)));
        Assert.Equal((ushort)2948, child.ReadWord(212));
        Assert.Equal((ushort)2785, child.ReadWord(184));
        Assert.Equal((ushort)0xA55A, child.ReadWord(90));
        Assert.Equal((ushort)1, result.Task.ReadWord(222));
        Assert.Equal((byte)1, result.Task.ReadByte(40));
        NativeBattleTheEnd85TaskInput independent = JsonSerializer.Deserialize<NativeBattleTheEnd85TaskInput>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(independent));
    }

    [Fact]
    public void Primary_particle_fixed_seed_has_no_random_draw_and_copies_retained_W_to_header()
    {
        NativeBattleTheEnd85TaskInput input = Input(0x788580, 228, 0);
        input = input with { Task = input.Task.WriteWord(222, 0).WriteWord(90, 0xD00D).WriteWord(122, 0xBEEF) };
        int draws = 0;
        NativeBattleTheEnd85TaskServices services = Services(draw: (current, callback, parameter) =>
        {
            draws++;
            Assert.Equal(0x788700u, callback);
            return new(current, 1);
        });
        NativeBattleTheEnd85TaskStep step = NativeBattleTheEnd85Particles.Advance(input, services);
        Assert.Equal(input.Random, step.Input.Random);
        Assert.Equal((ushort)32, step.Input.Task.ReadWord(180));
        Assert.Equal((ushort)0xBEEF, step.Input.Task.ReadWord(122));
        Assert.Equal((ushort)0xD00D, step.Input.Task.ReadWord(34));
        Assert.Equal(1, draws);
        Assert.Equal((ushort)1, step.Input.Task.ReadWord(36));
        Assert.False(step.Complete);
    }

    [Fact]
    public void Secondary_terminal_stage_keeps_four_draws_and_motion_before_child_completion()
    {
        NativeBattleTheEnd85TaskInput input = Input(0x78CFA0, 228, 6);
        input = input with { Task = input.Task.WriteByte(41, 1).WriteWord(80, 3584).WriteWord(90, 0x1234).WriteWord(122, 0x5678) };
        int draws = 0;
        NativeBattleTheEnd85TaskServices services = Services(draw: (current, callback, parameter) =>
        {
            draws++;
            return new(current, 1);
        });
        NativeBattleTheEnd85TaskStep step = NativeBattleTheEnd85Particles.Advance(input, services);
        Assert.True(step.Complete);
        Assert.Equal((byte)5, step.Input.Task.ReadByte(38));
        Assert.Equal((byte)1, step.Input.Task.ReadByte(41));
        Assert.Equal((ushort)3584, step.Input.Task.ReadWord(80));
        Assert.Equal((ushort)64, step.Input.Task.ReadWord(184));
        Assert.Equal((ushort)0x1234, step.Input.Task.ReadWord(90));
        Assert.Equal((ushort)0x5678, step.Input.Task.ReadWord(122));
        Assert.Equal(4, draws);
    }

    [Fact]
    public void Grounded_object_ignores_first_completion_result_and_uses_nonzero_completion_after_initialization()
    {
        List<uint> calls = [];
        NativeBattleTheEnd85TaskServices services = Services(draw: (current, callback, parameter) =>
        {
            calls.Add(callback);
            return new(current, 1);
        });
        NativeBattleTheEnd85TaskInput input = Input(0x788B90, 676, 0);
        input = input with { Task = input.Task.WriteWord(664, 1) };
        NativeBattleTheEnd85TaskStep step = NativeBattleTheEnd85Actors.Advance(input, services);
        Assert.Empty(calls);
        step = NativeBattleTheEnd85Actors.Advance(step.Input, services);
        Assert.Equal(new uint[] { 0x788C20, 0x789700 }, calls);
        Assert.False(step.Complete);
        Assert.Equal((byte)1, step.Input.Task.ReadByte(41));
        step = NativeBattleTheEnd85Actors.Advance(step.Input, services);
        Assert.True(step.Complete);
        Assert.Equal((byte)2, step.Input.Task.ReadByte(41));
    }

    private static NativeBattleModulePoolTask Task(uint callback, int size) => new(1, 0, null, callback, new byte[size - 12].ToImmutableArray());
    private static NativeBattleTheEnd85TaskInput Input(uint callback, int size, ushort stage)
    {
        byte[] context = new byte[84];
        BinaryPrimitives.WriteUInt16LittleEndian(context.AsSpan(64), stage);
        return new(Task(callback, size), context.ToImmutableArray(), NativeBattleTheEnd85State.FromColdProcess(), new(1));
    }
    private static NativeBattleTheEnd85TaskServices Services(Func<NativeBattleTheEnd85TaskInput, uint, uint, int, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask>, NativeBattleTheEnd85TaskInput>? spawn = null, Func<NativeBattleTheEnd85TaskInput, uint, short, NativeBattleTheEnd85TaskDraw>? draw = null)
    {
        return new(spawn ?? ((_, _, _, _, _) => throw new InvalidOperationException()), draw ?? ((input, _, _) => new(input, 1)), input => input, input => input);
    }
}
