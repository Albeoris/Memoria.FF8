using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Controller;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagmaBreath48ResourceMutationTests
{
    [Fact]
    public void External_shape_morph_writes_XYZ_only_and_retains_actual_W_knownness_without_GTE_changes()
    {
        byte[] mesh = new byte[56];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 24);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 2);
        var fixture = Fixture(mesh);
        NativeBattleEffectArena destination = fixture.Frame.State.ReadView(MeshAAddress, mesh.Length).MarkUnknown(8, 8).Write(16, new byte[8]).Write(22, BitConverter.GetBytes((ushort)0xBEEF));
        NativeBattleMagmaBreath48Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteView(MeshAAddress, destination, mesh.Length) };
        NativeBattleMagmaBreath48Frame actual = MorphResource(frame, fixture.Resources, MeshAAddress, 0x15B252C, 0x15B309C, 2048);
        Assert.Equal(new short[] { 0, 300, 650 }, Words(actual.State.Read(MeshAAddress + 8, 6)));
        Assert.Equal(new short[] { 10, 30, 50 }, Words(actual.State.Read(MeshAAddress + 16, 6)));
        Assert.Throws<NotSupportedException>(() => actual.State.ReadWord(MeshAAddress + 14));
        Assert.Equal((ushort)0xBEEF, actual.State.ReadWord(MeshAAddress + 22));
        Assert.Equal(frame.Memory, actual.Memory);
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(frame.State.Read(MeshBAddress, 16), actual.State.Read(MeshBAddress, 16));
    }

    [Theory]
    [InlineData(0u, 0x15B309Cu)]
    [InlineData(0x15B252Cu, 0u)]
    public void Null_shape_is_a_native_no_op_before_any_resource_read(uint first, uint second)
    {
        NativeBattleMagmaBreath48Frame frame = NativeBattleMagmaBreath48EngineTests.Frame(0);
        Assert.Same(frame, MorphResource(frame, null!, MeshAAddress, first, second, 2048));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void V_scroll_uses_current_owner_and_aggregate_underflow_wrap_without_touching_unknown_W(int group)
    {
        byte[] mesh = NativeBattleMagmaBreath48MeshTests.Mesh(group, group == 6 ? 0x34u : 0x3Cu).Bytes.ToArray();
        int face = 40 + (group + 1) * 4;
        int[] lanes = group == 6 ? [11, 13, 17] : [13, 17, 21, 23];
        for (int index = 0; index < lanes.Length; index++)
        {
            mesh[face + lanes[index]] = checked((byte)(index * 16));
        }
        var fixture = Fixture(mesh);
        NativeBattleMagmaBreath48Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteView(MeshAAddress, fixture.Frame.State.ReadView(MeshAAddress, mesh.Length).MarkUnknown(14, 2), mesh.Length) };
        NativeBattleMagmaBreath48Frame once = ScrollResource(frame, fixture.Resources, MeshAAddress);
        NativeBattleMagmaBreath48Frame onceSnapshot = NativeBattleStorageSnapshots.Capture(once);
        NativeBattleMagmaBreath48Frame twice = ScrollResource(once, fixture.Resources, MeshAAddress);
        for (int index = 0; index < lanes.Length; index++)
        {
            Assert.Equal(unchecked((byte)(index * 16 - 8 - 128)), onceSnapshot.State.Read(MeshAAddress + (uint)(face + lanes[index]), 1)[0]);
            Assert.Equal(unchecked((byte)(index * 16 - 16 - 128)), twice.State.Read(MeshAAddress + (uint)(face + lanes[index]), 1)[0]);
        }
        for (int offset = 0; offset < mesh.Length; offset++)
        {
            if (offset is 14 or 15 || lanes.Contains(offset - face))
            {
                continue;
            }
            Assert.Equal(mesh[offset], twice.State.Read(MeshAAddress + (uint)offset, 1)[0]);
        }
        Assert.Throws<NotSupportedException>(() => twice.State.ReadWord(MeshAAddress + 14));
        Assert.Equal(frame.Memory, twice.Memory);
    }

    private static (NativeBattleMagmaBreath48Frame Frame, NativeBattleMagmaBreath48Resources Resources) Fixture(byte[] mesh)
    {
        NativeBattleModuleResource first = new(0, 0x15B252C, 0, 0, Shape([-100, 200, 300, 111, 0, 20, 40, 222]));
        NativeBattleModuleResource second = new(1, 0x15B309C, 0, 0, Shape([100, 400, 1000, 333, 20, 40, 60, 444]));
        NativeBattleMagmaBreath48Resources resources = new(null!, null!, [], [new(0, MeshAAddress, mesh.ToImmutableArray())], [first, second], null!, 0, []);
        NativeBattleMagmaBreath48State state = NativeBattleMagmaBreath48State.FromColdProcess() with { SourcesLoaded = true, SourceHeader = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(1u)), MutablePe = NativeBattleEffectArena.Clear().Write(0, mesh).Write(MeshLength, Enumerable.Repeat((byte)0xA5, 16).ToArray()) };
        NativeBattleMagmaBreath48Frame frame = NativeBattleMagmaBreath48EngineTests.Frame(0) with { State = state };
        return (frame, resources);
    }

    private static ImmutableArray<byte> Shape(short[] words)
    {
        byte[] bytes = new byte[8 + words.Length * 2];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), words.Length / 4);
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), words[index]);
        }
        return bytes.ToImmutableArray();
    }
    private static short[] Words(byte[] bytes) => Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToArray();
}
