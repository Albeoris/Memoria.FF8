using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191Tests
{
    [Fact]
    public void Entry_preserves_matrix_padding_and_sparse_particle_record_suffixes_using_first_contact_yaw_and_actual_target_matrix()
    {
        var fixture = Fixture();
        NativeBattleDoomtrain191Frame preserved = NativeBattleStorageSnapshots.Capture(fixture.Frame);
        NativeBattleDoomtrain191Frame result = Initialize(fixture);
        NativeBattleDoomtrain191State state = result.Memory.Doomtrain191!;
        Assert.Equal(3, state.FirstTarget);
        Assert.Equal(0, state.Caster);
        Assert.Equal(NativeBattleGfMatrices.Euler56CD50(new(0, 0x900, 0)).Rotation.ToArray(), state.BaseMatrix.ReadMatrix().Rotation.ToArray());
        Assert.Equal(123, state.BaseMatrix.ReadMatrix().Z);
        Assert.Equal(0, state.BaseMatrix.ReadMatrix().X);
        Assert.False(state.BaseMatrix.Known[18]);
        Assert.False(state.BaseMatrix.Known[19]);
        Assert.Equal(new byte[] { 0x71, 0x72 }, state.BaseMatrix.Bytes.AsSpan().Slice(18, 2).ToArray());
        int[] offsets = [0x2E000, 0x2F000, 0x30000, 0x31000, 0x32000, 0x33000];
        int[] counts = [170, 170, 150, 120, 130, 590];
        for (int pool = 0; pool < offsets.Length; pool++)
        {
            for (int index = 0; index < counts[pool]; index++)
            {
                int offset = offsets[pool] + 24 * index;
                Assert.Equal(new byte[4], result.Memory.Arena!.Read(offset, 4));
                Assert.All(result.Memory.Arena.Read(offset + 4, 20), value => Assert.Equal((byte)0xA5, value));
                Assert.All(preserved.Memory.Arena!.Read(offset, 24), value => Assert.Equal((byte)0xA5, value));
            }
        }
        Assert.Equal(0x20DFAB8u, state.ArenaAddress);
        Assert.Equal(0x2116AB8u, state.WorkPointers[6]);
        Assert.Equal(1, result.Memory.Textures!.Count);
        Assert.Equal(new[] { 2, 100 }, result.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(0x6472C0u, result.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(0x63F2D0u, result.Effect.Pools[1].Slots[0].Callback);
        Assert.Equal(0x24FBF80u, result.Effect.ReturnedDescriptor);
        var restored = fixture with { Frame = RoundTrip(fixture.Frame) };
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Initialize(restored)));
    }

    [Fact]
    public void Euler_result_invalidates_cold_known_padding_instead_of_retaining_the_identity_seed_padding()
    {
        var fixture = Fixture();
        NativeBattleDoomtrain191State owner = fixture.Frame.Memory.Doomtrain191!;
        owner = owner with { BaseMatrix = owner.BaseMatrix with { Known = Enumerable.Repeat(true, 32).ToImmutableArray() } };
        fixture = fixture with { Frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Doomtrain191 = owner } } };
        NativeBattleDoomtrain191Frame preserved = NativeBattleStorageSnapshots.Capture(fixture.Frame);
        NativeBattleDoomtrain191Frame result = Initialize(fixture);
        Assert.False(result.Memory.Doomtrain191!.BaseMatrix.Known[18]);
        Assert.False(result.Memory.Doomtrain191.BaseMatrix.Known[19]);
        Assert.All(result.Memory.Doomtrain191.BaseMatrix.Known.Take(18), value => Assert.True(value));
    }

    [Fact]
    public void Zero_displacement_still_requires_the_current_target_matrix_and_warm_missing_module_state_is_not_recreated()
    {
        var fixture = Fixture();
        Assert.Throws<NotSupportedException>(() => Initialize(fixture with { Models = fixture.Models.SetItem(3, fixture.Models[3] with { GeometryWorldMatrix = null }) }));
        Assert.Throws<NotSupportedException>(() => Initialize(fixture with { Frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Doomtrain191 = null } } }));
    }

    [Fact]
    public void Timeline_initial_tick_captures_actual_ring_snapshots_and_runs_new_camera_task_in_the_same_pass_without_a_second_process_owner()
    {
        var fixture = Fixture();
        NativeBattleDoomtrain191Frame initial = Initialize(fixture);
        List<Type> operations = [];
        NativeBattleDoomtrain191Inputs inputs = Inputs(fixture) with
        {
            Execute = (current, operation) =>
            {
                operations.Add(operation.GetType());
                return current;
            }
        };
        NativeBattleDoomtrain191Frame before = RoundTrip(initial);
        NativeBattleDoomtrain191Frame result = NativeBattleDoomtrain191Controller.Advance6472C0(initial, inputs, fixture.Resources);
        Assert.Equal(new[] { typeof(NativeBattleDoomtrain191StreamStart), typeof(NativeBattleDoomtrain191StartVibration), typeof(NativeBattleDoomtrain191CameraPublish) }, operations);
        Assert.Equal(new[] { 3, 4 }, result.Memory.Doomtrain191!.Targets);
        NativeBattleDoomtrain191ActorSnapshot snapshot = result.Memory.Doomtrain191.ActorSnapshots[3]!;
        Assert.Equal((short)-31, BinaryPrimitives.ReadInt16LittleEndian(snapshot.Position.AsSpan().Slice(6)));
        Assert.Equal((short)777, BinaryPrimitives.ReadInt16LittleEndian(snapshot.Scale.AsSpan().Slice(6)));
        Assert.False(result.Memory.Doomtrain191.BaseView.Known[18]);
        NativeBattleEffectTaskPool children = result.Effect.Pools[1];
        Assert.Equal(0x63E9C0u, children.Slots[1].Callback);
        Assert.Equal((ushort)1, children.Slots[1].ReadWord(0xC));
        Assert.Equal((ushort)1, children.Slots[0].ReadWord(0xC));
        Assert.Equal((ushort)1, result.Effect.Pools[0].Slots[0].ReadWord(0xC));
        operations.Clear();
        NativeBattleDoomtrain191Frame restored = NativeBattleDoomtrain191Controller.Advance6472C0(before, inputs, fixture.Resources);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(restored));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x200u)]
    public void Timeline_publishes_full_view_copy_before_pause_and_stalls_only_its_counter(uint flags)
    {
        var fixture = Fixture();
        NativeBattleDoomtrain191Frame initial = Initialize(fixture);
        int polls = 0;
        NativeBattleDoomtrain191Inputs inputs = Inputs(fixture) with
        {
            ReadGraphicsFlags = () => flags,
            PollFile = () =>
            {
                polls++;
                return -1;
            },
            Execute = (_, _) => throw new InvalidOperationException("Paused timeline must not execute its initialization.")
        };
        NativeBattleDoomtrain191Frame before = RoundTrip(initial);
        NativeBattleDoomtrain191Frame result = NativeBattleDoomtrain191Controller.Advance6472C0(initial, inputs, fixture.Resources);
        Assert.Equal(flags == 1 ? 0 : 1, polls);
        Assert.Equal((ushort)0, result.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Equal((ushort)1, result.Effect.Pools[0].Slots[0].ReadWord(0xC));
        Assert.Equal(Inputs(fixture).ReadCameraMatrix(), result.Memory.Doomtrain191!.CameraBackup);
    }

    [Fact]
    public void Second_timeline_tick_preserves_unwritten_rig_suffix_and_dispatches_both_new_rigs_in_order()
    {
        var fixture = Fixture();
        NativeBattleDoomtrain191Frame initial = Initialize(fixture);
        NativeBattleEffectTaskPool children = initial.Effect.Pools[1];
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(0xC, 1)).SetItem(1, NativeBattleTaskFixture.EditTask(children.Slots[1], taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })).SetItem(2, NativeBattleTaskFixture.EditTask(children.Slots[2], taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })) });
        initial = initial with { Effect = initial.Effect with { Pools = initial.Effect.Pools.SetItem(1, children) } };
        List<int> visits = [];
        NativeBattleDoomtrain191Inputs inputs = Inputs(fixture) with
        {
            AdvanceLeaf = (current, slot) =>
            {
                visits.Add(slot);
                return new(current, 0);
            }
        };
        NativeBattleDoomtrain191Frame before = RoundTrip(initial);
        NativeBattleDoomtrain191Frame result = NativeBattleDoomtrain191Controller.Advance6472C0(initial, inputs, fixture.Resources);
        Assert.Equal(new[] { 1, 2 }, visits);
        NativeBattleModulePoolTask first = result.Effect.Pools[1].Slots[1];
        NativeBattleModulePoolTask second = result.Effect.Pools[1].Slots[2];
        Assert.Equal((ushort)0xC80, first.ReadWord(0x10));
        Assert.Equal((ushort)0xF380, second.ReadWord(0x10));
        Assert.Equal((ushort)0xA5A5, first.ReadWord(0x1E));
        Assert.Equal((ushort)0xA5A5, second.ReadWord(0x22));
        Assert.Equal((ushort)1, first.ReadWord(0x20));
        Assert.Equal((ushort)0, second.ReadWord(0x20));
    }

    internal sealed record TestFixture(NativeBattleDoomtrain191Frame Frame, ImmutableArray<NativeBattleGraphicsModel> Models, NativeBattleActionPacketsState Packets, NativeBattleDoomtrain191Resources Resources);

    internal static NativeBattleDoomtrain191Frame Initialize(TestFixture fixture) => NativeBattleDoomtrain191Controller.Initialize63E730(fixture.Frame.Memory, fixture.Frame.Effect, Inputs(fixture), fixture.Resources, fixture.Frame.Random);

    internal static NativeBattleDoomtrain191Inputs Inputs(TestFixture fixture)
    {
        return new(fixture.Packets, actor => fixture.Models[actor], _ => new(999, -999, 123), () => fixture.Frame.Memory.Doomtrain191!.CameraBackup, () => new(new(0, 0, 0), new(0, 0, 1), 256, 0), () => 0, () => 0, (current, _) => current, (_, _) => throw new InvalidOperationException("Unexpected native leaf."));
    }

    internal static TestFixture Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 2, 191, null));
        for (int index = 0; index < 2; index++)
        {
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(2);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x2DFFF, Enumerable.Repeat((byte)0xA5, 0x37000 - 0x2DFFF).ToArray()) };
        byte[] packed = new byte[32];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(8 * index), 4096);
        }
        NativeBattlePackedMatrix32 identity = new(packed.ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray());
        packed[18] = 0x71;
        packed[19] = 0x72;
        memory = memory with { Doomtrain191 = memory.Doomtrain191! with { BaseMatrix = new(packed.ToImmutableArray(), identity.Known.SetItem(18, false).SetItem(19, false)), CameraBackup = identity } };
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(actor => new NativeBattleGraphicsModel(actor, 2) { Rotation14 = 0x100, GeometryWorldMatrix = identity.ReadMatrix(), HasLinkedModelRing140 = actor is 3 or 4, LinkedModelIndex140 = actor == 3 ? 4 : actor == 4 ? 3 : null, Transform = new(new(0, 0, 0), new(1, 2, 3), -31, 0, 4096) { Scale148 = new(100, 200, 300), Scale154 = 777 } }).ToImmutableArray();
        NativeBattleOriginalEffectState effect = new(191, 190, new(191, 0x63E730, 0x575560), new(0, 0, 8, 0, 0, 0), null);
        byte[] tim = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(tim, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(62), 1);
        ushort[] words = [0x81, 0, 0, 0, 0x41, 0, 0, 10, 0x80, 2, 0x40, 2, 0x8000];
        byte[] program = new byte[2 * words.Length];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(2 * index), words[index]);
        }
        NativeBattleDoomtrain191Resources resources = new(new("tim", tim.ToImmutableArray()), new("camera", program.ToImmutableArray()), new(0, new("test", [], [])), new("sound", new byte[20].ToImmutableArray()), 144, []);
        return new(new(memory, effect, new(1)), models, packets, resources);
    }

    private static T RoundTrip<T>(T value) => NativeBattleTestSnapshots.RestoreJson<T>(NativeBattleTestSnapshots.Json(value))!;
}
