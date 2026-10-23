using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagmaBreath48StateTests
{
    [Fact]
    public void Cold_process_zeros_only_exact_typed_channels_and_requires_explicit_PE_binding()
    {
        var state = NativeBattleMagmaBreath48State.FromColdProcess();
        state.Validate();
        Assert.False(state.SourcesLoaded);
        Assert.Equal(95, ChannelRegions.Length);
        Assert.Equal(3880, ChannelRegions.Sum(region => region.Length));
        foreach (var region in ChannelRegions)
        {
            Assert.Equal(new byte[region.Length], state.Read(region.Address, region.Length));
        }
        Assert.Throws<InvalidDataException>(() => state.ReadWord(0x264963C));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(0x264B3C6));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(SourceAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MeshAAddress));
    }

    [Fact]
    public void Actual_source_binding_is_once_only_and_current_mesh_UV_XYZ_W_survive_JSON()
    {
        var resources = Resources();
        var cold = NativeBattleMagmaBreath48State.FromColdProcess();
        var state = cold.EnsureSources(resources);
        Assert.Equal(resources.Executable.Data.ToArray(), state.Read(SourceAddress, SourceLength));
        Assert.Equal(resources.ReadMesh(MeshAAddress).Bytes.ToArray(), state.Read(MeshAAddress, MeshLength));
        Assert.Same(cold.Channels, state.Channels);
        Assert.Same(cold.Globals, state.Globals);
        uint vertex = MeshAAddress + 8;
        uint face = MeshBAddress + 8 + 182 * 8 + 7 * 4;
        state = state.WriteWord(vertex, 123).WriteWord(vertex + 6, 456).WriteWord(face + 16, 789).WriteView(vertex + 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 8);
        Assert.Same(state, state.EnsureSources(resources));
        var restored = JsonSerializer.Deserialize<NativeBattleMagmaBreath48State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal((ushort)123, restored.ReadWord(vertex));
        Assert.Equal((ushort)456, restored.ReadWord(vertex + 6));
        Assert.Equal((ushort)789, restored.ReadWord(face + 16));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(vertex + 14));
        Assert.Equal(state.ReadView(vertex, 16).ReadSnapshot(0, 16).Known.ToArray(), restored.ReadView(vertex, 16).ReadSnapshot(0, 16).Known.ToArray());
        Assert.Throws<NotSupportedException>(() => restored.ReadMesh(resources, MeshAAddress));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadMesh(MeshAAddress).Bytes.AsSpan().Slice(8)));
    }

    [Fact]
    public void Current_mesh_snapshot_preserves_unknown_vertex_W_but_requires_consumed_XYZ_and_faces()
    {
        NativeBattleMagmaBreath48Resources resources = Resources();
        NativeBattleMagmaBreath48State state = NativeBattleMagmaBreath48State.FromColdProcess().EnsureSources(resources);
        uint w = MeshAAddress + 14;
        state = state.WriteView(w, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
        NativeBattleMagmaBreath48State restored = JsonSerializer.Deserialize<NativeBattleMagmaBreath48State>(JsonSerializer.Serialize(state))!;
        NativeBattleStaticMeshResource current = restored.ReadMesh(resources, MeshAAddress);
        Assert.Equal(182u, BinaryPrimitives.ReadUInt32LittleEndian(current.Bytes.AsSpan().Slice(4)));
        Assert.Equal(resources.ReadMesh(MeshAAddress).Bytes.AsSpan().Slice(8, 6).ToArray(), current.Bytes.AsSpan().Slice(8, 6).ToArray());
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(w));
        NativeBattleMagmaBreath48State unknownX = restored.WriteView(MeshAAddress + 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 1), 1);
        Assert.Throws<NotSupportedException>(() => unknownX.ReadMesh(resources, MeshAAddress));
        NativeBattleMagmaBreath48State unknownFace = restored.WriteView(MeshAAddress + 8 + 182 * 8 + 31, NativeBattleEffectArena.Clear().MarkUnknown(0, 1), 1);
        Assert.Throws<NotSupportedException>(() => unknownFace.ReadMesh(resources, MeshAAddress));
    }

    [Fact]
    public void Unknown_imports_do_not_reseed_channels_or_current_sources_and_marker_is_required()
    {
        var state = NativeBattleMagmaBreath48State.FromUnknown();
        state.Validate();
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ChannelBase));
        Assert.Throws<NotSupportedException>(() => state.EnsureSources(Resources()));
        var json = JsonNode.Parse(JsonSerializer.Serialize(state))!.AsObject();
        Assert.True(json.Remove(nameof(NativeBattleMagmaBreath48State.SourcesLoaded)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleMagmaBreath48State>(json.ToJsonString()));
    }

    [Fact]
    public void Sparse_globals_exclude_gaps_COMMON_shared_view_and_all_six_task_banks()
    {
        var state = NativeBattleMagmaBreath48State.FromColdProcess();
        foreach (uint address in new uint[] { 0x26495C8, 0x26495D4, 0x26481BE, 0x2793E58 })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        foreach (var pool in Pools)
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(pool.Bank));
            Assert.Throws<InvalidDataException>(() => state.ReadWord(pool.Descriptor));
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(0x26495C8 - (int)BaseAddress, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Channels = state.Channels.Write(80, new byte[1]) }).Validate());
    }

    [Fact]
    public void Six_retained_banks_have_exact_bounds_and_can_be_revoked_to_active_owners()
    {
        var state = NativeBattleMagmaBreath48State.FromColdProcess();
        (state with { InactiveBanks = Pools.Select(_ => (NativeBattleEffectArena?)null).ToImmutableArray() }).Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            var pool = Pools[index];
            Assert.Equal(new byte[pool.Stride * pool.Capacity], state.InactiveBanks[index]!.Read(0, pool.Stride * pool.Capacity));
            Assert.Throws<InvalidDataException>(() => (state with { InactiveBanks = state.InactiveBanks.SetItem(index, NativeBattleEffectArena.Clear().MarkUnknown(pool.Stride * pool.Capacity, 1)) }).Validate());
        }
    }

    internal static NativeBattleMagmaBreath48Resources Resources()
    {
        byte[] data = new byte[StaticLength];
        uint[] sprites = [0x15ADC6C, 0x15ADE18, 0x15ADFC4, 0x15AE170, 0x15AE31C];
        int[] lengths = [428, 428, 428, 428, 480];
        var spriteResources = sprites.Select((address, index) =>
        {
            byte[] bytes = new byte[lengths[index]];
            int frames = index == 4 ? 18 : 16;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)frames);
            for (int phase = 0; phase <= frames; phase++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), 64);
            }
            return Resource(address, bytes);
        }).ToImmutableArray();
        var meshes = new[] { MeshAAddress, MeshBAddress }.Select(address =>
        {
            byte[] bytes = new byte[MeshLength];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 1464);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 182);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1464 + 24), 24);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1464 + 28 + 24 * 28), 168);
            bytes.CopyTo(data, checked((int)(address - StaticAddress)));
            return new NativeBattleStaticMeshResource(0, address, bytes.ToImmutableArray());
        }).ToImmutableArray();
        uint[] shapes = [0x15B252C, 0x15B309C, 0x15B3654, 0x15B3C0C, 0x15B41C4, 0x15B477C, 0x15B4D34, 0x15B52EC, 0x15B58A4, 0x15B5E5C];
        var shapeResources = shapes.Select(address =>
        {
            byte[] bytes = new byte[1464];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 1464);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 182);
            return Resource(address, bytes);
        }).ToImmutableArray();
        byte[] header = new byte[SourceLength];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 1);
        uint[][] directories = [[0x15B7320, 0x15B7478, 0x15B75D0, 0x15B7728, 0x15B7880, 0x15B79D8, 0x15B7B30], [.. sprites, sprites[0]], [MeshAAddress, MeshBAddress], shapes];
        for (int table = 0; table < directories.Length; table++)
        {
            for (int index = 0; index < directories[table].Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4 + table * 64 + index * 4), directories[table][index]);
            }
        }
        header.CopyTo(data, checked((int)(SourceAddress - StaticAddress)));
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 52);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        var resources = new NativeBattleMagmaBreath48Resources(new(header.ToImmutableArray()) { ResourceAddress = SourceAddress }, new(0, StaticAddress, 0, 0, data.ToImmutableArray()), spriteResources, meshes, shapeResources, new("FF8_EN:15B7D8C", camera.ToImmutableArray()), 0x641B8, [5, 10, 15, 20, 30, 40, 50, 0xFE320000, 50, 40, 30, 20, 15, 10, 5, 0xFF000000]);
        resources.Validate();
        return resources;

        NativeBattleModuleResource Resource(uint address, byte[] bytes)
        {
            bytes.CopyTo(data, checked((int)(address - StaticAddress)));
            return new(0, address, 0, 0, bytes.ToImmutableArray());
        }
    }
}
