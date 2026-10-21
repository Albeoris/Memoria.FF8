using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAquaControllerTests
{
    [Theory]
    [InlineData(55, 0x808820u, 0x808800u, 0x2610FA8u, 0x2611180u, 50)]
    [InlineData(72, 0x7D1E80u, 0x7D1E60u, 0x25F4F68u, 0x25F5140u, 35)]
    public void Entry_selects_actual_module_pools_and_retains_independent_working_owners(ushort effect, uint entry, uint initializer, uint textureGlobal, uint descriptor, int threshold)
    {
        var fixture = Fixture(effect);
        Assert.Equal(entry, fixture.Frame.Profile.Entry);
        Assert.Equal(initializer, fixture.Frame.Profile.Initializer);
        Assert.Equal(textureGlobal, fixture.Frame.Profile.InitializerGlobal);
        Assert.Equal(descriptor, fixture.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(threshold, fixture.Frame.Profile.ContactThreshold);
        Assert.Equal(new[] { 2, 4, 3, 50, 4, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 88, 72, 340, 96, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        NativeBattleModulePoolTask root = fixture.Frame.Effect.Pools[0].Slots[0];
        Assert.Equal(fixture.Frame.Profile.Callback(0x808970), root.Callback);
        Assert.Equal((byte)0, root.ReadByte(0x2C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Equal((ushort)0, root.ReadWord(0x58));
        Assert.Equal(0x1000, fixture.Frame.State.PrimaryBuffer);
        Assert.Equal(0xD000, fixture.Frame.State.AlternateBuffer);
        Assert.Equal(0x19000, fixture.Frame.State.BufferEnd);
        Assert.Null(fixture.Frame.State.PrimaryDrawCursor);
        Assert.Equal(fixture.Resources.WaveMesh.ToArray(), fixture.Frame.State.WaveMesh!.Value.ToArray());
    }

    [Fact]
    public void Shared_initialization_uses_enabled_physical_enemies_and_packs_caster_vertical_word()
    {
        var fixture = Fixture(55);
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => Model(index, new(30000, 0, 30000), 0)).ToArray();
        models[0] = Model(0, new(100, -50, -100), 2, -1234);
        models[1] = Model(1, new(-30000, 0, -30000), 2);
        models[3] = Model(3, new(-200, 0, 100), 2);
        models[4] = Model(4, new(600, 0, 500), 2);
        models[5] = Model(5, new(-30000, 0, -30000), 0);
        models[6] = Model(6, new(200, 0, -300), 2);
        NativeBattleAquaState retained = fixture.Frame.State.WithDword(0x40, -1).WithDword(0x50, -1);
        NativeBattleModulePoolTask task = fixture.Frame.Effect.Pools[0].Slots[0];
        NativeBattleAquaState state = NativeBattleAquaController.InitializeShared(retained, task, fixture.Frame.Effect.Parameters!, fixture.Inputs with { ReadModel = index => models[index] });
        Assert.Equal((short)100, state.Word(0));
        Assert.Equal((short)-50, state.Word(2));
        Assert.Equal((short)-100, state.Word(4));
        Assert.Equal((short)-1234, state.Word(6));
        Assert.Equal(unchecked((int)0xFB2EFF9C), state.Dword(4));
        Assert.Equal((short)200, state.Word(8));
        Assert.Equal((short)0, state.Word(10));
        Assert.Equal((short)100, state.Word(12));
        Assert.Equal(4096, state.Dword(0x3C));
        Assert.Equal(0, state.Dword(0x40));
        Assert.Equal(0, state.Dword(0x50));
        Assert.Equal(-1, retained.Dword(0x50));
        Assert.Equal(retained.WaveMesh!.Value.ToArray(), state.WaveMesh!.Value.ToArray());
    }

    [Theory]
    [InlineData(0, -4096, 0, 4096)]
    [InlineData(-4096, 0, 1024, 4096)]
    [InlineData(4096, 0, 3072, 4096)]
    [InlineData(0, 20000, 2048, 8192)]
    public void Signed_cardinal_angle_and_captured_contact_distance_keep_native_clamps(int x, int z, int angle, int distance)
    {
        var fixture = Fixture(72);
        NativeBattleAquaInputs inputs = fixture.Inputs with { ReadModel = index => Model(index, index == 3 ? new(checked((short)x), 0, checked((short)z)) : new(0, 0, 0), index == 3 ? (ushort)2 : (ushort)0) };
        NativeBattleAquaState state = NativeBattleAquaController.InitializeShared(fixture.Frame.State, fixture.Frame.Effect.Pools[0].Slots[0], fixture.Frame.Effect.Parameters!, inputs);
        Assert.Equal((short)angle, state.Word(0x4A));
        Assert.Equal(distance, state.Dword(0x3C));
    }

    [Fact]
    public void Distance_uses_captured_header_count_while_center_uses_all_enabled_enemy_rows()
    {
        var fixture = Fixture(55);
        NativeBattleActionPacketsState packets = fixture.Inputs.Packets;
        (packets, _) = packets.AppendContact(new(new(4, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        NativeBattleAquaInputs inputs = fixture.Inputs with { Packets = packets, ReadModel = index => Model(index, index == 4 ? new(20000, 0, 20000) : new(0, 0, 0), index is 3 or 4 ? (ushort)2 : (ushort)0) };
        NativeBattleAquaState state = NativeBattleAquaController.InitializeShared(fixture.Frame.State, fixture.Frame.Effect.Pools[0].Slots[0], fixture.Frame.Effect.Parameters!, inputs);
        Assert.Equal((short)10000, state.Word(8));
        Assert.Equal((short)10000, state.Word(12));
        Assert.Equal(4096, state.Dword(0x3C));
    }

    [Theory]
    [InlineData(55, 50)]
    [InlineData(72, 35)]
    public void Controller_contact_threshold_is_strict_after_incrementing_the_shared_counter(ushort effect, short threshold)
    {
        var fixture = ControllerFixture(effect, 3);
        NativeBattleAquaFrame frame = fixture.Frame.WithState(fixture.Frame.State.WithWord(0x46, threshold - 1));
        frame = NativeBattleAquaController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(threshold, frame.State.Word(0x46));
        Assert.Equal((short)0, frame.State.Word(0x48));
        Assert.Equal((byte)3, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task).ReadByte(0x29));
        frame = NativeBattleAquaController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal((short)1, frame.State.Word(0x48));
        Assert.Equal((byte)4, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task).ReadByte(0x29));
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Phase_requests_propagate_one_lane_per_callback_and_reset_age_only_on_current_phase_change()
    {
        var fixture = ControllerFixture(55, 6);
        NativeBattleAquaFrame frame = fixture.Frame.WithState(fixture.Frame.State.WithWord(0x40, 0).WithWord(0x42, 2).WithWord(0x44, 3).WithWord(0x46, 100));
        frame = NativeBattleAquaController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal((short)2, frame.State.Word(0x40));
        Assert.Equal((short)3, frame.State.Word(0x42));
        Assert.Equal((short)3, frame.State.Word(0x44));
        Assert.Equal((short)0, frame.State.Word(0x46));
        frame = NativeBattleAquaController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal((short)3, frame.State.Word(0x40));
        Assert.Equal((short)0, frame.State.Word(0x46));
        frame = NativeBattleAquaController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal((short)1, frame.State.Word(0x46));
        Assert.Empty(fixture.Operations);
    }

    [Theory]
    [InlineData(1, 0, 0, 1, 2)]
    [InlineData(1, 0, 2, 2, 1)]
    [InlineData(1, 1, 1, 1, 1)]
    [InlineData(4, 1, 1, 2, 5)]
    [InlineData(4, 0, 0, 0, 4)]
    public void RequestPhase_requires_current_and_requested_to_match_the_exact_predecessor(byte phase, short current, short requested, short expectedRequest, byte expectedPhase)
    {
        var fixture = ControllerFixture(55, phase);
        NativeBattleAquaFrame frame = fixture.Frame.WithState(fixture.Frame.State.WithWord(0x40, current).WithWord(0x42, current).WithWord(0x44, requested));
        frame = NativeBattleAquaController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(expectedRequest, frame.State.Word(0x44));
        Assert.Equal(expectedPhase, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task).ReadByte(0x29));
    }

    [Fact]
    public void Stage_fade_emits_actual_zero_clear_ramp_and_final_zero_before_retiring_child()
    {
        var fixture = Fixture(55);
        List<NativeBattleAquaOperation> operations = [];
        NativeBattleAquaInputs inputs = fixture.Inputs with { Execute = (frame, operation) => { operations.Add(operation); return frame; } };
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(3, 0x808ED0, 340, root, (_, _, _) => new(0, 3));
        NativeBattleAquaFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = child.Hierarchy.Pools } };
        for (int pass = 0; pass < 4; pass++)
        {
            frame = NativeBattleAquaController.Advance(frame, fixture.Resources, inputs);
        }
        Assert.Equal(new NativeBattleAquaOperation[] { new NativeBattleAquaStage(0, true), new NativeBattleAquaStage(512, false), new NativeBattleAquaStage(1024, false), new NativeBattleAquaStage(1536, false) }, operations);
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(child.Address, hierarchy.Read(child.Address).WriteWord(0x24, 41));
        frame = NativeBattleAquaController.Advance(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, inputs);
        for (int pass = 0; pass < 6; pass++)
        {
            frame = NativeBattleAquaController.Advance(frame, fixture.Resources, inputs);
        }
        Assert.Equal(new NativeBattleAquaStage(0, false), operations[^1]);
        Assert.Equal(new short[] { 1280, 1024, 768, 512, 256, 0 }, operations.Skip(4).Cast<NativeBattleAquaStage>().Select(operation => operation.Depth));
        Assert.Null(frame.Effect.Pools[3].Head);
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x28));
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    public void Root_phase_two_waits_for_its_actual_descendant_count(bool childPresent, byte expectedPhase)
    {
        var fixture = Fixture(55);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 2));
        if (childPresent)
        {
            NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(2, 0x808DD0, 72, root, (_, _, _) => new(0, 3));
            hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteByte(0x29, 6));
        }
        NativeBattleAquaFrame frame = NativeBattleAquaController.Advance(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, fixture.Inputs);
        Assert.Equal(expectedPhase, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 1, 3)]
    public void Target_phase_two_waits_for_children_or_the_actual_shared_release_flag(byte descendants, short released, byte expectedPhase)
    {
        var fixture = Fixture(55);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10));
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, 0x808B20, 88, root, (_, _, _) => new(0, 3));
        hierarchy = target.Hierarchy.Write(target.Address, target.Hierarchy.Read(target.Address).WriteByte(0x29, 2).WriteByte(0x28, descendants));
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleAquaInputs inputs = fixture.Inputs with { ReadModel = index => Model(index, new(0, 0, 0), 0) with { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, ReadPoseSource = _ => source };
        NativeBattleAquaFrame frame = (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }).WithState(fixture.Frame.State.WithWord(0x4C, released));
        frame = NativeBattleAquaController.Advance(frame, fixture.Resources, inputs);
        Assert.Equal(expectedPhase, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(target.Address).ReadByte(0x29));
    }

    private static (NativeBattleAquaFrame Frame, NativeBattleAquaResources Resources, NativeBattleAquaInputs Inputs, uint Task, List<NativeBattleAquaOperation> Operations) ControllerFixture(ushort effect, byte phase)
    {
        var fixture = Fixture(effect);
        List<NativeBattleAquaOperation> operations = [];
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(2, fixture.Frame.Profile.Callback(0x808DD0), 72, root, (_, _, _) => new(0, 3));
        hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteByte(0x29, phase).WriteWord(0x44, 1));
        return (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, fixture.Inputs with { Execute = (frame, operation) => { operations.Add(operation); return frame; } }, child.Address, operations);
    }

    private static (NativeBattleAquaFrame Frame, NativeBattleAquaResources Resources, NativeBattleAquaInputs Inputs) Fixture(ushort effect)
    {
        NativeBattleAquaProfile profile = NativeBattleAquaProfile.ForEffect(effect);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 2, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleAquaResources resources = Resources(effect);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Aqua55 = NativeBattleAquaState.FromColdProcess(55, resources.WaveMesh), Aqua72 = NativeBattleAquaState.FromColdProcess(72, resources.WaveMesh) };
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.InitializerGlobal, 0x1000)] };
        NativeBattleAquaFrame frame = NativeBattleAquaController.Initialize(new(memory, selected, new(1)), resources, packets);
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleAquaInputs inputs = new(packets, camera, new(camera, 320, 160, 120), 0, NativeBattleGteColorInterpolation.CodeConstants45F270, index => Model(index, new(0, 0, 0), 0), _ => throw new InvalidOperationException("Disabled actor rows do not request pose data."), (_, _, _) => throw new InvalidOperationException(), (current, _) => current);
        return (frame, resources, inputs);
    }

    private static NativeBattleGraphicsModel Model(int index, NativeBattleVector position, ushort flags, short vertical = 0) => new(index, flags) { Transform = new NativeBattleModelTransform(new(0, 0, 0), position, 0, 0, 4096) with { Vertical34 = vertical } };

    private static NativeBattleAquaResources Resources(ushort effect)
    {
        NativeBattleAquaProfile profile = NativeBattleAquaProfile.ForEffect(effect);
        byte[] camera = new byte[effect == 55 ? 308 : 416];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), (ushort)camera.Length);
        ImmutableDictionary<uint, ImmutableArray<byte>>.Builder sprites = ImmutableDictionary.CreateBuilder<uint, ImmutableArray<byte>>();
        foreach (uint canonical in new uint[] { 0x15A38DC, 0x15A3A3C, 0x15A3B9C, 0x15A3CFC, 0x15A3E5C, 0x15A4008 })
        {
            bool extended = canonical == 0x15A3E5C;
            byte[] sprite = new byte[extended ? 428 : 352];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), extended ? (ushort)16 : (ushort)13);
            sprites.Add(profile.SpriteAddress(canonical), sprite.ToImmutableArray());
        }
        return new(effect, new("camera", camera.ToImmutableArray()), sprites.ToImmutable(), new byte[2504].ToImmutableArray(), new byte[10600].ToImmutableArray(), effect == 55 ? 0x64224u : 0x5577Eu, effect == 72 ? new byte[7028].ToImmutableArray() : null);
    }
}
