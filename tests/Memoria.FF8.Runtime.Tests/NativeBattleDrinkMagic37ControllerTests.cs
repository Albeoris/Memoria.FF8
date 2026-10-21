using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrinkMagic37ControllerTests
{
    [Fact]
    public void Entry_preserves_actual_mutable_mesh_and_anchor_while_initializing_six_physical_pools()
    {
        var fixture = Fixture();
        Assert.Equal(new ushort[] { 100, 88, 676, 304, 96, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3, 100, 10, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleDrinkMagic37State retained = fixture.Frame.State with { OrbMesh = fixture.Frame.State.OrbMesh.Write(16, new byte[] { 0xAB }), CapturedAnchor8 = [1, 2, 3, 4, 5, 6, 7, 8], Counter26A7D9C = 9, Counter26A8DA8 = 10 };
        NativeBattleDrinkMagic37Frame result = NativeBattleDrinkMagic37Controller.Initialize(fixture.Frame.Memory, fixture.Frame.Effect, fixture.Frame.Random, fixture.Inputs.Packets, fixture.Resources, retained);
        Assert.Equal((byte)0xAB, result.State.OrbMesh.Read(16, 1)[0]);
        Assert.Equal(retained.CapturedAnchor8, result.State.CapturedAnchor8);
        Assert.Equal(0u, result.State.Counter26A7D9C);
        Assert.Equal(0u, result.State.Counter26A8DA8);
        Assert.Null(result.State.Nodes!.PrimaryDrawCursor);
        Assert.Null(result.Memory.SharedEffectView2793E58);
        Assert.Equal(256 + 0x12000, result.State.Nodes.AuxiliaryCursor);
        Assert.Equal((ushort)37, result.ToGeneric().Effect.Binding.Effect);
    }

    [Fact]
    public void Target_uses_delay40_and_releases_at_exact15_before_contact_on_the_following_pass()
    {
        var fixture = Fixture();
        List<(int Callback, NativeBattleDrinkMagic37Operation Operation)> operations = [];
        int callback = 0;
        NativeBattleDrinkMagic37Inputs inputs = fixture.Inputs with { Execute = (frame, operation) =>
        {
            operations.Add((callback, operation));
            return frame;
        } };
        NativeBattleDrinkMagic37Frame frame = fixture.Frame;
        for (callback = 1; callback <= 20; callback++)
        {
            frame = NativeBattleDrinkMagic37Controller.Advance(frame, fixture.Resources, inputs);
            if (callback == 4)
            {
                NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[2].Slots, task => task.Callback == 0x856E90);
                Assert.Equal(0x17C8420u, generic.ReadDword(0x30));
                Assert.Equal((ushort)40, generic.ReadWord(0x298));
                Assert.Equal((ushort)15, generic.ReadWord(0x29E));
                Assert.Equal((ushort)0, generic.ReadWord(0x29C));
                Assert.Contains(frame.Effect.Pools[3].Slots, task => task.Callback == 0x85BBD0);
                Assert.Equal(0x123456u, Assert.Single(operations.Select(item => item.Operation).OfType<NativeBattleDrinkMagic37Sound>()).Sound.Sound);
            }
            if (callback == 19)
            {
                Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x63));
                Assert.DoesNotContain(operations, item => item.Operation is NativeBattleDrinkMagic37Contact);
                NativeBattleDrinkMagic37Frame restored = JsonSerializer.Deserialize<NativeBattleDrinkMagic37Frame>(JsonSerializer.Serialize(frame))!;
                Assert.Equal(JsonSerializer.Serialize(NativeBattleDrinkMagic37Controller.Advance(frame, fixture.Resources, fixture.Inputs)), JsonSerializer.Serialize(NativeBattleDrinkMagic37Controller.Advance(restored, fixture.Resources, fixture.Inputs)));
            }
        }
        var contact = Assert.Single(operations, item => item.Operation is NativeBattleDrinkMagic37Contact);
        Assert.Equal(20, contact.Callback);
        Assert.Equal(0, ((NativeBattleDrinkMagic37Contact)contact.Operation).Index);
    }

    [Fact]
    public void Root_observes_previous_survivors_and_publishes_packed_camera_without_losing_unknown_padding()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint address = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x29, 6).WriteWord(0x5E, 1));
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(fixture.Inputs.Camera, null);
        NativeBattleDrinkMagic37Frame frame = NativeBattleDrinkMagic37Controller.Advance(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, fixture.Inputs with { PackedCamera = packed });
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(0x5E));
        Assert.Same(packed, frame.Memory.SharedEffectView2793E58);
        Assert.False(frame.Memory.SharedEffectView2793E58!.Known[18]);
        Assert.Equal(256 + 0x9000, frame.State.Nodes!.PrimaryDrawCursor);
    }

    internal static (NativeBattleDrinkMagic37Frame Frame, NativeBattleDrinkMagic37Resources Resources, NativeBattleDrinkMagic37Inputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 32, 15, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        byte[] cameraBytes = new byte[0x440];
        BinaryPrimitives.WriteUInt16LittleEndian(cameraBytes, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(cameraBytes.AsSpan(6), (ushort)cameraBytes.Length);
        NativeBattleDrinkMagic37Resources resources = new(new("camera", cameraBytes.ToImmutableArray()), new(new byte[0x430C].ToImmutableArray()) { ResourceAddress = 0x17C8420 }, Sprite(6, 4), Sprite(0, 2), OrbSprite(), OrbMesh(), Shape(), 0x123456);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(36);
        memory = memory with { Arena = memory.Arena!.Write(256 + 0x12000, Enumerable.Repeat((byte)0xA5, 0x438 + 0x6900).ToArray()) };
        NativeBattleOriginalEffectState selected = new(37, 36, new(37, 0x8569F0, 0x8569D0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x26A05C8, 256)] };
        NativeBattleDrinkMagic37Frame frame = NativeBattleDrinkMagic37Controller.Initialize(memory, selected, new(1), packets, resources, NativeBattleDrinkMagic37State.FromColdProcess(resources));
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleDrinkMagic37Inputs inputs = new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source, (current, _) => current);
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
            for (int quad = 0; quad < quads; quad++)
            {
                bytes[offset + 4 + quad * 20] = 1;
                bytes[offset + 6 + quad * 20] = 1;
                bytes[offset + 7 + quad * 20] = 0x2C;
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), ushort.MaxValue);
        return bytes.ToImmutableArray();
    }

    private static ImmutableArray<byte> OrbSprite()
    {
        byte[] bytes = new byte[428];
        bytes[3] = 6;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int frame = 0; frame < 16; frame++)
        {
            int offset = 44 + frame * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 1;
            bytes[offset + 6] = 1;
            bytes[offset + 7] = 0x2C;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return bytes.ToImmutableArray();
    }

    private static ImmutableArray<byte> OrbMesh()
    {
        byte[] bytes = new byte[8992];
        int cursor = 2216;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)cursor);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 276);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] counts = [0, 0, 0, 62, 0, 0, 0, 146];
        for (int group = 0; group < counts.Length; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), counts[group]);
            cursor += 4 + counts[group] * strides[group];
        }
        return bytes.ToImmutableArray();
    }

    private static ImmutableArray<byte> Shape()
    {
        byte[] bytes = new byte[2216];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 276);
        return bytes.ToImmutableArray();
    }
}
