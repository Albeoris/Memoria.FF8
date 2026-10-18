using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco97DirectorTests
{
    [Theory]
    [InlineData(0u, 99u, 7u, 1u, 1u)]
    [InlineData(1u, 99u, 0u, 0u, 0u)]
    [InlineData(1u, 99u, 1u, 1u, 99u)]
    [InlineData(0u, 99u, 0u, 0u, 99u)]
    [InlineData(2u, 99u, 0u, 2u, 99u)]
    public void Freeze_setter_changes_both_owners_only_on_the_exact_primary_transitions(uint primary, uint secondary, uint value, uint expectedPrimary, uint expectedSecondary)
    {
        Assert.Equal((expectedPrimary, expectedSecondary), NativeBattleChoco97Director.SetFreeze730610(primary, secondary, value));
    }

    [Fact]
    public void Initial_phase_emits_ordered_children_and_retains_vibration_handle_without_resetting_counter()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Frame(0) with { Task = Frame(0).Task.WriteWord(0x24, 19) };
        NativeBattleChoco97DirectorInputs inputs = Inputs(operations) with
        {
            Execute = (current, operation) =>
            {
                operations.Add(operation);
                return operation is NativeBattleChoco97StartVibration ? current with { Context = current.Context.WriteWord(0x52, 0xFEDC) } : current;
            }
        };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco97Director.Advance72A410(frame, inputs);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97StartVibration(), new NativeBattleChoco97AppendNeutral(0x168C654, 18, 45, 2), new NativeBattleChoco97AppendProgram(0x72A660, 0x168F8BC, 0x4AC, 0, 0), new NativeBattleChoco97AppendAux(0x72FBC0, 0x70), new NativeBattleChoco97LoadModelTim() }, operations);
        Assert.Equal((ushort)0xFEDC, step.Frame.Context.Word(0x52));
        Assert.Equal((ushort)20, step.Frame.Task.ReadWord(0x24));
        Assert.Equal((byte)1, step.Frame.Task.ReadByte(0x29));
        Assert.False(step.ReadyToComplete);
    }

    [Fact]
    public void Mode_request_propagates_to_camera_then_current_and_rereads_context_after_ordered_OnMode_operations()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Frame(17) with { Context = NativeBattleChoco97Context.Clear72A350().WriteWord(0x44, 2).WriteWord(0x46, 0xFFFF) };
        NativeBattleChoco97DirectorInputs inputs = Inputs(operations) with
        {
            Execute = (current, operation) =>
            {
                operations.Add(operation);
                return operation is NativeBattleChoco97AppendAux ? current with { Context = current.Context.WriteWord(0x44, 4) } : current;
            }
        };
        NativeBattleChoco97DirectorStep first = NativeBattleChoco97Director.Advance72A410(frame, inputs);
        Assert.Empty(operations);
        Assert.Equal((ushort)0, first.Frame.Context.Word(0x40));
        Assert.Equal((ushort)2, first.Frame.Context.Word(0x42));
        Assert.Equal((ushort)0, first.Frame.Context.Word(0x46));
        NativeBattleChoco97DirectorStep second = NativeBattleChoco97Director.Advance72A410(first.Frame, inputs);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendProgram(0x731840, 0x19DB018, 0x44C, 1, 0), new NativeBattleChoco97AppendAux(0x731A50, 0x70) }, operations);
        Assert.Equal((ushort)2, second.Frame.Context.Word(0x40));
        Assert.Equal((ushort)4, second.Frame.Context.Word(0x42));
        Assert.Equal((ushort)4, second.Frame.Context.Word(0x44));
        Assert.Equal((ushort)0, second.Frame.Context.Word(0x46));
        operations.Clear();
        NativeBattleChoco97DirectorStep third = NativeBattleChoco97Director.Advance72A410(second.Frame, Inputs(operations));
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendProgram(0x731950, 0x19E4958, 0x170, 2, 0), new NativeBattleChoco97AppendNeutral(0x19DFEF8, 0, 45, 2) }, operations);
        Assert.Equal((ushort)4, third.Frame.Context.Word(0x40));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void Wait_release_is_external_and_its_updated_frame_is_retained(int phase)
    {
        bool released = false;
        NativeBattleChoco97DirectorInputs inputs = Inputs([]) with { ReleaseWait = frame => new(frame with { Context = frame.Context.WriteWord(0x4C, 0x1234) }, released) };
        NativeBattleChoco97DirectorStep waiting = NativeBattleChoco97Director.Advance72A410(Frame((byte)phase), inputs);
        Assert.Equal((byte)phase, waiting.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)0x1234, waiting.Frame.Context.Word(0x4C));
        released = true;
        NativeBattleChoco97DirectorStep advanced = NativeBattleChoco97Director.Advance72A410(waiting.Frame, inputs);
        Assert.Equal((byte)(phase + 1), advanced.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)2, advanced.Frame.Task.ReadWord(0x24));
    }

    [Theory]
    [InlineData(26, false)]
    [InlineData(27, true)]
    [InlineData(32767, true)]
    [InlineData(32768, false)]
    [InlineData(65535, false)]
    public void IO_wait_freeze_threshold_uses_signed_cumulative_counter_and_counter_wraps(int counter, bool freezes)
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Frame(2) with { Task = Frame(2).Task.WriteWord(0x24, (ushort)counter) };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco97Director.Advance72A410(frame, Inputs(operations) with { ReadIoWord166 = () => 1 });
        Assert.Equal(freezes ? new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97SetFreeze(1) } : [], operations);
        Assert.Equal((byte)2, step.Frame.Task.ReadByte(0x29));
        Assert.Equal(unchecked((ushort)(counter + 1)), step.Frame.Task.ReadWord(0x24));
        operations.Clear();
        NativeBattleChoco97DirectorStep released = NativeBattleChoco97Director.Advance72A410(step.Frame, Inputs(operations));
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97SetFreeze(0) }, operations);
        Assert.Equal((byte)3, released.Frame.Task.ReadByte(0x29));
    }

    [Theory]
    [InlineData(27, false)]
    [InlineData(28, true)]
    [InlineData(32768, false)]
    public void Model_append_uses_28_on_the_same_cumulative_signed_counter(int counter, bool append)
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Frame(3) with { Task = Frame(3).Task.WriteWord(0x24, (ushort)counter) };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco97Director.Advance72A410(frame, Inputs(operations));
        Assert.Equal(append ? new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendModel() } : [], operations);
        Assert.Equal(append ? (byte)4 : (byte)3, step.Frame.Task.ReadByte(0x29));
    }

    [Fact]
    public void Countdown_boundaries_publish_contact_once_then_auxiliary_without_resetting_counter()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Frame(13) with { Task = Frame(13).Task.WriteWord(0x44, 20).WriteWord(0x24, 100) };
        int contactTransitions = 0;
        for (int callback = 0; callback < 20; callback++)
        {
            NativeBattleChoco97DirectorFrame restored = JsonSerializer.Deserialize<NativeBattleChoco97DirectorFrame>(JsonSerializer.Serialize(frame))!;
            NativeBattleChoco97DirectorStep step = NativeBattleChoco97Director.Advance72A410(frame, Inputs(operations));
            NativeBattleChoco97DirectorStep cold = NativeBattleChoco97Director.Advance72A410(restored, Inputs([]));
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(cold));
            if (frame.Context.Word(0x48) == 0 && step.Frame.Context.Word(0x48) == 1)
            {
                contactTransitions++;
                Assert.Equal((ushort)18, step.Frame.Task.ReadWord(0x44));
                Assert.Equal((byte)14, step.Frame.Task.ReadByte(0x29));
            }
            frame = step.Frame;
        }
        Assert.Equal(1, contactTransitions);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendAux(0x731630, 0x70) }, operations);
        Assert.Equal((byte)16, frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)120, frame.Task.ReadWord(0x24));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, true)]
    [InlineData(32768, false)]
    public void Loaded_final_program_countdown_decrements_WORD_then_compares_signed(int countdown, bool append)
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Frame(11) with { Task = Frame(11).Task.WriteWord(0x44, (ushort)countdown) };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco97Director.Advance72A410(frame, Inputs(operations));
        Assert.Equal(unchecked((ushort)(countdown - 1)), step.Frame.Task.ReadWord(0x44));
        Assert.Equal(append ? new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendProgram(0x731100, 0x19E8660, 0x290, 3, 0) } : [], operations);
    }

    [Fact]
    public void Terminal_mode_request_stops_signed_handle_but_children_hold_physical_completion()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Frame(16) with { Task = Frame(16).Task.WriteByte(0x26, 0x80).WriteByte(0x28, 1).WriteWord(0x24, 0xFFFF), Context = NativeBattleChoco97Context.Clear72A350().WriteWord(0x40, 5).WriteWord(0x42, 5).WriteWord(0x44, 5).WriteWord(0x52, 0xFFFF) };
        NativeBattleChoco97DirectorStep held = NativeBattleChoco97Director.Advance72A410(frame, Inputs(operations));
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97StopVibration(-1) }, operations);
        Assert.False(held.ReadyToComplete);
        Assert.Equal((byte)0x81, held.Frame.Task.ReadByte(0x26));
        Assert.Equal((byte)17, held.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)0, held.Frame.Task.ReadWord(0x24));
        Assert.Equal((ushort)6, held.Frame.Context.Word(0x44));
        operations.Clear();
        NativeBattleChoco97DirectorStep complete = NativeBattleChoco97Director.Advance72A410(held.Frame with { Task = held.Frame.Task.WriteByte(0x28, 0) }, Inputs(operations));
        Assert.True(complete.ReadyToComplete);
        Assert.Empty(operations);
    }

    private static NativeBattleChoco97DirectorFrame Frame(byte phase) => new(new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco97Director.Callback, new byte[0x48 - 12].ToImmutableArray()).WriteByte(0x29, phase), NativeBattleChoco97Context.Clear72A350());

    private static NativeBattleChoco97DirectorInputs Inputs(List<NativeBattleChoco97DirectorOperation> operations) => new(() => 0, offset => offset switch { 0x128 => 0x19DB018, 0x108 => 0x19DFEF8, 0x12C => 0x19E4958, 0x130 => 0x19E8660, _ => throw new InvalidOperationException() }, frame => new(frame, false), (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });
}
