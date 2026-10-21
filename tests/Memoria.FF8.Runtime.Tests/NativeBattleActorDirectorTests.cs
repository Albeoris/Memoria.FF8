using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActorDirectorTests
{
    [Theory]
    [InlineData(16, 0x8AB140u, 0x2730680u, 90, 0xEC40)]
    [InlineData(61, 0x7E58C0u, 0x2601B00u, 100, 0x10680)]
    public void Original_profiles_initialize_real_pools_and_only_allocate_arrays_after_root_wait(ushort effect, uint entry, uint descriptor, int geometryCount, int geometryBytes)
    {
        var fixture = Fixture(effect);
        Assert.Equal(entry, fixture.Frame.Profile.Entry);
        Assert.Equal(descriptor, fixture.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(geometryCount, fixture.Frame.Profile.GeometryCapacity);
        Assert.Equal(geometryBytes, fixture.Frame.Profile.GeometryBytes);
        Assert.Equal(new[] { 2, 4, 3, 2, 50, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 88, 72, 676, 140, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Null(fixture.Frame.State.Generic.Auxiliary25F8);
        NativeBattleActorDirectorFrame frame = NativeBattleActorDirectorController.Advance(fixture.Frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(0x9000, frame.State.PrimaryDrawCursor);
        Assert.Equal(0x11000, frame.State.SecondaryDrawCursor);
        frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(0x1000, frame.State.PrimaryDrawCursor);
        Assert.Null(frame.State.Generic.Auxiliary25F8);
        frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(0x11000, frame.State.Generic.Auxiliary25F8);
        Assert.Equal(0x11870, frame.State.Generic.Auxiliary16F80);
        Assert.Equal(0x11870 + geometryBytes, frame.State.Generic.AuxiliaryCursor);
        Assert.Equal(new byte[0x870], frame.Memory.Arena!.Read(0x11000, 0x870));
        Assert.Equal(new byte[geometryBytes], frame.Memory.Arena.Read(0x11870, geometryBytes));
        Assert.Equal((ushort)0, frame.State.Generic.Counter26D76C0);
        Assert.Equal((ushort)0, frame.State.Generic.Counter26D82E8);
    }

    [Fact]
    public void Shared_initializer_captures_actual_selected_actor_and_target_packed_vertical_words()
    {
        var fixture = Fixture(61);
        NativeBattleActorDirectorState retained = fixture.Frame.State.WithDword(0x40, -1);
        NativeBattleModulePoolTask task = fixture.Frame.Effect.Pools[0].Slots[0];
        NativeBattleActorDirectorInputs inputs = fixture.Inputs with { ReadModel = index => Model(index) with { Transform = new(new(0, 0, 0), index == 0 ? new(100, -200, 300) : new(-400, 500, -600), index == 0 ? (short)-1234 : (short)5678, 0, 4096) } };
        NativeBattleActorDirectorState state = NativeBattleActorDirectorController.InitializeShared(retained, task, inputs);
        Assert.Equal(new short[] { 100, -200, 300, -1234, -400, 500, -600, 5678 }, Enumerable.Range(0, 8).Select(index => state.Word(index * 2)));
        Assert.Equal(0, state.Dword(0x40));
        Assert.Equal(-1, retained.Dword(0x40));
        Assert.Null(state.Generic.PrimaryDrawCursor);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(61)]
    public void Propagated_phase_one_creates_exact_generic_and_stage_children_before_same_pass_generic_dispatch(ushort effect)
    {
        var fixture = DirectorFixture(effect, 1);
        NativeBattleActorDirectorFrame frame = fixture.Frame.WithState(fixture.Frame.State.WithWord(0x40, 0).WithWord(0x42, 1).WithWord(0x44, 1));
        List<string> calls = [];
        NativeBattleActorDirectorInputs inputs = fixture.Inputs with
        {
            Execute = (current, operation) => { calls.Add(operation.GetType().Name); return current; },
            AdvanceGeneric = (current, address) =>
            {
                calls.Add("generic");
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                Assert.Equal(current.Profile.Callback(0x8AB890), task.Callback);
                Assert.Equal(current.Profile.SourceAddress, task.ReadDword(0x30));
                Assert.Equal((ushort)4, task.ReadWord(0x298));
                Assert.Equal((ushort)0, task.ReadWord(0x29A));
                Assert.Equal((ushort)0, task.ReadWord(0x29C));
                Assert.Equal((ushort)15, task.ReadWord(0x29E));
                Assert.Equal(fixture.Task, task.ReadDword(0x18));
                return new(current, 0);
            }
        };
        frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, inputs);
        Assert.Equal(new[] { nameof(NativeBattleActorDirectorGenericTask), nameof(NativeBattleActorDirectorSound), "generic", nameof(NativeBattleActorDirectorStage) }, calls);
        Assert.Equal((short)1, frame.State.Word(0x40));
        Assert.Equal((short)0, frame.State.Word(0x46));
        Assert.Equal((byte)2, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task).ReadByte(0x29));
        Assert.Equal((byte)2, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task).ReadByte(0x28));
    }

    [Theory]
    [InlineData(38, 2, 0)]
    [InlineData(39, 3, 1)]
    public void Contact_threshold_is_inclusive_after_the_shared_age_increment(short age, byte phase, short contact)
    {
        var fixture = DirectorFixture(61, 2);
        NativeBattleActorDirectorFrame frame = fixture.Frame.WithState(fixture.Frame.State.WithWord(0x40, 1).WithWord(0x42, 1).WithWord(0x44, 1).WithWord(0x46, age));
        frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal((short)(age + 1), frame.State.Word(0x46));
        Assert.Equal(contact, frame.State.Word(0x48));
        Assert.Equal(phase, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task).ReadByte(0x29));
    }

    [Theory]
    [InlineData(0, 0, 0, 1, 1)]
    [InlineData(0, 1, 1, 1, 0)]
    [InlineData(3, 1, 1, 2, 4)]
    [InlineData(3, 0, 0, 0, 3)]
    public void Request_phase_preserves_strict_current_requested_predecessor_gate(byte phase, short current, short requested, short nextRequest, byte nextPhase)
    {
        var fixture = DirectorFixture(16, phase);
        NativeBattleActorDirectorFrame frame = fixture.Frame.WithState(fixture.Frame.State.WithWord(0x40, current).WithWord(0x42, current).WithWord(0x44, requested));
        frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(nextRequest, frame.State.Word(0x44));
        Assert.Equal(nextPhase, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task).ReadByte(0x29));
    }

    [Fact]
    public void Stage_task_keeps_inclusive_age_wait_and_publishes_final_zero_depth_before_retirement()
    {
        var fixture = Fixture(61);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10));
        NativeBattleEffectHierarchyAppend stage = hierarchy.Append8DC540(4, fixture.Frame.Profile.Callback(0x8B0660), 140, root, (_, _, _) => new(0, 3));
        NativeBattleActorDirectorFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = stage.Hierarchy.Pools } };
        List<NativeBattleActorDirectorOperation> operations = [];
        NativeBattleActorDirectorInputs inputs = fixture.Inputs with { Execute = (current, operation) => { operations.Add(operation); return current; } };
        for (int pass = 0; pass < 5; pass++)
        {
            frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, inputs);
        }
        Assert.Equal(new short[] { 0, 512, 1024, 1536, 2048 }, operations.Cast<NativeBattleActorDirectorStage>().Select(operation => operation.Depth));
        Assert.True(((NativeBattleActorDirectorStage)operations[0]).ClearRgb);
        Assert.All(operations.Skip(1), operation => Assert.False(((NativeBattleActorDirectorStage)operation).ClearRgb));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(stage.Address, hierarchy.Read(stage.Address).WriteWord(0x24, 40));
        frame = NativeBattleActorDirectorController.Advance(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, inputs);
        Assert.Equal((byte)3, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(stage.Address).ReadByte(0x29));
        for (int pass = 0; pass < 8; pass++)
        {
            frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, inputs);
        }
        Assert.Equal(new NativeBattleActorDirectorStage(0, false), operations[^1]);
        Assert.Null(frame.Effect.Pools[4].Head);
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x28));
    }

    [Fact]
    public void Root_wait_uses_previous_survivors_and_snapshot_retains_actual_workspace_and_shared_state()
    {
        var fixture = Fixture(61);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 6).WriteWord(0x5E, 1));
        NativeBattleActorDirectorFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        NativeBattleActorDirectorFrame restored = JsonSerializer.Deserialize<NativeBattleActorDirectorFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleActorDirectorFrame expected = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, fixture.Inputs);
        NativeBattleActorDirectorFrame actual = NativeBattleActorDirectorController.Advance(restored, fixture.Resources, fixture.Inputs);
        Assert.Equal((byte)7, actual.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Throws<InvalidDataException>(() => NativeBattleActorDirectorController.Advance(frame, Resources(16), fixture.Inputs));
    }

    [Fact]
    public void Root_publishes_the_actual_packed_shared_view_and_overwrites_unknown_padding()
    {
        var fixture = Fixture(61);
        NativeBattleActionMatrix actual = NativeBattleCuragaMatrices.Identity8DD770() with { X = 123, Y = -456, Z = 789 };
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(actual, 0xA55A);
        NativeBattleActorDirectorFrame frame = NativeBattleActorDirectorController.Advance(fixture.Frame, fixture.Resources, fixture.Inputs with { PackedCamera = packed });
        Assert.Same(packed, frame.Memory.SharedEffectView2793E58);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(frame.State.Generic.CopiedCamera));
        frame = NativeBattleActorDirectorController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.False(frame.Memory.SharedEffectView2793E58!.Known[18]);
        Assert.False(frame.Memory.SharedEffectView2793E58.Known[19]);
        Assert.Equal(JsonSerializer.Serialize(fixture.Inputs.Camera), JsonSerializer.Serialize(frame.State.Generic.CopiedCamera));
    }

    private static (NativeBattleActorDirectorFrame Frame, NativeBattleActorDirectorResources Resources, NativeBattleActorDirectorInputs Inputs, uint Task) DirectorFixture(ushort effect, byte phase)
    {
        var fixture = Fixture(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(2, fixture.Frame.Profile.Callback(0x8AB600), 72, root, (_, _, _) => new(0, 3));
        hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteByte(0x29, phase));
        return (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, fixture.Inputs, child.Address);
    }

    private static (NativeBattleActorDirectorFrame Frame, NativeBattleActorDirectorResources Resources, NativeBattleActorDirectorInputs Inputs) Fixture(ushort effect)
    {
        NativeBattleActorDirectorProfile profile = NativeBattleActorDirectorProfile.ForEffect(effect);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 0, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.InitializerGlobal, 0x1000)] };
        NativeBattleActorDirectorResources resources = Resources(effect);
        NativeBattleActorDirectorFrame frame = NativeBattleActorDirectorController.Initialize(new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, selected, NativeBattleActorDirectorState.FromColdProcess(effect)), resources, packets);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActorDirectorInputs inputs = new(packets, NativeBattleCuragaMatrices.Identity8DD770(), Model, _ => pose, (current, _) => current, (current, _) => new(current, 0));
        return (frame, resources, inputs);
    }

    private static NativeBattleGraphicsModel Model(int index) => new(index, 0) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };

    private static NativeBattleActorDirectorResources Resources(ushort effect)
    {
        NativeBattleActorDirectorProfile profile = NativeBattleActorDirectorProfile.ForEffect(effect);
        byte[] camera = new byte[profile.CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), (ushort)camera.Length);
        return new(effect, new("camera", camera.ToImmutableArray()), profile.Sound, new(new byte[profile.SourceLength].ToImmutableArray()) { ResourceAddress = profile.SourceAddress });
    }
}
