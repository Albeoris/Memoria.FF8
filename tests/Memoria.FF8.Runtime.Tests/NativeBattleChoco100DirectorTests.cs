using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100DirectorTests
{
    [Fact]
    public void Context_ingress_reuses_proved_enemy_centroid_clamp_in_one54_byte_owner()
    {
        NativeBattleGraphicsModel Model(int index) => new(index, 3)
        {
            Flags0 = index is 0 or 3 or 5 ? (ushort)2 : (ushort)0,
            Transform = new(new(0, 0, 0), index switch { 0 => new(30000, 999, 30000), 3 => new(-100, 999, -301), 5 => new(25, 999, 100), _ => new(0, 0, 0) }, 0, 0, 4096)
        };
        NativeBattleChoco97Context context = NativeBattleChoco100Context.Initialize70F290(Model);
        Assert.Equal(NativeBattleChoco99Context.Initialize718670(Model).Data, context.Data);
        Assert.Equal((ushort)0, context.Word(8));
        Assert.Equal(unchecked((ushort)(short)-100), context.Word(12));
        Assert.Equal((ushort)2048, context.Word(4));
        Assert.Equal(0x25653E0u, NativeBattleChoco100Context.Address);
    }

    [Fact]
    public void Initial_phase_emits_exact_sources_and_70_byte_camera_in_service_order()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorStep step = Advance(Frame(0), operations);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97StartVibration(), new NativeBattleChoco97AppendNeutral(0x165FEEC, 18, 45, 2), new NativeBattleChoco97AppendProgram(0x70F5F0, 0x1663154, 0x4AC, 0, 0), new NativeBattleChoco97AppendAux(0x7148C0, 0x70), new NativeBattleChoco97LoadModelTim() }, operations);
        Assert.Equal((byte)1, step.Frame.Task.ReadByte(0x29));
    }

    [Fact]
    public void Mode6_appends_loaded_neutral_before_actual_model_initialization_argument3C_and_mode8_preserves_order()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        Advance(Mode(Frame(23), 5, 6, 6), operations);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendNeutral(0x10108, 0, 45, 2), new NativeBattleChoco100AppendLoadedModel(0x10118, 0x3C) }, operations);
        Assert.Equal((ushort)0x3C, Assert.IsType<NativeBattleChoco100AppendLoadedModel>(operations[1]).InitializationArgument);
        operations.Clear();
        Advance(Mode(Frame(23), 7, 8, 8), operations);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendProgram(0x716950, 0x10130, 0x32C, 3, 0), new NativeBattleChoco99AppendSmall(0x716300, 0x1529F70, 15), new NativeBattleChoco97AppendNeutral(0x1010C, 15, 45, 2) }, operations);
    }

    [Fact]
    public void Mode9_appends_per_contact_in_order_including_duplicate_actors_and_rechecks_published_context()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco100DirectorInputs inputs = Inputs(operations) with { ReadSelectedContacts = frame =>
        {
            Assert.Equal((ushort)9, frame.Context.Word(0x40));
            return [Contact(3), Contact(3), Contact(6)];
        } };
        inputs = inputs with { Services = inputs.Services with { Execute = (frame, operation) =>
        {
            operations.Add(operation);
            return frame with { Context = frame.Context.WriteWord(0x44, 10) };
        } } };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco100Director.Advance70F370(Mode(Frame(23), 8, 9, 9), inputs);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco100AppendContactSecondary(3), new NativeBattleChoco100AppendContactSecondary(3), new NativeBattleChoco100AppendContactSecondary(6) }, operations);
        Assert.Equal((ushort)9, step.Frame.Context.Word(0x40));
        Assert.Equal((ushort)10, step.Frame.Context.Word(0x42));
        Assert.Equal((ushort)0, step.Frame.Context.Word(0x46));
    }

    [Fact]
    public void Phase14_saves_old_counter_before_sound_and_increment_not_after()
    {
        NativeBattleChoco100DirectorInputs inputs = Inputs([]);
        inputs = inputs with { Services = inputs.Services with { Execute = (frame, operation) =>
        {
            Assert.Equal(new NativeBattleChoco100DirectorSound(0x15297FC, 0, 128), operation);
            Assert.Equal((ushort)123, frame.Task.ReadWord(0x24));
            Assert.Equal((ushort)123, frame.Task.ReadWord(0x44));
            Assert.Equal((byte)14, frame.Task.ReadByte(0x29));
            return frame;
        } } };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco100Director.Advance70F370(Mode(Frame(14) with { Task = Frame(14).Task.WriteWord(0x24, 123) }, 6, 6, 6), inputs);
        Assert.Equal((ushort)124, step.Frame.Task.ReadWord(0x24));
        Assert.Equal((ushort)123, step.Frame.Task.ReadWord(0x44));
        Assert.Equal((byte)15, step.Frame.Task.ReadByte(0x29));
    }

    [Theory]
    [InlineData(32776, 32750, false)]
    [InlineData(32760, 32768, true)]
    [InlineData(119, 100, false)]
    [InlineData(120, 100, true)]
    public void Phase18_subtracts_signextended_WORDs_in_DWORD_width(ushort counter, ushort saved, bool advances)
    {
        NativeBattleChoco97DirectorFrame frame = Frame(18) with { Task = Frame(18).Task.WriteWord(0x24, counter).WriteWord(0x44, saved) };
        NativeBattleChoco97DirectorStep step = Advance(frame, []);
        Assert.Equal(advances ? (byte)19 : (byte)18, step.Frame.Task.ReadByte(0x29));
    }

    [Fact]
    public void Contact_countdown_reaches18_after82_callbacks_then_remaining18_callbacks_and_stop_observes_child_hold()
    {
        NativeBattleChoco97DirectorStep step = Advance(Mode(Frame(19), 8, 8, 8), []);
        Assert.Equal((ushort)100, step.Frame.Task.ReadWord(0x44));
        for (int callback = 1; callback <= 82; callback++)
        {
            NativeBattleChoco97DirectorFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco97DirectorFrame>(NativeBattleTestSnapshots.Json(step.Frame))!;
            NativeBattleChoco97DirectorStep next = Advance(step.Frame, []);
            Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(Advance(restored, [])));
            Assert.Equal(callback == 82 ? (ushort)1 : (ushort)0, next.Frame.Context.Word(0x48));
            step = next;
        }
        Assert.Equal((byte)21, step.Frame.Task.ReadByte(0x29));
        for (int callback = 0; callback < 18; callback++)
        {
            step = Advance(step.Frame, []);
        }
        Assert.Equal((byte)22, step.Frame.Task.ReadByte(0x29));
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame terminal = Mode(step.Frame, 9, 9, 9) with { Task = step.Frame.Task.WriteByte(0x28, 1).WriteWord(0x24, 65535) };
        terminal = terminal with { Context = terminal.Context.WriteWord(0x52, 65534) };
        step = Advance(terminal, operations);
        Assert.Equal(new NativeBattleChoco97StopVibration(-2), Assert.Single(operations));
        Assert.False(step.ReadyToComplete);
        Assert.Equal((ushort)0, step.Frame.Task.ReadWord(0x24));
        Assert.Equal((byte)23, step.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)10, step.Frame.Context.Word(0x44));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(15)]
    public void Each_actual_WAIT_phase_preserves_service_side_effect_before_next(byte phase)
    {
        NativeBattleChoco100DirectorInputs inputs = Inputs([]);
        inputs = inputs with { Services = inputs.Services with { ReleaseWait = frame => new(frame with { Context = frame.Context.WriteWord(0x4C, 0x1234) }, true) } };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco100Director.Advance70F370(Frame(phase), inputs);
        Assert.Equal((byte)(phase + 1), step.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)0x1234, step.Frame.Context.Word(0x4C));
    }

    [Theory]
    [InlineData(0, 65535, 21)]
    [InlineData(32768, 32767, 20)]
    public void Contact_timer_wraps_as_WORD_before_signed_threshold18(ushort initial, ushort expected, byte phase)
    {
        NativeBattleChoco97DirectorFrame frame = Frame(20) with { Task = Frame(20).Task.WriteWord(0x44, initial) };
        NativeBattleChoco97DirectorStep step = Advance(frame, []);
        Assert.Equal(expected, step.Frame.Task.ReadWord(0x44));
        Assert.Equal(phase, step.Frame.Task.ReadByte(0x29));
        Assert.Equal(phase == 21 ? (ushort)1 : (ushort)0, step.Frame.Context.Word(0x48));
    }

    [Fact]
    public void Unknown_selected_contacts_fail_at_real_mode9_boundary_and_other_modes_do_not_query_them()
    {
        NativeBattleChoco100DirectorInputs unknown = Inputs([]) with { ReadSelectedContacts = _ => default };
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100Director.Advance70F370(Mode(Frame(23), 8, 9, 9), unknown));
        NativeBattleChoco100DirectorInputs unused = Inputs([]) with { ReadSelectedContacts = _ => throw new InvalidOperationException() };
        NativeBattleChoco100Director.Advance70F370(Mode(Frame(23), 1, 2, 2), unused);
        Assert.Equal((2u, 7u), NativeBattleChoco100Director.SetFreeze7154B0(2, 7, 0));
        Assert.Equal((1u, 1u), NativeBattleChoco100Director.SetFreeze7154B0(0, 7, 1));
        Assert.Throws<NotSupportedException>(() => Advance(Frame(24), []));
    }

    private static NativeBattleContactPacket Contact(byte actor) => new(new(actor, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));
    private static NativeBattleChoco97DirectorFrame Frame(byte phase) => new(new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco100Director.Callback, new byte[0x48 - 12].ToImmutableArray()).WriteByte(0x29, phase), NativeBattleChoco100Context.Clear70F290());
    private static NativeBattleChoco97DirectorFrame Mode(NativeBattleChoco97DirectorFrame frame, ushort current, ushort published, ushort requested) => frame with { Context = frame.Context.WriteWord(0x40, current).WriteWord(0x42, published).WriteWord(0x44, requested) };
    private static NativeBattleChoco97DirectorStep Advance(NativeBattleChoco97DirectorFrame frame, List<NativeBattleChoco97DirectorOperation> operations) => NativeBattleChoco100Director.Advance70F370(frame, Inputs(operations));
    private static NativeBattleChoco100DirectorInputs Inputs(List<NativeBattleChoco97DirectorOperation> operations) => new(new(() => 0, offset => checked((uint)(0x10000 + offset)), frame => new(frame, true), (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    }), _ => []);
}
