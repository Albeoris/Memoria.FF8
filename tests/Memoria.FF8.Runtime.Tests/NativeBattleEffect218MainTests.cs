using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218MainTests
{
    [Fact]
    public void Paused_main_composes_current_view_before_returning_without_polling_or_advancing_age()
    {
        var (frame, address) = Fixture(16);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { X = 123, Y = -456, Z = 789 };
        NativeBattleEffect218Inputs inputs = Inputs(_ => throw new InvalidOperationException()) with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(view, null), PollIo508500 = _ => throw new InvalidOperationException() };
        var actual = AdvanceMain6328A0(frame, address, 1, inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal((ushort)16, Read(actual.Frame, address).ReadWord(12));
        NativeBattleActionMatrix published = NativeBattleArenaPose.ReadMatrix(actual.Frame.State.ReadView(CurrentView, 32), 0);
        Assert.Equal(view.Rotation.ToArray(), published.Rotation.ToArray());
        Assert.Equal((view.X, view.Y, view.Z), (published.X, published.Y, published.Z));
        Assert.Equal((ushort)0x1234, actual.Frame.State.ReadWord(CurrentView + 18));
    }

    [Fact]
    public void Busy_boundary_retries_without_queuing_or_uploading_then_uses_original_buffer_and_file()
    {
        var (frame, address) = Fixture(16);
        List<NativeBattleEffect218Operation> operations = [];
        bool busy = true;
        NativeBattleEffect218Inputs inputs = Inputs(operation => operations.Add(operation)) with { PollIo508500 = current => new(current, busy) };
        var wait = AdvanceMain6328A0(frame, address, 0, inputs);
        Assert.Empty(operations);
        Assert.Equal((ushort)16, Read(wait.Frame, address).ReadWord(12));
        busy = false;
        var ready = AdvanceMain6328A0(wait.Frame, address, 0, inputs);
        Assert.Equal(new NativeBattleEffect218UploadTim505E30(ArenaAddress + StreamedIoOffset), operations[0]);
        Assert.Equal(new NativeBattleEffect218QueueFile508480(706, ArenaAddress), operations[1]);
        Assert.Equal((ushort)17, Read(ready.Frame, address).ReadWord(12));
    }

    [Fact]
    public void Actual_contact_uses_sequence55_then_capture_and_contact_without_changing_actor_selection()
    {
        var (frame, address) = Fixture(383);
        List<NativeBattleEffect218Operation> operations = [];
        NativeBattleEffect218Inputs inputs = Inputs(operation => operations.Add(operation));
        inputs = inputs with
        {
            Execute = (current, operation) =>
            {
                operations.Add(operation);
                if (operation is NativeBattleEffect218CapturePoint571400 capture)
                {
                    NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)10)).Write(2, BitConverter.GetBytes((short)-20)).Write(4, BitConverter.GetBytes((short)30));
                    return WriteBytes(current, capture.DestinationAddress, point, 6);
                }
                return current;
            }
        };
        var actual = AdvanceMain6328A0(frame, address, 0, inputs);
        Assert.Equal(new NativeBattleEffect218SelectSequence505C00(0x1D97494, 55), operations[0]);
        Assert.Equal(new NativeBattleEffect218ModelFlags(0x1D97494, 0xFF, 0x20), operations[1]);
        Assert.IsType<NativeBattleEffect218CapturePoint571400>(operations[2]);
        Assert.IsType<NativeBattleEffect218Contact506690>(operations[3]);
        Assert.Equal(4, operations.Count);
        NativeBattleModulePoolTask child = Assert.Single(actual.Frame.Effect.Pools[0].Slots.Where(task => task.Callback == 0x633720));
        Assert.Equal((ushort)10, child.ReadWord(16));
        Assert.Equal((ushort)0, child.ReadWord(18));
        Assert.Equal((ushort)430, child.ReadWord(20));
        Assert.Equal((ushort)384, Read(actual.Frame, address).ReadWord(12));
    }

    [Theory]
    [InlineData(369, 0xFFFFFFFBu)]
    [InlineData(370, 0xFFFFFFFFu)]
    [InlineData(373, 0xFFFFFFFFu)]
    [InlineData(374, 0xFFFFFFFBu)]
    [InlineData(382, 0xFFFFFFFBu)]
    [InlineData(384, 0xFFFFFFFFu)]
    public void Flash_gate_masks_only_its_actual_DWORD_bit_in_the_reached_age_interval(short age, uint expected)
    {
        var (frame, address) = Fixture(age);
        frame = frame with { State = frame.State.WriteDword(0x24F3544, uint.MaxValue) };
        var actual = AdvanceMain6328A0(frame, address, 0, Inputs(_ => { }));
        Assert.Equal(expected, actual.Frame.State.ReadDword(0x24F3544));
    }

    [Fact]
    public void Main_retires_only_after_age579_and_appends_real_cleanup_before_restoring_target_flags()
    {
        var (frame, address) = Fixture(579);
        List<NativeBattleEffect218Operation> operations = [];
        var actual = AdvanceMain6328A0(frame, address, 0, Inputs(operation => operations.Add(operation)));
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)580, Read(actual.Frame, address).ReadWord(12));
        NativeBattleModulePoolTask cleanup = Assert.Single(actual.Frame.Effect.Pools[0].Slots.Where(task => task.Callback == 0x633BF0));
        Assert.Equal((ushort)0, cleanup.ReadWord(12));
        Assert.Equal((ushort)0, cleanup.ReadWord(14));
        Assert.Equal(new NativeBattleEffect218SelectSequence505C00(0x1D97494, 1), operations[0]);
        Assert.Equal(new NativeBattleEffect218ModelFlags(0x1D97494, 0xFF, 4), operations[1]);
        Assert.Equal(2, operations.Count);
    }

    private static NativeBattleEffect218Inputs Inputs(Action<NativeBattleEffect218Operation> observe) => new(_ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), frame => new(frame, false), frame => new(frame, 7), (_, _) => false, (frame, operation) =>
    {
        observe(operation);
        return frame;
    });

    private static (NativeBattleEffect218Frame Frame, uint Address) Fixture(short age)
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        (pool, int slot) = pool.Append(MainCallback);
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(12, unchecked((ushort)age)));
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [pool] };
        NativeBattleEffect218State state = NativeBattleEffect218State.FromColdProcess().WriteDword(TargetGlobal, 3).WriteDword(ArenaGlobal, ArenaAddress).WriteView(CameraBasis, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)), 32).WriteWord(CurrentView + 18, 0x1234);
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), state), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }
}
