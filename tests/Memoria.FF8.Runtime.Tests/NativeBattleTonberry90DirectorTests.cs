using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90DirectorTests
{
    private const uint Director = 0x25A3DF0;
    private const uint TaskAddress = 0x259EEB8;

    [Fact]
    public void Boot_clears_current_director_and_preserves_zero_anchor_padding_with_current_scale_WORD()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = fixture.Frame;
        uint moved = 0x25A4C40;
        NativeBattleTonberry90State state = frame.Memory.Tonberry90!;
        state = state.Write(moved, Enumerable.Repeat((byte)0xCC, 0x54).ToArray()) with { DirectorAddress1547168 = moved };
        frame = frame with { Memory = frame.Memory with { Tonberry90 = state } };
        NativeBattleModelTransform transform = new(new(1, 2, 3), new(123, 456, 32760), 0, 0, unchecked((short)0xF123));
        List<int> actors = [];
        frame = NativeBattleTonberry90Director.Initialize762BB0(frame, fixture.Child, actor =>
        {
            actors.Add(actor);
            return new(actor, 0) { Transform = transform };
        }, _ => throw new InvalidOperationException("An unposed model does not read a pose resource."));
        state = frame.Memory.Tonberry90!;
        Assert.Equal(new[] { 3, 3 }, actors);
        Assert.Equal((ushort)123, state.ReadWord(moved));
        Assert.Equal((ushort)0, state.ReadWord(moved + 2));
        Assert.Equal(unchecked((ushort)(32760 + 0xC00)), state.ReadWord(moved + 4));
        Assert.Equal((ushort)0, state.ReadWord(moved + 6));
        Assert.Equal((ushort)32760, state.ReadWord(moved + 0xC));
        Assert.Equal((ushort)0, state.ReadWord(moved + 0xE));
        Assert.Equal((ushort)0, state.ReadWord(moved + 0x4A));
        Assert.Equal((ushort)0xF123, state.ReadWord(moved + 0x4E));
        Assert.Equal((ushort)0, state.ReadWord(moved + 0x52));
    }

    [Fact]
    public void Request_active_and_spawned_phases_require_two_distinct_callbacks_before_actor_spawning()
    {
        var fixture = Fixture(11);
        NativeBattleTonberry90Frame frame = fixture.Frame;
        var requested = NativeBattleTonberry90Programme.RequestPhase763940(frame, 1);
        frame = NativeBattleTonberry90Director.Advance762C60(requested.Frame, TaskAddress, fixture.Services).Frame;
        Assert.Equal((ushort)0, frame.Memory.Tonberry90!.ReadWord(Director + 0x40));
        Assert.Equal((ushort)1, frame.Memory.Tonberry90.ReadWord(Director + 0x42));
        Assert.Null(frame.Effect.Pools[3].Head);
        frame = NativeBattleTonberry90Director.Advance762C60(frame, TaskAddress, fixture.Services).Frame;
        Assert.Equal((ushort)1, frame.Memory.Tonberry90!.ReadWord(Director + 0x40));
        Assert.Equal((ushort)0, frame.Memory.Tonberry90.ReadWord(Director + 0x46));
        NativeBattleModulePoolTask particle = frame.Effect.Pools[3].Slots[0];
        Assert.Equal(0x763EB0u, particle.Callback);
        Assert.Equal(0x520 - 12, NativeBattleStorageSnapshots.Capture(particle).Payload.Length);
        Assert.Equal(0x16D7DECu, particle.ReadDword(0x74));
        Assert.Equal(0x48Cu, particle.ReadDword(0x78));
        Assert.Equal((ushort)0, particle.ReadWord(0x80));
        Assert.Equal((ushort)0, particle.ReadWord(0x82));
        Assert.Equal(0x7681B0u, frame.Effect.Pools[4].Slots[0].Callback);
        Assert.Equal((byte)2, CurrentTask(frame).ReadByte(0x28));
    }

    [Fact]
    public void Initial_director_starts_the_real_vibration_pattern_and_appends_camera_programme()
    {
        var fixture = Fixture();
        NativeBattleTonberry90DirectorServices services = fixture.Services with
        {
            StartVibration4A29A0 = (frame, source, index, strength) =>
            {
                Assert.Equal(0x16D7CD0u, source);
                Assert.Equal(1, index);
                Assert.Equal(128, strength);
                return new(frame, -7);
            }
        };
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Director.Advance762C60(fixture.Frame, TaskAddress, services).Frame;
        Assert.Equal(unchecked((ushort)-7), frame.Memory.Tonberry90!.ReadWord(Director + 0x52));
        Assert.Equal(0x762DA0u, frame.Effect.Pools[4].Slots[0].Callback);
        Assert.Equal(NativeBattleTonberry90Controller.MainBank, frame.Effect.Pools[4].Slots[0].ReadDword(0x10));
        Assert.Equal(TaskAddress, frame.Effect.Pools[4].Slots[0].ReadDword(0x18));
        Assert.Equal((byte)1, CurrentTask(frame).ReadByte(0x28));
        Assert.Equal((byte)1, CurrentTask(frame).ReadByte(0x29));
    }

    [Fact]
    public void Phase_two_initializes_actual_creature_source_and_mode_before_appending_auxiliary()
    {
        var fixture = Fixture(11);
        NativeBattleTonberry90Frame frame = Word(fixture.Frame, Director + 0x40, 2);
        NativeBattleTonberry90DirectorServices services = fixture.Services with
        {
            InitializeProgram8DD0F0 = (current, address, source, mode) =>
            {
                Assert.Equal(0x25A3E48u, address);
                Assert.Equal(0x1547514u, source);
                Assert.Equal(3, mode);
                Assert.Null(current.Effect.Pools[4].Head);
                return current;
            }
        };
        frame = NativeBattleTonberry90Director.Spawn763D60(frame, TaskAddress, services);
        Assert.Equal(0x7674E0u, frame.Effect.Pools[5].Slots[0].Callback);
        Assert.Equal(0x7670E0u, frame.Effect.Pools[4].Slots[0].Callback);
        Assert.Equal((byte)2, CurrentTask(frame).ReadByte(0x28));
        var spawned = NativeBattleTonberry90Director.Spawn763E60(frame, TaskAddress, 0x1234, 0x4321, -1, 0x1234, 0x5678, fixture.Inputs.Packets);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(spawned.Frame.Effect.Pools).Read(spawned.Address);
        Assert.Equal(uint.MaxValue, task.ReadDword(0x78));
        Assert.Equal((ushort)0x1234, task.ReadWord(0x80));
        Assert.Equal((ushort)0x5678, task.ReadWord(0x82));
    }

    [Fact]
    public void Task_countdown_wrap_is_separate_from_requested_phase_and_vibration_stop_waits_for_acceptance()
    {
        var fixture = Fixture(8);
        NativeBattleTonberry90Frame frame = Word(Word(Word(fixture.Frame, Director + 0x40, 7), Director + 0x42, 7), Director + 0x44, 7);
        frame = WriteTask(frame, CurrentTask(frame).WriteWord(0x44, 0));
        frame = NativeBattleTonberry90Director.Advance762C60(frame, TaskAddress, fixture.Services).Frame;
        Assert.Equal(ushort.MaxValue, CurrentTask(frame).ReadWord(0x44));
        Assert.Equal((ushort)7, frame.Memory.Tonberry90!.ReadWord(Director + 0x44));
        Assert.Equal((ushort)1, frame.Memory.Tonberry90.ReadWord(Director + 0x48));
        List<short> stopped = [];
        NativeBattleTonberry90DirectorServices services = fixture.Services with
        {
            StopVibration4A2940 = (current, handle) =>
            {
                stopped.Add(handle);
                return current;
            }
        };
        frame = NativeBattleTonberry90Director.Advance762C60(frame, TaskAddress, services).Frame;
        Assert.Empty(stopped);
        frame = Word(Word(Word(Word(frame, Director + 0x40, 8), Director + 0x42, 8), Director + 0x44, 8), Director + 0x52, unchecked((ushort)-5));
        NativeBattleTonberry90Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleTonberry90Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleTonberry90Director.Advance762C60(restored, TaskAddress, services).Frame;
        Assert.Equal(new short[] { -5 }, stopped);
        Assert.Equal((ushort)9, frame.Memory.Tonberry90!.ReadWord(Director + 0x44));
        Assert.Equal((byte)10, CurrentTask(frame).ReadByte(0x29));
    }

    private static (NativeBattleTonberry90Frame Frame, uint Child, NativeBattleTonberry90Inputs Inputs, NativeBattleTonberry90DirectorServices Services) Fixture(byte phase = 0)
    {
        var fixture = NativeBattleTonberry90ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectTaskActors Select(uint _, sbyte __, sbyte ___) => new(0, 3);
        var child = hierarchy.Append8DC540(1, 0x762690, 0x58, NativeBattleTonberry90Controller.MainBank, Select);
        var director = child.Hierarchy.Append8DC540(2, 0x762C60, 0x48, child.Address, Select);
        hierarchy = director.Hierarchy.Write(director.Address, director.Hierarchy.Read(director.Address).WriteByte(0x29, phase));
        NativeBattleTonberry90Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleTonberry90DirectorServices services = new(fixture.Inputs.Packets, (_, _, _, _) => throw new InvalidOperationException("Vibration start was not reached."), (_, _) => throw new InvalidOperationException("Vibration stop was not reached."), (_, _, _, _) => throw new InvalidOperationException("Creature initialization was not reached."));
        return (frame, child.Address, fixture.Inputs, services);
    }

    private static NativeBattleModulePoolTask CurrentTask(NativeBattleTonberry90Frame frame) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(TaskAddress);
    private static NativeBattleTonberry90Frame WriteTask(NativeBattleTonberry90Frame frame, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(TaskAddress, task).Pools } };
    private static NativeBattleTonberry90Frame Word(NativeBattleTonberry90Frame frame, uint address, ushort value) => frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90!.Write(address, BitConverter.GetBytes(value)) } };
}
