using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMightyGuardTests
{
    [Theory]
    [InlineData(78, 0x7BE2D0u, 0x25EB620u, 0)]
    [InlineData(73, 0x7CBDB0u, 0x25F4050u, 1)]
    public void Root_waits_for_its_real_children_then_clears_physical_arrays_before_binding_and_fade(ushort effect, uint entry, uint descriptor, int bindings)
    {
        var fixture = Fixture(effect);
        Assert.Equal(entry, fixture.Frame.Profile.Entry);
        Assert.Equal(descriptor, fixture.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 2, 4, 4, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 88, 676, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 2).WriteByte(0x28, 1));
        NativeBattleMightyGuardFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Null(frame.State.Emitters);
        Assert.Empty(fixture.Operations);
        hierarchy = new(frame.Effect.Pools);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x28, 0)).Pools } };
        frame = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, fixture.Inputs);
        int first = 0x1000 + 0x12000;
        Assert.Equal(first, frame.State.Emitters);
        Assert.Equal(first + 0xEC4, frame.State.Geometry);
        Assert.Equal(first + 0xEC4 + 0x10680, frame.State.AuxiliaryCursor);
        Assert.All(frame.Memory.Arena!.Read(first, 0xEC4 + 0x10680), value => Assert.Equal((byte)0, value));
        Assert.Equal(bindings, fixture.Operations.OfType<NativeBattleMightyGuardBindCaster>().Count());
        if (bindings != 0)
        {
            Assert.Equal(new NativeBattleMightyGuardBindCaster(0, 0x25F4F38, 0x1587F4C, 1), Assert.Single(fixture.Operations.OfType<NativeBattleMightyGuardBindCaster>()));
        }
        Assert.Equal(new NativeBattleMightyGuardStage(0, true), Assert.Single(fixture.Operations.OfType<NativeBattleMightyGuardStage>()));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.SharedEffectView2793E58!.Bytes.AsSpan().Slice(18)));
    }

    [Theory]
    [InlineData(78, 255, 1, 0, 3, 0)]
    [InlineData(78, 0, 2, 1, 3, 0)]
    [InlineData(78, 1, 2, 1, 5, 30)]
    [InlineData(73, 1, 2, 1, 5, 35)]
    public void Root_repeats_signed_contact_index_and_preserves_header_index(ushort effect, byte contact, ushort count, byte expectedContact, byte phase, ushort wait)
    {
        NativeBattleModulePoolTask task = Task(NativeBattleMightyGuardProfile.ForEffect(effect).MainCallback, 100).WriteByte(0x29, 4).WriteByte(0x2A, 7).WriteByte(0x2B, contact).WriteWord(0x5A, count).WriteWord(0x58, 99);
        NativeBattleModulePoolTask next = NativeBattleMightyGuardController.AdvanceTargetIteration(task, NativeBattleMightyGuardProfile.ForEffect(effect));
        Assert.Equal((byte)7, next.ReadByte(0x2A));
        Assert.Equal(expectedContact, next.ReadByte(0x2B));
        Assert.Equal(phase, next.ReadByte(0x29));
        Assert.Equal(wait, next.ReadWord(0x60));
        Assert.Equal(phase == 3 ? (byte)1 : (byte)0, next.ReadByte(0x2E));
        NativeBattleModulePoolTask held = task.WriteByte(0x63, 1);
        Assert.Same(held, NativeBattleMightyGuardController.AdvanceTargetIteration(held, NativeBattleMightyGuardProfile.ForEffect(effect)));
    }

    [Theory]
    [InlineData(78, 39, 0)]
    [InlineData(78, 40, 1)]
    [InlineData(73, 44, 0)]
    [InlineData(73, 45, 1)]
    public void Target_applies_selected_contact_at_its_inclusive_original_age(ushort effect, ushort age, int expected)
    {
        var fixture = Fixture(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10));
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, fixture.Frame.Profile.TargetCallback, 88, root, (_, _, _) => new(0, 3));
        hierarchy = target.Hierarchy.Write(target.Address, target.Hierarchy.Read(target.Address).WriteByte(0x29, 2).WriteWord(0x24, age));
        NativeBattleMightyGuardFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        _ = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(expected, fixture.Operations.OfType<NativeBattleMightyGuardContact>().Count());
        if (expected != 0)
        {
            Assert.Equal(0, Assert.Single(fixture.Operations.OfType<NativeBattleMightyGuardContact>()).Index);
        }
    }

    [Theory]
    [InlineData(78, 10)]
    [InlineData(73, 16)]
    public void Sound_and_root_release_use_distinct_preincrement_ages(ushort effect, ushort soundAge)
    {
        var fixture = Fixture(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10).WriteByte(0x63, 1));
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, fixture.Frame.Profile.TargetCallback, 88, root, (_, _, _) => new(0, 3));
        hierarchy = target.Hierarchy.Write(target.Address, target.Hierarchy.Read(target.Address).WriteByte(0x29, 1).WriteWord(0x24, soundAge));
        NativeBattleMightyGuardFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(fixture.Frame.Profile.Sound, Assert.Single(fixture.Operations.OfType<NativeBattleMightyGuardSound>()).Sound.Sound);
        hierarchy = new(frame.Effect.Pools);
        Assert.Equal((byte)1, hierarchy.Read(root).ReadByte(0x63));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(target.Address, hierarchy.Read(target.Address).WriteWord(0x24, 20)).Pools } };
        frame = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(root).ReadByte(0x63));
    }

    [Theory]
    [InlineData(73)]
    [InlineData(78)]
    public void Generic_initialization_keeps_only_the_selected_contact_and_each_modules_fixed_bank(ushort effect)
    {
        var fixture = Fixture(effect, 2);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        NativeBattleEffectHierarchyAppend context = hierarchy.Append8DC540(2, fixture.Frame.Profile.GenericCallback, 676, root, (_, _, _) => new(0, 4));
        hierarchy = context.Hierarchy.Write(context.Address, context.Hierarchy.Read(context.Address).WriteByte(0x2B, 1).WriteDword(0x30, fixture.Frame.Profile.SourceAddress).WriteWord(0x29E, 45));
        NativeBattleMightyGuardFrame frame = NativeBattleMightyGuardController.InitializeContext(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, context.Address, fixture.Resources, fixture.Inputs.Packets, fixture.Inputs.ReadModel, fixture.Inputs.ReadPoseSource);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(context.Address);
        Assert.Equal((ushort)1, task.ReadWord(0x34 + 0x1C));
        Assert.Equal(4u, task.ReadDword(0x34 + 4));
        Assert.Equal(fixture.Frame.Profile.SourceAddress + 4, task.ReadDword(0x34 + 0x224));
        Assert.Equal(1u, frame.State.InitializationFlag);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(frame.State.Executable!.Value.AsSpan()));
        Assert.Equal(effect == 78 ? 1500u : 0u, task.ReadDword(0x34 + 0x214));
        Assert.Throws<NotSupportedException>(() => (NativeBattleMightyGuardState.FromColdProcess(effect) with { InitializationFlag = 1 }).InitializeSource(fixture.Resources));
    }

    [Theory]
    [InlineData(0, 100, 1024)]
    [InlineData(-100, 400, 1500)]
    [InlineData(0, 5500, 16384)]
    [InlineData(0, 12000, 1024)]
    [InlineData(-32768, 32767, 1024)]
    public void Target_scale_wraps_the_native_height_and_product_WORD_before_clamping(short minimum, short maximum, short expected)
    {
        Assert.Equal(expected, NativeBattleMightyGuardController.TargetScale(new(new(0, minimum, 0), new(0, maximum, 0))));
    }

    [Theory]
    [InlineData(4, 0x12FE, 0)]
    [InlineData(5, 0x1234, 0x36)]
    public void Mesh_scrolls_horizontal_low_byte_and_preserves_the_other_context_counter(sbyte source, ushort counter, ushort expected)
    {
        NativeBattleModulePoolTask task = Task(0x7BE820, 676).WriteWord(0x34 + 0x28, counter).WriteWord(0x34 + 0x2A, 0x5AA5);
        NativeBattleEsunaMeshPreparation step = NativeBattleMightyGuardMeshPackets.Prepare(task, source, 0, 0, null);
        Assert.Equal(expected, step.Task.ReadWord(0x34 + 0x28));
        Assert.Equal((ushort)0x5AA5, step.Task.ReadWord(0x34 + 0x2A));
        Assert.Equal(expected, step.Context.UOffset);
        Assert.Equal((ushort)0, step.Context.VOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 128, 256), step.Context.WindowAfter);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 256, 256), step.Context.WindowBefore);
        Assert.Equal(0x30u, step.Context.Flags);
        NativeBattleEsunaMeshOperands plain = NativeBattleMightyGuardMeshPackets.Prepare(counter, 6, 1, 7, 0x00332211);
        Assert.Equal(counter, plain.NextUvCounter);
        Assert.Equal((ushort)0, plain.Context.UOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 256, 256), plain.Context.WindowAfter);
        Assert.Equal(0xC0u, plain.Context.Flags);
        Assert.Equal(0x00332211u, plain.Context.FarColor);
        Assert.Throws<NotSupportedException>(() => NativeBattleMightyGuardMeshPackets.Prepare(counter, 4, 1, 7, null));
    }

    [Fact]
    public void Generic_context_reloads_body_writes_and_completes_on_the_following_native_phase()
    {
        var fixture = Fixture(78);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(2, fixture.Frame.Profile.GenericCallback, 676, root, (_, _, _) => new(0, 3));
        hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteByte(0x29, 1).WriteWord(0x34 + 0x20, 1).WriteWord(0x34 + 0x1A, 1).WriteWord(0x34 + 0x14, 1));
        NativeBattleMightyGuardFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        int calls = 0;
        NativeBattleMightyGuardInputs inputs = fixture.Inputs with
        {
            AdvanceNodes = (current, address) =>
            {
                calls++;
                Assert.Equal(child.Address, address);
                Assert.Equal(address, current.State.ContextTask);
                NativeBattleEffectHierarchy latest = new(current.Effect.Pools);
                latest = latest.Write(address, latest.Read(address).WriteWord(0x34 + 0x14, 0));
                return current with { Effect = current.Effect with { Pools = latest.Pools }, Random = new(99) };
            }
        };
        frame = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, inputs);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(child.Address);
        Assert.Equal((ushort)2, task.ReadWord(0x34 + 0x20));
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((ushort)0, frame.State.PassEmitters);
        Assert.Equal(99u, frame.Random.Seed);
        frame = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, inputs);
        Assert.Equal(1, calls);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(root).ReadByte(0x28));
        Assert.Equal((byte)0, (byte)(new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(child.Address).Flags & 1));
    }

    [Fact]
    public void Root_predecrements_wait_and_fade_writes_the_final_zero()
    {
        var fixture = Fixture(78);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 5).WriteWord(0x60, 0));
        NativeBattleMightyGuardFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, fixture.Inputs);
        hierarchy = new(frame.Effect.Pools);
        Assert.Equal((ushort)65535, hierarchy.Read(root).ReadWord(0x60));
        Assert.Equal((byte)6, hierarchy.Read(root).ReadByte(0x29));
        NativeBattleModulePoolTask fade = Assert.Single(frame.Effect.Pools[3].Slots, task => (task.Flags & 1) != 0);
        Assert.Equal((ushort)1024, fade.ReadWord(0x1C));
        Assert.Empty(fixture.Operations.OfType<NativeBattleMightyGuardStage>());
        for (int pass = 0; pass < 4; pass++)
        {
            frame = NativeBattleMightyGuardController.Advance(frame, fixture.Resources, fixture.Inputs);
        }
        Assert.Equal(new short[] { 768, 512, 256, 0 }, fixture.Operations.OfType<NativeBattleMightyGuardStage>().Select(operation => operation.Depth));
        Assert.All(fixture.Operations.OfType<NativeBattleMightyGuardStage>(), operation => Assert.False(operation.ClearRgb));
        Assert.DoesNotContain(frame.Effect.Pools[3].Slots, task => (task.Flags & 1) != 0);
    }

    private static NativeBattleModulePoolTask Task(uint callback, int bytes) => NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, (ushort)bytes, 1).Append(callback).Pool.Slots[0];

    private static (NativeBattleMightyGuardFrame Frame, NativeBattleMightyGuardResources Resources, NativeBattleMightyGuardInputs Inputs, List<NativeBattleMightyGuardOperation> Operations) Fixture(ushort effect, byte targets = 1)
    {
        NativeBattleMightyGuardProfile profile = NativeBattleMightyGuardProfile.ForEffect(effect);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 0, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        if (targets == 2)
        {
            (packets, _) = packets.AppendContact(new(new(4, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(targets);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.InitializerGlobal, 0x1000)] };
        byte[] camera = new byte[profile.CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), (ushort)camera.Length);
        byte[]? dat = effect == 73 ? new byte[0x1B74] : null;
        if (dat is not null)
        {
            uint[] fields = [3, 0x14, 0x1648, 0x1698, 0x1B74];
            for (int index = 0; index < fields.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(dat.AsSpan(index * 4), fields[index]);
            }
        }
        NativeBattleMightyGuardResources resources = new(effect, new("camera", camera.ToImmutableArray()), profile.Sound, new(new byte[profile.SourceLength].ToImmutableArray()) { ResourceAddress = profile.SourceAddress }, dat?.ToImmutableArray());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x13000, Enumerable.Repeat((byte)0xA5, 0xEC4 + 0x10680).ToArray()) };
        NativeBattleMightyGuardFrame frame = NativeBattleMightyGuardController.Initialize(new(memory, selected, NativeBattleMightyGuardState.FromColdProcess(effect), new(0)), resources, packets);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        List<NativeBattleMightyGuardOperation> operations = [];
        NativeBattleMightyGuardInputs inputs = new(packets, NativeBattleCuragaMatrices.Identity8DD770(), index => new(index, 0) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), SequenceState = new() { Pose = 0, PoseFrame114 = 1 }, GeometryBounds52 = new(new(0, -100, 0), new(0, 400, 0)) }, _ => pose, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        }, (_, _) => throw new InvalidOperationException("Unexpected particle body execution in this controller boundary fixture.")) { CopiedCameraPadding = 0xBEEF };
        return (frame, resources, inputs, operations);
    }
}
