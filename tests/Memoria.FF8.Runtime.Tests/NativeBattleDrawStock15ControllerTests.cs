using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrawStock15ControllerTests
{
    [Fact]
    public void Entry_uses_five_actual_pools_and_the_initializer_pointer_without_replacing_retained_memory()
    {
        var fixture = Fixture();
        Assert.Equal(new uint[] { 0x2732CE8, 0x273ACD8, 0x273AA38, 0x2732CF8, 0x273ACC8 }, fixture.Frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(new uint[] { 0x2732C20, 0x273ACE8, 0x273A248, 0x2732D08, 0x273AA48 }, fixture.Frame.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Equal(new ushort[] { 100, 88, 676, 300, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3, 100, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleModulePoolTask root = fixture.Frame.Effect.Pools[0].Slots[0];
        Assert.Equal(0x8B0A50u, root.Callback);
        Assert.Equal((byte)2, root.ReadByte(0x2C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Equal((ushort)0, root.ReadWord(0x58));
        Assert.Null(fixture.Frame.State.Nodes.PrimaryDrawCursor);
        Assert.Null(fixture.Frame.Memory.SharedEffectView2793E58);
        Assert.Equal(256 + 0x12000, fixture.Frame.State.Nodes.AuxiliaryCursor);
        Assert.Equal((ushort)15, fixture.Resources.GenericNodes().Effect);
        Assert.Same(fixture.Resources.Executable, fixture.Resources.GenericNodes().Executable);
        Assert.Same(fixture.Frame.Memory, fixture.Frame.ToGeneric().Memory);
        Assert.Equal((ushort)15, fixture.Frame.ToGeneric().Effect.Binding.Effect);
    }

    [Fact]
    public void Root_allocates_exact_auxiliary_arrays_and_uses_9000_draw_banks_without_a_fade_child()
    {
        var fixture = Fixture();
        NativeBattleDrawStock15Frame frame = fixture.Frame;
        for (int callback = 1; callback <= 3; callback++)
        {
            frame = NativeBattleDrawStock15Controller.Advance(frame, fixture.Resources, fixture.Inputs);
            Assert.Equal(256 + ((callback & 1) == 1 ? 0x9000 : 0), frame.State.Nodes.PrimaryDrawCursor);
            Assert.Equal(256 + ((callback & 1) == 1 ? 0x12000 : 0x9000), frame.State.Nodes.SecondaryDrawCursor);
        }
        Assert.Equal(256 + 0x12000, frame.State.Nodes.Auxiliary25F8);
        Assert.Equal(256 + 0x12000 + 0x438, frame.State.Nodes.Auxiliary16F80);
        Assert.Equal(256 + 0x12000 + 0x438 + 0x6900, frame.State.Nodes.AuxiliaryCursor);
        Assert.All(frame.Memory.Arena!.Read(frame.State.Nodes.Auxiliary25F8!.Value, 0x438 + 0x6900), value => Assert.Equal((byte)0, value));
        Assert.All(frame.Effect.Pools.Skip(1), pool => Assert.Null(pool.Head));
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D76C0);
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D82E8);
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D82D8);
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D75C4);
    }

    [Fact]
    public void Target_spawns_original_generic_and_scheduler_then_contacts_on_callback20_after_equality15()
    {
        var fixture = Fixture();
        List<(int Callback, NativeBattleDrawStock15Operation Operation)> operations = [];
        int callback = 0;
        NativeBattleDrawStock15Inputs inputs = fixture.Inputs with { Execute = (frame, operation) =>
        {
            operations.Add((callback, operation));
            return frame;
        } };
        NativeBattleDrawStock15Frame frame = fixture.Frame;
        for (callback = 1; callback <= 20; callback++)
        {
            frame = NativeBattleDrawStock15Controller.Advance(frame, fixture.Resources, inputs);
            if (callback == 4)
            {
                NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[2].Slots, task => task.Callback == 0x8B0DA0);
                Assert.Equal(0x17FC68Cu, generic.ReadDword(0x30));
                Assert.Equal((ushort)0, generic.ReadWord(0x298));
                Assert.Equal((ushort)0, generic.ReadWord(0x29C));
                Assert.Equal((ushort)15, generic.ReadWord(0x29E));
                Assert.Contains(frame.Effect.Pools[3].Slots, task => task.Callback == 0x8B5AE0);
                Assert.Equal(new uint[] { 115, 116, 117 }, operations.Select(item => item.Operation).OfType<NativeBattleDrawStock15Sound>().Select(operation => operation.Sound.Sound));
                Assert.All(operations.Select(item => item.Operation).OfType<NativeBattleDrawStock15Sound>(), operation => Assert.Equal((byte?)128, operation.Sound.Pan));
                int firstGeneric = operations.FindIndex(item => item.Operation is NativeBattleDrawStock15GenericTask);
                int firstParticle = operations.FindIndex(item => item.Operation is NativeBattleDrawStock15Particle);
                Assert.True(firstGeneric >= 0 && firstParticle > firstGeneric);
            }
            if (callback == 19)
            {
                Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x63));
                Assert.DoesNotContain(operations, item => item.Operation is NativeBattleDrawStock15Contact);
                NativeBattleDrawStock15Frame restored = JsonSerializer.Deserialize<NativeBattleDrawStock15Frame>(JsonSerializer.Serialize(frame))!;
                Assert.Equal(JsonSerializer.Serialize(NativeBattleDrawStock15Controller.Advance(frame, fixture.Resources, fixture.Inputs)), JsonSerializer.Serialize(NativeBattleDrawStock15Controller.Advance(restored, fixture.Resources, fixture.Inputs)));
            }
        }
        var contact = Assert.Single(operations, item => item.Operation is NativeBattleDrawStock15Contact);
        Assert.Equal(20, contact.Callback);
        Assert.Equal(0, ((NativeBattleDrawStock15Contact)contact.Operation).Index);
        Assert.Equal(0x2732CE8u, frame.Effect.ReturnedDescriptor);
        Assert.NotNull(frame.Effect.Pools[2].Head);
        Assert.Null(frame.Effect.Pools[4].Head);
    }

    [Fact]
    public void Entry_uses_signed_selectors_and_preserves_unwritten_suffix_words()
    {
        var fixture = Fixture();
        NativeBattleActionPacketsState packets = fixture.Inputs.Packets;
        NativeBattleActionHeaderSlot selected = packets.Headers[0] with { FirstContactIndex = 2 };
        NativeBattleActionHeaderSlot parameter = selected with { Payload = selected.Payload.SetItem(0, (byte)6).SetItem(16, (byte)4).SetItem(17, (byte)2), UnknownByteMask = selected.UnknownByteMask & ~((1u << 0) | (1u << 16) | (1u << 17)) };
        packets = packets with { Headers = packets.Headers.SetItem(0, selected).SetItem(1, parameter) };
        NativeBattleModulePoolTask task = fixture.Frame.Effect.Pools[0].Slots[0].WriteByte(0x2A, 255).WriteByte(0x2B, 254).WriteWord(0x60, 0xABCD);
        NativeBattleEffectParameters parameters = fixture.Frame.Effect.Parameters! with { HeaderIndex7C = 1 };
        NativeBattleModulePoolTask result = NativeBattleDrawStock15Controller.InitializeRoot(task, parameters, packets);
        Assert.Equal((byte)2, result.ReadByte(0x2C));
        Assert.Equal((byte)3, result.ReadByte(0x2D));
        Assert.Equal((ushort)4, result.ReadWord(0x5A));
        Assert.Equal((ushort)2, result.ReadWord(0x58));
        Assert.Equal((byte)3, result.ReadByte(0x2F));
        Assert.Equal((byte)255, result.ReadByte(0x2A));
        Assert.Equal((byte)254, result.ReadByte(0x2B));
        Assert.Equal((ushort)0xABCD, result.ReadWord(0x60));
        Assert.Throws<NotSupportedException>(() => NativeBattleDrawStock15Controller.InitializeRoot(task, parameters with { HeaderIndex7C = 0 }, packets));
    }

    [Fact]
    public void Camera_copy_preserves_the_single_packed_owner_and_its_unknown_padding()
    {
        var fixture = Fixture();
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(fixture.Inputs.Camera, null);
        packed = packed with { Bytes = packed.Bytes.SetItem(18, 0xCD).SetItem(19, 0xAB) };
        NativeBattleDrawStock15Frame frame = NativeBattleDrawStock15Controller.Advance(fixture.Frame, fixture.Resources, fixture.Inputs with { PackedCamera = packed });
        Assert.Same(packed, frame.Memory.SharedEffectView2793E58);
        Assert.False(frame.Memory.SharedEffectView2793E58!.Known[18]);
        Assert.Equal((byte)0xCD, frame.Memory.SharedEffectView2793E58.Bytes[18]);
        Assert.Equal(JsonSerializer.Serialize(packed.ReadMatrix()), JsonSerializer.Serialize(frame.State.Nodes.CopiedCamera));
    }

    [Fact]
    public void Signed_root_loop_and_prior_survivors_are_observed_without_a_duration_shortcut()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4).WriteByte(0x2A, 255).WriteWord(0x60, 0xABCD));
        NativeBattleDrawStock15Frame frame = NativeBattleDrawStock15Controller.Advance(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, fixture.Inputs);
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x2A));
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 6).WriteWord(0x5E, 1));
        frame = NativeBattleDrawStock15Controller.Advance(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, fixture.Inputs);
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(0x5E));
        for (int callback = 0; callback < 4; callback++)
        {
            frame = NativeBattleDrawStock15Controller.Advance(frame, fixture.Resources, fixture.Inputs);
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)0xABCD, frame.Effect.Pools[0].Slots[0].ReadWord(0x60));
    }

    [Fact]
    public void Root_rejects_another_module_binding_or_incomplete_resource_bank()
    {
        var fixture = Fixture();
        NativeBattleOriginalEffectState other = fixture.Frame.Effect with { Binding = new(28, 0x87DB10, 0x87DAF0) };
        Assert.Throws<InvalidDataException>(() => NativeBattleDrawStock15Controller.Initialize(fixture.Frame.Memory, other, fixture.Frame.Random, fixture.Inputs.Packets, fixture.Resources));
        Assert.Throws<InvalidDataException>(() => (fixture.Resources with { Executable = fixture.Resources.Executable with { ResourceAddress = 0x17D5F4C } }).Validate());
        Assert.Throws<InvalidDataException>(() => (fixture.Resources with { Animation = new("incomplete", []) }).Validate());
        Assert.Throws<InvalidDataException>(() => fixture.Frame.WithGeneric(fixture.Frame.ToGeneric() with { Effect = other }));
    }

    private static (NativeBattleDrawStock15Frame Frame, NativeBattleDrawStock15Resources Resources, NativeBattleDrawStock15Inputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 32, 15, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        byte[] cameraBytes = new byte[0x7CC];
        BinaryPrimitives.WriteUInt16LittleEndian(cameraBytes, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(cameraBytes.AsSpan(6), (ushort)cameraBytes.Length);
        NativeBattleDrawStock15Resources resources = new(new("camera", cameraBytes.ToImmutableArray()), new(new byte[0x5944].ToImmutableArray()) { ResourceAddress = 0x17FC68C }, new(Sprite(6, 4), Sprite(0, 2)));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(14);
        memory = memory with { Arena = memory.Arena!.Write(256 + 0x12000, Enumerable.Repeat((byte)0xA5, 0x438 + 0x6900).ToArray()) };
        NativeBattleOriginalEffectState selected = new(15, 14, new(15, 0x8B0910, 0x8B08F0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x2732BF8, 256)] };
        NativeBattleDrawStock15Frame frame = NativeBattleDrawStock15Controller.Initialize(memory, selected, new(1), packets, resources);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleDrawStock15Inputs inputs = new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source, _ => new(0, 0, 0, unchecked((short)0xABCD)), (current, _) => current);
        return (frame, resources, inputs);
    }

    private static ImmutableArray<byte> Sprite(byte kind, int quads)
    {
        int stride = 4 + 20 * quads;
        byte[] bytes = new byte[24 + 6 * stride];
        bytes[3] = kind;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 6);
        for (int frame = 0; frame < 6; frame++)
        {
            int offset = 24 + frame * stride;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), (uint)quads);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), ushort.MaxValue);
        return bytes.ToImmutableArray();
    }
}
