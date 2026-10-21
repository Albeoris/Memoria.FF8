using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWindBlast29ControllerTests
{
    [Fact]
    public void Entry_retains_scan_state_until_native_phase2_and_uses_three_actual_pools()
    {
        Fixture fixture = new();
        Assert.Equal(new ushort[] { 100, 88, 676 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleWindBlast29State retained = fixture.Frame.State with { Counter26D74E8 = 123, Counter26D6B44 = 456, Nodes = fixture.Frame.State.Nodes! with { Counter26D76C0 = 38, Counter26D82E8 = 78, PrimaryDrawCursor = 777 } };
        NativeBattleWindBlast29Frame frame = NativeBattleWindBlast29Controller.Initialize(fixture.Frame.Memory, fixture.Frame.Effect, fixture.Frame.Random, fixture.Inputs.Packets, fixture.Resources, retained);
        Assert.Equal(0u, frame.State.Counter26D74E8);
        Assert.Equal(0u, frame.State.Counter26D6B44);
        Assert.Equal((ushort)38, frame.State.Nodes!.Counter26D76C0);
        Assert.Equal(777, frame.State.PrimaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256 + 0x8000, frame.State.PrimaryDrawCursor);
        Assert.Equal(256 + 0x10000, frame.State.SecondaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256, frame.State.PrimaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256 + 0x10000, frame.State.Nodes!.Auxiliary25F8);
        Assert.Equal(256 + 0x10000 + 0x10E0, frame.State.Nodes.Auxiliary16F80);
        Assert.Equal(256 + 0x10000 + 0x10E0 + 0xD200, frame.State.Nodes.AuxiliaryCursor);
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D76C0);
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D82E8);
    }

    [Fact]
    public void Target_creates_generic_mode1_delay12_duration45_and_emits_sound_only_at_age_zero()
    {
        Fixture fixture = new();
        NativeBattleWindBlast29Frame frame = fixture.Frame;
        for (int index = 0; index < 4; index++)
        {
            frame = fixture.Step(frame);
        }
        NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[2].Slots, task => task.Callback != 0);
        Assert.Equal(NativeBattleWindBlast29Profile.GenericCallback, generic.Callback);
        Assert.Equal(0x17D27DCu, generic.ReadDword(0x30));
        Assert.Equal((ushort)12, generic.ReadWord(0x298));
        Assert.Equal((ushort)45, generic.ReadWord(0x29E));
        Assert.Equal((ushort)1, generic.ReadWord(0x29C));
        Assert.Single(fixture.Operations.OfType<NativeBattleWindBlast29Sound>());
        frame = fixture.Step(frame);
        Assert.Single(fixture.Operations.OfType<NativeBattleWindBlast29Sound>());
        Assert.Empty(fixture.Operations.OfType<NativeBattleWindBlast29Contact>());
    }

    [Theory]
    [InlineData(40, false)]
    [InlineData(41, true)]
    [InlineData(42, false)]
    public void Parent_release_is_exact_age41_and_does_not_contact_in_same_phase(ushort age, bool releases)
    {
        Fixture fixture = new();
        NativeBattleWindBlast29Frame frame = fixture.TargetFrame(1, age, out uint root, out uint target);
        frame = fixture.Step(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        Assert.Equal(releases ? (byte)0 : (byte)1, hierarchy.Read(root).ReadByte(0x63));
        Assert.Equal(releases ? (byte)2 : (byte)1, hierarchy.Read(target).ReadByte(0x29));
        Assert.Empty(fixture.Operations.OfType<NativeBattleWindBlast29Contact>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Contact_fanout_uses_all_header_contacts_ignoring_target_selector_and_preserves_callback_writes(byte count)
    {
        Fixture fixture = new();
        NativeBattleWindBlast29Frame frame = fixture.TargetFrame(2, 42, out _, out uint target);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(target, hierarchy.Read(target).WriteByte(0x2B, 1));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleActionPacketsState packets = fixture.Inputs.Packets;
        NativeBattleContactPacket packet = packets.ReadContact(0);
        while (packets.ContactCount < count)
        {
            (packets, _) = packets.AppendContact(packet);
        }
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        header = header with { Payload = header.Payload.SetItem(16, count), UnknownByteMask = header.UnknownByteMask & ~(1u << 16), FirstContactIndex = count == 0 ? null : 0 };
        packets = packets with { Headers = packets.Headers.SetItem(0, header) };
        NativeBattleWindBlast29Inputs inputs = fixture.Inputs with { Packets = packets, Execute = (current, operation) =>
        {
            fixture.Operations.Add(operation);
            if (operation is NativeBattleWindBlast29Contact)
            {
                NativeCrtRandomState random = current.Random.Draw(out _);
                current = current with { Random = random };
            }
            return current;
        } };
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < count; index++)
        {
            expected = expected.Draw(out _);
        }
        frame = NativeBattleWindBlast29Controller.Advance(frame, fixture.Resources, inputs);
        Assert.Equal(Enumerable.Range(0, count), fixture.Operations.OfType<NativeBattleWindBlast29Contact>().Select(operation => operation.Index));
        Assert.Equal(expected, frame.Random);
        Assert.Equal((byte)3, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(target).ReadByte(0x29));
        Assert.Equal(1, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(target).ReadByte(0x26) & 1);
    }

    [Fact]
    public void Dead_target_waits_for_generic_child_and_does_not_repeat_contact_before_release()
    {
        Fixture fixture = new();
        NativeBattleWindBlast29Frame frame = fixture.TargetFrame(2, 42, out uint root, out uint target);
        frame = fixture.Step(frame);
        Assert.Single(fixture.Operations.OfType<NativeBattleWindBlast29Contact>());
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(target).ReadByte(0x28));
        fixture.CompleteGenerics = true;
        frame = fixture.Step(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        Assert.Equal((byte)0, hierarchy.Read(target).ReadByte(0x28));
        Assert.Equal((byte)1, hierarchy.Read(root).ReadByte(0x28));
        frame = fixture.Step(frame);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(root).ReadByte(0x28));
        Assert.Single(fixture.Operations.OfType<NativeBattleWindBlast29Contact>());
    }

    [Fact]
    public void Snapshot_and_generic_bridge_keep_single_camera_and_node_cursor_owner()
    {
        Fixture fixture = new();
        NativeBattleWindBlast29Frame frame = fixture.Step(fixture.Frame);
        NativeBattleCuragaFrame generic = frame.ToGeneric();
        Assert.NotNull(generic.State.CopiedCamera);
        frame = frame.WithGeneric(generic with { State = generic.State with { PrimaryDrawCursor = 12345 } });
        Assert.Null(frame.State.Nodes!.CopiedCamera);
        Assert.Equal(12345, frame.State.PrimaryDrawCursor);
        NativeBattleWindBlast29Frame restored = JsonSerializer.Deserialize<NativeBattleWindBlast29Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(fixture.Step(frame)), JsonSerializer.Serialize(fixture.Step(restored)));
    }

    internal sealed class Fixture
    {
        public NativeBattleWindBlast29Frame Frame { get; }
        public NativeBattleWindBlast29Resources Resources { get; }
        public NativeBattleWindBlast29Inputs Inputs { get; }
        public List<NativeBattleWindBlast29Operation> Operations { get; } = [];
        public bool CompleteGenerics { get; set; }
        public Fixture()
        {
            var prior = NativeBattleRaldo19ControllerTests.Fixture();
            byte[] camera = new byte[276];
            BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), 276);
            Resources = new(new("camera", camera.ToImmutableArray()), new(new byte[14192].ToImmutableArray()) { ResourceAddress = 0x17D27DC }, 0x61B65);
            NativeBattleOriginalEffectState effect = new(29, 28, new(29, NativeBattleWindBlast29Profile.Entry, NativeBattleWindBlast29Profile.Initializer), prior.Frame.Effect.Parameters, null) { ResourcePointers = [new(NativeBattleWindBlast29Profile.InitializerGlobal, 256)] };
            Frame = NativeBattleWindBlast29Controller.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(28), effect, new(1), prior.Inputs.Packets, Resources, NativeBattleWindBlast29State.FromColdProcess(123, 456));
            Inputs = new(prior.Inputs.Packets, NativeBattlePackedMatrix32.FromMatrix(prior.Inputs.Camera, null), prior.Inputs.ReadModel, prior.Inputs.ReadPoseSource, (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            }, AdvanceGeneric);
        }
        public NativeBattleWindBlast29Frame Step(NativeBattleWindBlast29Frame frame) => NativeBattleWindBlast29Controller.Advance(frame, Resources, Inputs);
        public NativeBattleWindBlast29Frame TargetFrame(byte phase, ushort age, out uint root, out uint target)
        {
            NativeBattleEffectHierarchy hierarchy = new(Frame.Effect.Pools);
            root = hierarchy.Address(0, 0);
            hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4).WriteByte(0x63, 1));
            NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(1, NativeBattleWindBlast29Profile.TargetCallback, 88, root, (_, _, _) => new(0, 3));
            target = append.Address;
            hierarchy = append.Hierarchy.Write(target, append.Hierarchy.Read(target).WriteByte(0x29, phase).WriteWord(0x24, age));
            NativeBattleEffectHierarchyAppend generic = hierarchy.Append8DC540(2, NativeBattleWindBlast29Profile.GenericCallback, 676, target, (_, _, _) => new(0, 3));
            return Frame with { Effect = Frame.Effect with { Pools = generic.Hierarchy.Pools } };
        }
        private NativeBattleWindBlast29LeafStep AdvanceGeneric(NativeBattleWindBlast29Frame frame, uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address).WriteWord(0x24, unchecked((ushort)(hierarchy.Read(address).ReadWord(0x24) + 1)));
            hierarchy = hierarchy.Write(address, task);
            if (CompleteGenerics)
            {
                hierarchy = hierarchy.Complete8DC530(address);
            }
            return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, CompleteGenerics ? 2 : 0);
        }
    }
}
