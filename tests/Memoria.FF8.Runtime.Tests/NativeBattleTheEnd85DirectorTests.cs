using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85DirectorTests
{
    [Fact]
    public void Stage_request_observes_two_ordered_publications_before_spawning()
    {
        List<string> calls = [];
        NativeBattleTheEnd85DirectorServices services = Services(calls);
        NativeBattleTheEnd85DirectorStep first = NativeBattleTheEnd85Director.Advance784E30(Input(0), services);
        Assert.Equal(new[] { "mode:False" }, calls);
        Assert.Equal((ushort)1, Word(first.Input, 68));
        Assert.Equal((ushort)0, Word(first.Input, 66));
        NativeBattleTheEnd85DirectorStep second = NativeBattleTheEnd85Director.Advance784E30(first.Input, services);
        Assert.Equal((ushort)1, Word(second.Input, 66));
        Assert.Equal((ushort)0, Word(second.Input, 64));
        Assert.Single(calls);
        NativeBattleTheEnd85DirectorStep third = NativeBattleTheEnd85Director.Advance784E30(second.Input, services);
        Assert.Equal(new[] { "mode:False", "spawn:1", "sound:156C7F4" }, calls);
        Assert.Equal((ushort)0, Word(third.Input, 70));
        Assert.Equal((byte)2, third.Input.Task.ReadByte(41));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 6, false)]
    public void Script_release_requires_both_actual_control_and_current_opcode(ushort release, ushort opcode, bool expected)
    {
        NativeBattleTheEnd85DirectorStep step = NativeBattleTheEnd85Director.Advance784E30(Input(2) with { IoRelease358 = release, ScriptOpcode = opcode }, Services([]));
        Assert.Equal(expected, step.AdvanceScript16);
        Assert.Equal(expected ? (byte)3 : (byte)2, step.Input.Task.ReadByte(41));
        Assert.Equal(expected ? (ushort)1 : release, step.Input.IoRelease358);
    }

    [Fact]
    public void Io_freeze_threshold_uses_stage_clock_and_preserves_unexecuted_conditional_stores()
    {
        NativeBattleTheEnd85DirectorInput input = Context(Input(3) with { IoRelease358 = 1, IoFreeze25C21D4 = 2, Pause25BC894 = 7 }, 70, 73);
        NativeBattleTheEnd85DirectorStep unchanged = NativeBattleTheEnd85Director.Advance784E30(input, Services([]));
        Assert.Equal(2u, unchanged.Input.IoFreeze25C21D4);
        Assert.Equal(7u, unchanged.Input.Pause25BC894);
        NativeBattleTheEnd85DirectorStep frozen = NativeBattleTheEnd85Director.Advance784E30(input with { IoFreeze25C21D4 = 0 }, Services([]));
        Assert.Equal(1u, frozen.Input.IoFreeze25C21D4);
        Assert.Equal(1u, frozen.Input.Pause25BC894);
        NativeBattleTheEnd85DirectorStep released = NativeBattleTheEnd85Director.Advance784E30(frozen.Input with { IoRelease358 = 0 }, Services([]));
        Assert.Equal(0u, released.Input.IoFreeze25C21D4);
        Assert.Equal(0u, released.Input.Pause25BC894);
        Assert.Equal((byte)4, released.Input.Task.ReadByte(41));
    }

    [Fact]
    public void Stage_comparison_zero_extends_the_native_WORD()
    {
        NativeBattleTheEnd85DirectorStep step = NativeBattleTheEnd85Director.Advance784E30(Context(Context(Context(Input(1), 64, 0x8000), 66, 0x8000), 68, 0x8000), Services([]));
        Assert.Equal((byte)2, step.Input.Task.ReadByte(41));
    }

    [Fact]
    public void Overlay_counter_and_final_completion_use_actual_parent_child_byte()
    {
        List<string> calls = [];
        NativeBattleTheEnd85DirectorStep overlay = NativeBattleTheEnd85Director.Advance784E30(Context(Input(7), 70, 13), Services(calls));
        Assert.Equal(new[] { "overlay:1571FE8" }, calls);
        Assert.Equal((ushort)6, overlay.Input.Task.ReadWord(68));
        NativeBattleTheEnd85DirectorInput input = overlay.Input;
        for (int tick = 0; tick < 6; tick++)
        {
            input = NativeBattleTheEnd85Director.Advance784E30(input, Services(calls)).Input;
        }
        Assert.Equal((byte)9, input.Task.ReadByte(41));
        NativeBattleTheEnd85DirectorInput final = Input(21);
        Assert.True(NativeBattleTheEnd85Director.Advance784E30(final, Services([])).Complete);
        Assert.False(NativeBattleTheEnd85Director.Advance784E30(final with { Task = final.Task.WriteByte(40, 1) }, Services([])).Complete);
    }

    private static NativeBattleTheEnd85DirectorInput Input(byte phase) => new(new NativeBattleModulePoolTask(1, 0, null, 0x784E30, new byte[60].ToImmutableArray()).WriteByte(41, phase), new byte[84].ToImmutableArray(), 0, 0, 0, 0);
    private static NativeBattleTheEnd85DirectorInput Context(NativeBattleTheEnd85DirectorInput input, int offset, ushort value)
    {
        byte[] bytes = input.Context.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        return input with { Context = bytes.ToImmutableArray() };
    }
    private static ushort Word(NativeBattleTheEnd85DirectorInput input, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(input.Context.AsSpan().Slice(offset));
    private static NativeBattleTheEnd85DirectorServices Services(List<string> calls) => new(input => Record(calls, input, $"spawn:{Word(input, 64)}"), (input, sound) => Record(calls, input, $"sound:{sound:X}"), (input, visible) => Record(calls, input, $"mode:{visible}"), input => Record(calls, input, "overlay:1571FE8"));
    private static NativeBattleTheEnd85DirectorInput Record(List<string> calls, NativeBattleTheEnd85DirectorInput input, string text)
    {
        calls.Add(text);
        return input;
    }
}
