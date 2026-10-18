using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco99DirectorTests
{
    [Theory]
    [InlineData(-100, 25, 0)]
    [InlineData(-1, 1, 0)]
    [InlineData(100, 201, 150)]
    public void Context_shares_signed_enemy_centroid_then_clamps_only_nonpositive_targetX(short first, short second, ushort expected)
    {
        NativeBattleGraphicsModel Model(int index) => new(index, 3)
        {
            Flags0 = index is 0 or 3 or 5 ? (ushort)2 : (ushort)0,
            Transform = new(new(0, 0, 0), index switch { 0 => new(30000, 999, 30000), 3 => new(first, 999, -301), 5 => new(second, 999, 100), _ => new(0, 0, 0) }, 0, 0, 4096)
        };
        NativeBattleChoco97Context actual = NativeBattleChoco99Context.Initialize718670(Model);
        Assert.Equal(expected, actual.Word(8));
        Assert.Equal(unchecked((ushort)(short)-100), actual.Word(12));
        Assert.Equal((ushort)2048, actual.Word(4));
        Assert.Equal((ushort)0, actual.Word(10));
        Assert.Equal(0x54, actual.Data.Length);
        Assert.All(actual.Data.Skip(16), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void Initial_phase_emits_actual_static_sources_cameraB0_and_ordered_services()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorStep step = Advance(Frame(0), operations);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97StartVibration(), new NativeBattleChoco97AppendNeutral(0x166A9F0, 18, 45, 2), new NativeBattleChoco97AppendProgram(0x7189B0, 0x166DC58, 0x4AC, 0, 0), new NativeBattleChoco97AppendAux(0x71DC40, 0xB0), new NativeBattleChoco97LoadModelTim() }, operations);
        Assert.Equal((byte)1, step.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)1, step.Frame.Task.ReadWord(0x24));
    }

    [Fact]
    public void Mode8_appends_both_secondary_tasks_before_four_ordered_programs()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorStep step = Advance(Mode(Frame(20), 7, 8, 8), operations);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco99AppendSecondary(0x7211D0, 0x58), new NativeBattleChoco99AppendSecondary(0x721390, 0x58), new NativeBattleChoco97AppendProgram(0x71FFB0, 0x10134, 0x2C, 4, 0), new NativeBattleChoco97AppendProgram(0x7201F0, 0x10138, 0x194, 5, 0), new NativeBattleChoco97AppendProgram(0x720320, 0x1013C, 0x30, 6, 0), new NativeBattleChoco97AppendProgram(0x720410, 0x10140, 0x1E8, 7, 0) }, operations);
        Assert.Equal((ushort)8, step.Frame.Context.Word(0x40));
        Assert.Equal((ushort)0, step.Frame.Context.Word(0x46));
    }

    [Fact]
    public void Mode10_appends_loaded_neutral_then_both_actual_small_child_suffixes()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        Advance(Mode(Frame(20), 9, 10, 10), operations);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendNeutral(0x10108, 4, 45, 2), new NativeBattleChoco99AppendSmall(0x71F880, 0x152AEF0, 8), new NativeBattleChoco99AppendSmall(0x8DDC30, 0x152AF04, 9) }, operations);
        Assert.Equal(0x2569338u, NativeBattleChoco99Director.SecondaryDescriptor);
        Assert.Equal(0x256C470u, NativeBattleChoco99Director.SmallDescriptor);
    }

    [Fact]
    public void Mode5_auxiliary_order_and_two_step_publication_reread_callback_context()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorInputs inputs = Inputs(operations) with
        {
            Execute = (frame, operation) =>
            {
                operations.Add(operation);
                return frame with { Context = frame.Context.WriteWord(0x44, 6) };
            }
        };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco99Director.Advance718750(Mode(Frame(20), 4, 5, 5), inputs);
        Assert.Equal(new NativeBattleChoco97DirectorOperation[] { new NativeBattleChoco97AppendProgram(0x71FEC0, 0x10130, 0xB0, 3, 0), new NativeBattleChoco97AppendAux(0x721100, 0xB0), new NativeBattleChoco97AppendAux(0x720500, 0xB0), new NativeBattleChoco97AppendAux(0x720790, 0xB0) }, operations);
        Assert.Equal((ushort)5, step.Frame.Context.Word(0x40));
        Assert.Equal((ushort)6, step.Frame.Context.Word(0x42));
        Assert.Equal((ushort)6, step.Frame.Context.Word(0x44));
    }

    [Theory]
    [InlineData(4, 0, 12)]
    [InlineData(5, 1, 13)]
    public void Phase12_does_not_release_IO_WAIT_before_mode5(ushort mode, int expectedReleases, byte phase)
    {
        int releases = 0;
        NativeBattleChoco97DirectorInputs inputs = Inputs([]) with
        {
            ReleaseWait = frame =>
            {
                releases++;
                return new(frame with { Context = frame.Context.WriteWord(0x4C, 0x9876) }, true);
            }
        };
        NativeBattleChoco97DirectorStep step = NativeBattleChoco99Director.Advance718750(Mode(Frame(12), mode, mode, mode), inputs);
        Assert.Equal(expectedReleases, releases);
        Assert.Equal(phase, step.Frame.Task.ReadByte(0x29));
        Assert.Equal(expectedReleases == 0 ? (ushort)0 : (ushort)0x9876, step.Frame.Context.Word(0x4C));
    }

    [Theory]
    [InlineData(0, 65535, 17)]
    [InlineData(1, 0, 17)]
    [InlineData(32768, 32767, 16)]
    public void Contact_countdown_wraps_before_signed_comparison_and_resets_exact40(ushort initial, ushort remaining, byte expectedPhase)
    {
        NativeBattleChoco97DirectorStep step = Advance((Frame(16) with { Task = Frame(16).Task.WriteWord(0x44, initial) }), []);
        Assert.Equal(expectedPhase, step.Frame.Task.ReadByte(0x29));
        Assert.Equal(expectedPhase == 17 ? (ushort)40 : remaining, step.Frame.Task.ReadWord(0x44));
        Assert.Equal(expectedPhase == 17 ? (ushort)1 : (ushort)0, step.Frame.Context.Word(0x48));
    }

    [Fact]
    public void Phase18_can_complete_without_inventing_a_vibration_stop_and_phase19_respects_child_hold()
    {
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorFrame frame = Mode(Frame(18), 10, 10, 10) with { Task = Frame(18).Task.WriteWord(0x24, 65535) };
        NativeBattleChoco97DirectorStep step = Advance(frame, operations);
        Assert.True(step.ReadyToComplete);
        Assert.Empty(operations);
        Assert.Equal((byte)19, step.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)0, step.Frame.Task.ReadWord(0x24));
        Assert.Equal((ushort)11, step.Frame.Context.Word(0x44));
        NativeBattleChoco97DirectorFrame held = Mode(step.Frame, 11, 11, 11) with { Task = step.Frame.Task.WriteByte(0x28, 1) };
        held = held with { Context = held.Context.WriteWord(0x52, 65534) };
        NativeBattleChoco97DirectorStep stopped = Advance(held, operations);
        Assert.Equal(new NativeBattleChoco97StopVibration(-2), Assert.Single(operations));
        Assert.False(stopped.ReadyToComplete);
        Assert.Equal((byte)20, stopped.Frame.Task.ReadByte(0x29));
    }

    [Fact]
    public void Signed_cumulative_counter_and_JSON_next_preserve_IO_and_mode_request_boundaries()
    {
        NativeBattleChoco97DirectorFrame frame = Frame(2) with { Task = Frame(2).Task.WriteWord(0x24, 32768) };
        List<NativeBattleChoco97DirectorOperation> operations = [];
        NativeBattleChoco97DirectorInputs busy = Inputs(operations) with { ReadIoWord166 = () => 1 };
        NativeBattleChoco97DirectorStep held = NativeBattleChoco99Director.Advance718750(frame, busy);
        Assert.Empty(operations);
        Assert.Equal((byte)2, held.Frame.Task.ReadByte(0x29));
        frame = Mode(Frame(5), 0, 0, 0);
        NativeBattleChoco97DirectorFrame restored = JsonSerializer.Deserialize<NativeBattleChoco97DirectorFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(Advance(frame, [])), JsonSerializer.Serialize(Advance(restored, [])));
        Assert.Equal((ushort)1, Advance(frame, []).Frame.Context.Word(0x44));
        Assert.Throws<NotSupportedException>(() => Advance(Frame(21), []));
    }

    private static NativeBattleChoco97DirectorFrame Frame(byte phase) => new(new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco99Director.Callback, new byte[0x48 - 12].ToImmutableArray()).WriteByte(0x29, phase), NativeBattleChoco99Context.Clear718670());
    private static NativeBattleChoco97DirectorFrame Mode(NativeBattleChoco97DirectorFrame frame, ushort current, ushort published, ushort requested) => frame with { Context = frame.Context.WriteWord(0x40, current).WriteWord(0x42, published).WriteWord(0x44, requested) };
    private static NativeBattleChoco97DirectorStep Advance(NativeBattleChoco97DirectorFrame frame, List<NativeBattleChoco97DirectorOperation> operations) => NativeBattleChoco99Director.Advance718750(frame, Inputs(operations));
    private static NativeBattleChoco97DirectorInputs Inputs(List<NativeBattleChoco97DirectorOperation> operations) => new(() => 0, offset => checked((uint)(0x10000 + offset)), frame => new(frame, true), (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });
}
