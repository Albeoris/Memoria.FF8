using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeltingBubble43ControllerTests
{
    [Fact]
    public void Entry_retains_channels_and_scan_state_until_native_phase2()
    {
        Fixture fixture = new();
        Assert.Equal(new ushort[] { 100, 88, 676 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        uint channel = NativeBattleMeltingBubble43Profile.ChannelRegions[0].Address;
        int relative = checked((int)(channel - NativeBattleMeltingBubble43Profile.ChannelBase));
        NativeBattleEffectArena channels = fixture.Frame.State.Channels.Write(relative, new byte[] { 0xCD, 0xAB }).MarkUnknown(relative + 2, 2);
        NativeBattleMeltingBubble43State retained = fixture.Frame.State with { Channels = channels, Counter2656220 = 123, Counter265587C = 456, Nodes = fixture.Frame.State.Nodes! with { Counter26D76C0 = 18, Counter26D82E8 = 98, PrimaryDrawCursor = 777 } };
        NativeBattleMeltingBubble43Frame frame = NativeBattleMeltingBubble43Controller.Initialize(fixture.Frame.Memory, fixture.Frame.Effect, fixture.Frame.Random, fixture.Inputs.Packets, fixture.Resources, retained);
        Assert.Equal(0u, frame.State.Counter2656220);
        Assert.Equal(0u, frame.State.Counter265587C);
        Assert.Same(channels, frame.State.Channels);
        Assert.Equal(new byte[] { 0xCD, 0xAB }, frame.State.ReadChannel(channel, 2));
        Assert.Throws<NotSupportedException>(() => frame.State.ReadChannel(channel + 2, 2));
        Assert.Equal((ushort)18, frame.State.Nodes!.Counter26D76C0);
        Assert.Equal(777, frame.State.PrimaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256 + 0xA000, frame.State.PrimaryDrawCursor);
        Assert.Equal(256 + 0x14000, frame.State.SecondaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256, frame.State.PrimaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256 + 0x14000, frame.State.Nodes!.Auxiliary25F8);
        Assert.Equal(256 + 0x14000 + 0x870, frame.State.Nodes.Auxiliary16F80);
        Assert.Equal(256 + 0x14000 + 0x870 + 0x10680, frame.State.Nodes.AuxiliaryCursor);
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D76C0);
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D82E8);
    }

    [Fact]
    public void Target_creates_generic_mode0_delay0_duration45_and_emits_sound_only_at_age_zero()
    {
        Fixture fixture = new();
        NativeBattleMeltingBubble43Frame frame = fixture.Frame;
        for (int index = 0; index < 4; index++)
        {
            frame = fixture.Step(frame);
        }
        NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[2].Slots, task => task.Callback != 0);
        Assert.Equal(NativeBattleMeltingBubble43Profile.GenericCallback, generic.Callback);
        Assert.Equal(0x15C6080u, generic.ReadDword(0x30));
        Assert.Equal((ushort)0, generic.ReadWord(0x298));
        Assert.Equal((ushort)45, generic.ReadWord(0x29E));
        Assert.Equal((ushort)0, generic.ReadWord(0x29C));
        Assert.Single(fixture.Operations.OfType<NativeBattleMeltingBubble43Sound>());
        frame = fixture.Step(frame);
        Assert.Single(fixture.Operations.OfType<NativeBattleMeltingBubble43Sound>());
        Assert.Empty(fixture.Operations.OfType<NativeBattleMeltingBubble43Contact>());
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void Parent_release_is_exact_age60_and_does_not_contact_in_same_phase(ushort age, bool releases)
    {
        Fixture fixture = new();
        NativeBattleMeltingBubble43Frame frame = fixture.TargetFrame(1, age, out uint root, out uint target);
        frame = fixture.Step(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        Assert.Equal(releases ? (byte)0 : (byte)1, hierarchy.Read(root).ReadByte(0x63));
        Assert.Equal(releases ? (byte)2 : (byte)1, hierarchy.Read(target).ReadByte(0x29));
        Assert.Empty(fixture.Operations.OfType<NativeBattleMeltingBubble43Contact>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Contact_uses_only_the_signed_selected_record_and_preserves_callback_writes(byte selected)
    {
        Fixture fixture = new();
        NativeBattleMeltingBubble43Frame frame = fixture.TargetFrame(2, 61, out _, out uint target);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(target, hierarchy.Read(target).WriteByte(0x2B, selected));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleActionPacketsState packets = fixture.Inputs.Packets;
        NativeBattleContactPacket packet = packets.ReadContact(0);
        while (packets.ContactCount < 3)
        {
            (packets, _) = packets.AppendContact(packet);
        }
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        header = header with { Payload = header.Payload.SetItem(16, 3), UnknownByteMask = header.UnknownByteMask & ~(1u << 16) };
        packets = packets with { Headers = packets.Headers.SetItem(0, header) };
        NativeBattleMeltingBubble43Inputs inputs = fixture.Inputs with { Packets = packets, Execute = (current, operation) =>
        {
            fixture.Operations.Add(operation);
            return operation is NativeBattleMeltingBubble43Contact ? current with { Random = current.Random.Draw(out _) } : current;
        } };
        NativeCrtRandomState expected = frame.Random.Draw(out _);
        frame = NativeBattleMeltingBubble43Controller.Advance(frame, fixture.Resources, inputs);
        Assert.Equal((int)selected, Assert.Single(fixture.Operations.OfType<NativeBattleMeltingBubble43Contact>()).Index);
        Assert.Equal(expected, frame.Random);
        Assert.Equal((byte)3, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(target).ReadByte(0x29));
    }

    [Fact]
    public void Dead_target_waits_for_generic_child_and_does_not_repeat_contact_before_release()
    {
        Fixture fixture = new();
        NativeBattleMeltingBubble43Frame frame = fixture.TargetFrame(2, 61, out uint root, out uint target);
        frame = fixture.Step(frame);
        Assert.Single(fixture.Operations.OfType<NativeBattleMeltingBubble43Contact>());
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(target).ReadByte(0x28));
        fixture.CompleteGenerics = true;
        frame = fixture.Step(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        Assert.Equal((byte)0, hierarchy.Read(target).ReadByte(0x28));
        Assert.Equal((byte)1, hierarchy.Read(root).ReadByte(0x28));
        frame = fixture.Step(frame);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(root).ReadByte(0x28));
        Assert.Single(fixture.Operations.OfType<NativeBattleMeltingBubble43Contact>());
    }

    [Fact]
    public void Snapshot_and_generic_bridge_keep_single_camera_and_node_cursor_owner()
    {
        Fixture fixture = new();
        NativeBattleMeltingBubble43Frame frame = fixture.Step(fixture.Frame);
        uint channel = NativeBattleMeltingBubble43Profile.ChannelRegions[0].Address;
        int offset = checked((int)(channel - NativeBattleMeltingBubble43Profile.ChannelBase));
        frame = frame with { State = frame.State with { Channels = frame.State.Channels.Write(offset, new byte[] { 1, 2 }).MarkUnknown(offset + 2, 2) } };
        NativeBattleCuragaFrame generic = frame.ToGeneric();
        Assert.Equal(new byte[] { 1, 2 }, generic.Memory.RequireMeltingBubble43().ReadChannel(channel, 2));
        Assert.Throws<NotSupportedException>(() => generic.Memory.RequireMeltingBubble43().ReadChannel(channel + 2, 2));
        Assert.NotNull(generic.State.CopiedCamera);
        frame = frame.WithGeneric(generic with { State = generic.State with { PrimaryDrawCursor = 12345 } });
        Assert.Null(frame.State.Nodes!.CopiedCamera);
        Assert.Equal(12345, frame.State.PrimaryDrawCursor);
        NativeBattleMeltingBubble43Frame restored = JsonSerializer.Deserialize<NativeBattleMeltingBubble43Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(fixture.Step(frame)), JsonSerializer.Serialize(fixture.Step(restored)));
    }

    internal sealed class Fixture
    {
        public NativeBattleMeltingBubble43Frame Frame { get; }
        public NativeBattleMeltingBubble43Resources Resources { get; }
        public NativeBattleMeltingBubble43Inputs Inputs { get; }
        public List<NativeBattleMeltingBubble43Operation> Operations { get; } = [];
        public bool CompleteGenerics { get; set; }
        public Fixture()
        {
            var prior = NativeBattleRaldo19ControllerTests.Fixture();
            Resources = CreateResources();
            NativeBattleOriginalEffectState effect = new(43, 42, new(43, NativeBattleMeltingBubble43Profile.Entry, NativeBattleMeltingBubble43Profile.Initializer), prior.Frame.Effect.Parameters, null) { ResourcePointers = [new(NativeBattleMeltingBubble43Profile.InitializerGlobal, 256)] };
            Frame = NativeBattleMeltingBubble43Controller.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(42), effect, new(1), prior.Inputs.Packets, Resources, NativeBattleMeltingBubble43State.FromColdProcess(123, 456, new byte[948].ToImmutableArray()));
            Inputs = new(prior.Inputs.Packets, NativeBattlePackedMatrix32.FromMatrix(prior.Inputs.Camera, null), prior.Inputs.ReadModel, prior.Inputs.ReadPoseSource, (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            }, AdvanceGeneric);
        }
        public NativeBattleMeltingBubble43Frame Step(NativeBattleMeltingBubble43Frame frame) => NativeBattleMeltingBubble43Controller.Advance(frame, Resources, Inputs);
        public NativeBattleMeltingBubble43Frame TargetFrame(byte phase, ushort age, out uint root, out uint target)
        {
            NativeBattleEffectHierarchy hierarchy = new(Frame.Effect.Pools);
            root = hierarchy.Address(0, 0);
            hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4).WriteByte(0x63, 1));
            NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(1, NativeBattleMeltingBubble43Profile.TargetCallback, 88, root, (_, _, _) => new(0, 3));
            target = append.Address;
            hierarchy = append.Hierarchy.Write(target, append.Hierarchy.Read(target).WriteByte(0x29, phase).WriteWord(0x24, age));
            NativeBattleEffectHierarchyAppend generic = hierarchy.Append8DC540(2, NativeBattleMeltingBubble43Profile.GenericCallback, 676, target, (_, _, _) => new(0, 3));
            return Frame with { Effect = Frame.Effect with { Pools = generic.Hierarchy.Pools } };
        }
        private NativeBattleMeltingBubble43LeafStep AdvanceGeneric(NativeBattleMeltingBubble43Frame frame, uint address)
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
        private static NativeBattleMeltingBubble43Resources CreateResources()
        {
            byte[] camera = new byte[296];
            BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), 296);
            byte[] header = new byte[260];
            BinaryPrimitives.WriteUInt32LittleEndian(header, 1);
            byte[] data = new byte[10748];
            uint[] addresses = [0x15C3684, 0x15C38B4, 0x15C3AE4, 0x15C3F44, 0x15C40F0, 0x15C429C, 0x15C4448, 0x15C45F4];
            int[] lengths = [560, 560, 560, 428, 428, 428, 428, 40];
            int[] frames = [21, 21, 21, 16, 16, 16, 16, 1];
            ImmutableArray<NativeBattleModuleResource>.Builder sprites = ImmutableArray.CreateBuilder<NativeBattleModuleResource>();
            for (int index = 0; index < addresses.Length; index++)
            {
                byte[] bytes = new byte[lengths[index]];
                bytes[3] = 6;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)frames[index]);
                int first = 12 + 2 * frames[index];
                for (int frame = 0; frame < frames[index]; frame++)
                {
                    int start = first + frame * 24;
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)start);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start), 1);
                    bytes[start + 4] = 1;
                    bytes[start + 6] = 1;
                    bytes[start + 7] = 0x2C;
                }
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * frames[index]), ushort.MaxValue);
                bytes.CopyTo(data, checked((int)(addresses[index] - 0x15C3684)));
                sprites.Add(new(0, addresses[index], 0, 0, bytes.ToImmutableArray()));
            }
            return new(new("camera", camera.ToImmutableArray()), new(header.ToImmutableArray()) { ResourceAddress = 0x15C6080 }, new(0, 0x15C3684, 0, 0, data.ToImmutableArray()), sprites.ToImmutable(), 0);
        }
    }
}
