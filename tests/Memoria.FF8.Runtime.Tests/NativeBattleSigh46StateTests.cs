using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Memoria.FF8.Runtime.NativeBattleSigh46Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSigh46StateTests
{
    [Fact]
    public void Cold_sources_need_explicit_binding_and_only_exact_loader_channels_are_admitted()
    {
        var cold = NativeBattleSigh46State.FromColdProcess();
        cold.Validate();
        Assert.False(cold.SourcesLoaded);
        Assert.Equal(92, ChannelRegions.Length);
        Assert.Equal(3013, ChannelRegions.Sum(region => region.Length));
        foreach (var region in ChannelRegions)
        {
            Assert.Equal(new byte[region.Length], cold.Read(region.Address, region.Length));
        }
        Assert.Throws<NotSupportedException>(() => cold.ReadWord(SourceAddress));
        Assert.Throws<NotSupportedException>(() => cold.ReadWord(MeshAAddress));
        Assert.Throws<InvalidDataException>(() => cold.ReadWord(0x26543AA));
        Assert.Throws<InvalidDataException>(() => cold.ReadWord(0x2652790));
        Assert.Throws<InvalidDataException>(() => cold.ReadWord(0x265279C));
    }

    [Fact]
    public void Current_material_DWORDs_retain_all_other_mesh_bytes_and_warm_seeds_are_never_replayed()
    {
        var resources = Resources();
        var cold = NativeBattleSigh46State.FromColdProcess();
        var current = cold.EnsureSources(resources);
        Assert.Same(cold.Globals, current.Globals);
        Assert.Same(cold.Channels, current.Channels);
        uint firstFace = MeshAAddress + 624 + 32;
        int[] lanes = [0, 24, 28, 32];
        foreach (int lane in lanes)
        {
            current = current.WriteDword(firstFace + (uint)lane, 0x3E123456u + (uint)lane);
        }
        byte[] expected = resources.ReadMesh(MeshAAddress).Bytes.ToArray();
        foreach (int lane in lanes)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(656 + lane), 0x3E123456u + (uint)lane);
        }
        Assert.Equal(expected, current.ReadMesh(resources, MeshAAddress).Bytes.ToArray());
        Assert.Equal(resources.ReadMesh(MeshBAddress).Bytes.ToArray(), current.Read(MeshBAddress, MeshLength));
        Assert.Same(current, current.EnsureSources(resources));
        var restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleSigh46State>(SnapshotJson(current))!);
        restored.Validate();
        Assert.Same(restored, restored.EnsureSources(resources));
        Assert.Equal(expected, restored.ReadMesh(resources, MeshAAddress).Bytes.ToArray());
    }

    [Fact]
    public void Imported_unknown_channel_bytes_and_initialization_marker_are_not_invented()
    {
        var unknown = NativeBattleSigh46State.FromUnknown();
        unknown.Validate();
        Assert.Null(unknown.SourcesLoaded);
        Assert.Throws<NotSupportedException>(() => unknown.EnsureSources(Resources()));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(ChannelBase));
        var json = JsonNode.Parse(SnapshotJson(unknown))!.AsObject();
        Assert.True(json.Remove(nameof(NativeBattleSigh46State.SourcesLoaded)));
        Assert.Throws<JsonException>(() => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleSigh46State>(json.ToJsonString())));
    }

    [Fact]
    public void Unread_vertex_W_remains_unknown_but_current_XYZ_material_and_UV_reads_are_strict()
    {
        var resources = Resources();
        var state = NativeBattleSigh46State.FromColdProcess().EnsureSources(resources);
        var wUnknown = state.WriteView(MeshAAddress + 14, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
        Assert.Throws<NotSupportedException>(() => wUnknown.ReadWord(MeshAAddress + 14));
        Assert.Equal(MeshLength, wUnknown.ReadMesh(resources, MeshAAddress).Bytes.Length);
        var xyzUnknown = state.WriteView(MeshAAddress + 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
        Assert.Throws<NotSupportedException>(() => xyzUnknown.ReadMesh(resources, MeshAAddress));
        var materialUnknown = state.WriteView(MeshAAddress + 656, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 4);
        Assert.Throws<NotSupportedException>(() => materialUnknown.ReadMesh(resources, MeshAAddress));
    }

    [Fact]
    public void Six_bank_bounds_and_sparse_globals_exclude_descriptors_COMMON_and_shared_scratch()
    {
        var state = NativeBattleSigh46State.FromColdProcess();
        (state with { InactiveBanks = Pools.Select(_ => (NativeBattleEffectArena?)null).ToImmutableArray() }).Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            var pool = Pools[index];
            Assert.Throws<InvalidDataException>(() => state.ReadWord(pool.Bank));
            Assert.Throws<InvalidDataException>(() => state.ReadWord(pool.Descriptor));
            Assert.Throws<InvalidDataException>(() => (state with { InactiveBanks = state.InactiveBanks.SetItem(index, state.InactiveBanks[index]!.Write(pool.Stride * pool.Capacity, new byte[1])) }).Validate());
        }
        Assert.Throws<InvalidDataException>(() => state.ReadWord(0x2793E58));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(0x20DFAB8));
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(0x2652790 - (int)BaseAddress, new byte[1]) }).Validate());
    }

    internal static NativeBattleSigh46Resources Resources()
    {
        byte[] data = new byte[StaticLength];
        uint[] spriteAddresses = [0x15B82A4, 0x15B8438, 0x15B85CC, 0x15B8830, 0x15B8A94, 0x15B8BA4];
        int[] spriteLengths = [404, 404, 612, 612, 272, 428];
        int[] spriteFrames = [15, 15, 23, 23, 10, 16];
        var sprites = spriteAddresses.Select((address, index) =>
        {
            byte[] bytes = new byte[spriteLengths[index]];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)spriteFrames[index]);
            for (int frame = 0; frame <= spriteFrames[index]; frame++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), 64);
            }
            return Resource(address, bytes);
        }).ToImmutableArray();
        var meshes = new[] { MeshAAddress, MeshBAddress }.Select(address =>
        {
            byte[] bytes = new byte[MeshLength];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 624);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 77);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(652), 60);
            bytes.CopyTo(data, checked((int)(address - StaticAddress)));
            return new NativeBattleStaticMeshResource(0, address, bytes.ToImmutableArray());
        }).ToImmutableArray();
        var streams = Enumerable.Range(0, 34).Select(index =>
        {
            byte[] bytes = new byte[960];
            for (int face = 0; face < 60; face++)
            {
                bytes[face * 16 + 3] = 0x3E;
            }
            return Resource((index < 17 ? 0x15B8D50u : 0x15BCD54u) + (uint)(index % 17 * 960), bytes);
        }).ToImmutableArray();
        var tables = new[] { ColorTableAAddress, ColorTableBAddress }.Select((address, table) =>
        {
            byte[] bytes = new byte[68];
            for (int row = 0; row < 17; row++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(row * 4), streams[table * 17 + row].Address);
            }
            return Resource(address, bytes);
        }).ToImmutableArray();
        byte[] header = new byte[SourceLength];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 1);
        uint[] nodes = [0x15C2A30, 0x15C2B88, 0x15C2CE0, 0x15C2E38, 0x15C2F90, 0x15C30E8, 0x15C3240];
        uint[] sourceSprites = [0x15B85CC, 0x15B8830, 0x15B82A4, 0x15B8438, 0x15B8BA4, 0x15B8BA4, 0x15B8A94];
        for (int index = 0; index < 7; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4 + index * 4), nodes[index]);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(68 + index * 4), sourceSprites[index]);
        }
        Resource(SourceAddress, header);
        short[] delays = [2, 3, 4, 5, 4, 3, 2, 1, 0];
        short[] fade = [512, 1024, 1536, 1792, 2048, 2304, 2560, 2816, 3072, 3328, 3456, 3584, 3712, 3840, 3968, 4096];
        Resource(DelayAddress, delays.SelectMany(BitConverter.GetBytes).ToArray());
        Resource(FadeAddress, fade.SelectMany(BitConverter.GetBytes).ToArray());
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 24);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        Resource(CameraAddress, camera);
        var resources = new NativeBattleSigh46Resources(new(header.ToImmutableArray()) { ResourceAddress = SourceAddress }, new(0, StaticAddress, 0, 0, data.ToImmutableArray()), sprites, meshes, tables, streams, delays.ToImmutableArray(), fade.ToImmutableArray(), new("FF8_EN:15C349C", camera.ToImmutableArray()), 0x61AB4);
        resources.Validate();
        return resources;

        NativeBattleModuleResource Resource(uint address, byte[] bytes)
        {
            bytes.CopyTo(data, checked((int)(address - StaticAddress)));
            return new(0, address, 0, 0, bytes.ToImmutableArray());
        }
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
