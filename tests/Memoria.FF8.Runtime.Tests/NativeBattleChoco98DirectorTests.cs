using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco98DirectorTests
{
    [Fact]
    public void Context_ingress_reuses_enemy_only_centroid_and_one_complete54_byte_owner()
    {
        NativeBattleGraphicsModel Model(int index) => new(index, 3)
        {
            Flags0 = index is 0 or 3 or 5 ? (ushort)2 : (ushort)0,
            Transform = new(new(0, 0, 0), index switch { 0 => new(30000, 999, 30000), 3 => new(-100, 999, -301), 5 => new(25, 999, 100), _ => new(0, 0, 0) }, 0, 0, 4096)
        };
        NativeBattleChoco97Context actual = NativeBattleChoco98Context.Initialize722150(Model);
        Assert.Equal(0x54, actual.Data.Length);
        Assert.Equal((ushort)2048, actual.Word(4));
        Assert.Equal(unchecked((ushort)(short)-37), actual.Word(8));
        Assert.Equal(unchecked((ushort)(short)-100), actual.Word(12));
        Assert.Equal((ushort)0, actual.Word(10));
        Assert.Equal((ushort)0, actual.Word(14));
        Assert.All(actual.Data.Skip(16), value => Assert.Equal((byte)0, value));
        Assert.Equal(NativeBattleChoco97Context.Initialize72A350(Model).Data, actual.Data);
        NativeBattleChoco97Context noEnemies = NativeBattleChoco98Context.Initialize722150(index => Model(index) with { Flags0 = index == 0 ? (ushort)2 : (ushort)0 });
        Assert.Equal(Enumerable.Repeat((byte)0, 8), noEnemies.Data.Skip(8).Take(8));
    }

    [Fact]
    public void Initial_phase_uses98_static_sources_and_camera_without_changing_shared_order()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorStep step = NativeBattleChoco98Director.Advance722210(Frame(0), Inputs(operations));
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97StartVibration(), new NativeBattleChoco97AppendNeutral(0x167D934, 18, 45, 2), new NativeBattleChoco97AppendProgram(0x722460, 0x1680B9C, 0x4AC, 0, 0), new NativeBattleChoco97AppendAux(0x7276F0, 0x70), new NativeBattleChoco97LoadModelTim() }, operations);
        Assert.Equal((byte)1, step.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)1, step.Frame.Task.ReadWord(0x24));
    }

    [Fact]
    public void OnMode_four_appends_only_file461_and_does_not_invent_a_loaded_neutral_task()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = WithMode(Frame(18), 2, 4, 4);
        NativeBattleChoco97DirectorStep step = NativeBattleChoco98Director.Advance722210(frame, Inputs(operations));
        Assert.Equal(new NativeBattleChoco97AppendProgram(0x7295D0, 0x19E187C, 0x170, 2, 0), Assert.Single(operations));
        Assert.Equal((ushort)4, step.Frame.Context.Word(0x40));
        Assert.Equal((ushort)0, step.Frame.Context.Word(0x46));
    }

    [Fact]
    public void OnMode_two_and_five_keep_their_actual98_auxiliary_callbacks()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco98Director.Advance722210(WithMode(Frame(18), 0, 2, 2), Inputs(operations));
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendProgram(0x7294C0, 0x19DB018, 0x44C, 1, 0), new NativeBattleChoco97AppendAux(0x7296D0, 0x70) }, operations);
        operations.Clear();
        NativeBattleChoco98Director.Advance722210(WithMode(Frame(18), 4, 5, 5), Inputs(operations));
        Assert.Equal(new NativeBattleChoco97AppendAux(0x7297C0, 0x70), Assert.Single(operations));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void Phase_four_releases_wait_before_requesting_mode_one_even_when_request_rejects(ushort mode, bool advances)
    {
        NativeBattleChoco97DirectorFrame frame = WithMode(Frame(4), mode, mode, mode);
        NativeBattleChoco97DirectorInputs inputs = Inputs([]) with { ReleaseWait = current => new(current with { Context = current.Context.WriteWord(0x4C, 0x1234) }, true) };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco98Director.Advance722210(frame, inputs);
        Assert.Equal((ushort)0x1234, step.Frame.Context.Word(0x4C));
        Assert.Equal(advances ? (byte)5 : (byte)4, step.Frame.Task.ReadByte(0x29));
        Assert.Equal(advances ? (ushort)1 : mode, step.Frame.Context.Word(0x44));
    }

    [Fact]
    public void Phase_five_only_waits_for_IO_without_requesting_another_mode()
    {
        NativeBattleChoco97DirectorFrame frame = WithMode(Frame(5), 2, 2, 2);
        NativeBattleChoco97DirectorStep step = NativeBattleChoco98Director.Advance722210(frame, Inputs([]));
        Assert.Equal((byte)6, step.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)2, step.Frame.Context.Word(0x44));
    }

    [Fact]
    public void Extra_phase_twelve_waits24_callbacks_between_distinct_target_programs_with_JSON_next()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Frame(11) with { Task = Frame(11).Task.WriteWord(0x44, 1).WriteWord(0x24, 100) };
        NativeBattleChoco97DirectorStep first = NativeBattleChoco98Director.Advance722210(frame, Inputs(operations));
        Assert.Equal(new NativeBattleChoco97AppendProgram(0x728C60, 0x19E5584, 0x494, 3, 0), Assert.Single(operations));
        Assert.Equal((ushort)24, first.Frame.Task.ReadWord(0x44));
        Assert.Equal((byte)12, first.Frame.Task.ReadByte(0x29));
        frame = first.Frame;
        operations.Clear();
        for (int callback = 0; callback < 24; callback++)
        {
            NativeBattleChoco97DirectorFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco97DirectorFrame>(NativeBattleTestSnapshots.Json(frame))!;
            NativeBattleChoco97DirectorStep actual = NativeBattleChoco98Director.Advance722210(frame, Inputs(operations));
            NativeBattleChoco97DirectorStep cold = NativeBattleChoco98Director.Advance722210(restored, Inputs([]));
            Assert.Equal(NativeBattleTestSnapshots.Json(actual), NativeBattleTestSnapshots.Json(cold));
            if (callback < 23)
            {
                Assert.Empty(operations);
                Assert.Equal((byte)12, actual.Frame.Task.ReadByte(0x29));
            }
            frame = actual.Frame;
        }
        Assert.Equal(new NativeBattleChoco97AppendProgram(0x728DA0, 0x19E8E70, 0x7D0, 4, 0), Assert.Single(operations));
        Assert.Equal((byte)13, frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)125, frame.Task.ReadWord(0x24));
    }

    [Fact]
    public void Contact_and_final_auxiliary_use_44_then24_and8_countdowns()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorStep requested = NativeBattleChoco98Director.Advance722210(WithMode(Frame(13), 4, 4, 4), Inputs(operations));
        Assert.Equal((ushort)44, requested.Frame.Task.ReadWord(0x44));
        Assert.Equal((byte)14, requested.Frame.Task.ReadByte(0x29));
        NativeBattleChoco97DirectorFrame frame = WithMode(requested.Frame, 5, 5, 5);
        for (int index = 0; index < 19; index++)
        {
            frame = NativeBattleChoco98Director.Advance722210(frame, Inputs(operations)).Frame;
            Assert.Equal((ushort)0, frame.Context.Word(0x48));
        }
        frame = NativeBattleChoco98Director.Advance722210(frame, Inputs(operations)).Frame;
        Assert.Equal((ushort)24, frame.Task.ReadWord(0x44));
        Assert.Equal((ushort)1, frame.Context.Word(0x48));
        Assert.Equal((byte)15, frame.Task.ReadByte(0x29));
        for (int index = 0; index < 15; index++)
        {
            frame = NativeBattleChoco98Director.Advance722210(frame, Inputs(operations)).Frame;
            Assert.Empty(operations);
        }
        frame = NativeBattleChoco98Director.Advance722210(frame, Inputs(operations)).Frame;
        Assert.Equal((ushort)8, frame.Task.ReadWord(0x44));
        Assert.Equal(new NativeBattleChoco97AppendAux(0x7292D0, 0x70), Assert.Single(operations));
        Assert.Equal((byte)16, frame.Task.ReadByte(0x29));
    }

    [Fact]
    public void Nineteenth_phase_stops_signed_handle_and_retains_generic_child_completion()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = WithMode(Frame(17), 5, 5, 5);
        frame = frame with { Context = frame.Context.WriteWord(0x52, 0xFFFF), Task = frame.Task.WriteByte(0x28, 1) };
        NativeBattleChoco97DirectorStep held = NativeBattleChoco98Director.Advance722210(frame, Inputs(operations));
        Assert.Equal(new NativeBattleChoco97StopVibration(-1), Assert.Single(operations));
        Assert.Equal((byte)18, held.Frame.Task.ReadByte(0x29));
        Assert.False(held.ReadyToComplete);
        Assert.True(NativeBattleChoco98Director.Advance722210(held.Frame with { Task = held.Frame.Task.WriteByte(0x28, 0) }, Inputs([])).ReadyToComplete);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco97Director.Advance72A410(frame, Inputs([])));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98Director.Advance722210(frame with { Task = frame.Task.WriteByte(0x29, 19) }, Inputs([])));
    }

    private static NativeBattleChoco97DirectorFrame Frame(byte phase) => new(new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco98Director.Callback, new byte[0x48 - 12].ToImmutableArray()).WriteByte(0x29, phase), NativeBattleChoco98Context.Clear722150());
    private static NativeBattleChoco97DirectorFrame WithMode(NativeBattleChoco97DirectorFrame frame, ushort current, ushort camera, ushort requested) => frame with { Context = frame.Context.WriteWord(0x40, current).WriteWord(0x42, camera).WriteWord(0x44, requested) };
    private static NativeBattleChoco97DirectorInputs Inputs(List<NativeBattleChoco97DirectorOperation> operations) => new(() => 0, offset => offset switch { 0x128 => 0x19DB018, 0x12C => 0x19E187C, 0x130 => 0x19E5584, 0x134 => 0x19E8E70, _ => throw new InvalidOperationException() }, current => new(current, false), (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });
}
