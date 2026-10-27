using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect289StateTests
{
    [Fact]
    public void Cold_scalar_and_root_bytes_are_distinct_from_initialized_source_W_and_COMMON()
    {
        NativeBattleEffect289State state = NativeBattleEffect289State.FromColdProcess();
        state.Validate();
        Assert.True(state.ColdSourceMeshW);
        Assert.Equal(new byte[12], state.Read(BaseAddress, 12));
        Assert.Equal(new byte[8], state.Read(CursorGlobal, 8));
        Assert.Equal(new byte[RootBankLength], state.RootBankStorage!.Read(0, RootBankLength));
        foreach (uint address in new[] { BaseAddress + 12, RootDescriptor, ParticleDescriptor, ProgrammeDescriptor, MainDescriptor, ArenaAddress })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        NativeBattleEffect289Resources resources = Resources();
        Assert.Equal((ushort)119, Word(state.ReadStatic(resources, MeshAddresses[0] + 14, 2), 0));
        Assert.Throws<NotSupportedException>(() => state.MeshWStorage.Read(0, 2));
        Assert.Equal(ScratchOffset + ScratchLength, FirstPacketOffset);
        Assert.Equal(544, 68 * 8);
        Assert.Equal(new[] { RootDescriptor, MainDescriptor, ParticleDescriptor, ProgrammeDescriptor }, Pools.Select(pool => pool.Descriptor).ToArray());
    }

    [Fact]
    public void Unknown_import_preserves_readonly_XYZ_but_W_requires_actual_current_writer()
    {
        NativeBattleEffect289Resources resources = Resources();
        NativeBattleEffect289State state = NativeBattleEffect289State.FromUnknown();
        state.Validate();
        Assert.False(state.ColdSourceMeshW);
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadStatic(resources, MeshAddresses[0] + 14, 2).Read(0, 2));
        Assert.Equal(resources.Meshes[0].Bytes.AsSpan().Slice(8, 6).ToArray(), state.ReadStatic(resources, MeshAddresses[0] + 8, 6).Read(0, 6));
        state = state.WriteMeshW(MeshAddresses[0], 0, -1234);
        Assert.Equal(unchecked((ushort)-1234), Word(state.ReadStatic(resources, MeshAddresses[0] + 14, 2), 0));
        Assert.Throws<NotSupportedException>(() => state.ReadStatic(resources, MeshAddresses[0] + 22, 2).Read(0, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(state, MeshAddresses[0]));
    }

    [Fact]
    public void Source_W_overlays_roundtrip_without_changing_static_bytes_or_original_origin()
    {
        NativeBattleEffect289Resources resources = Resources();
        NativeBattleEffect289State state = NativeBattleEffect289State.FromColdProcess().WriteMeshW(MeshAddresses[0], 0, 3456).WriteMeshW(MeshAddresses[1], 163, 4080) with { RootBankStorage = null };
        NativeBattleEffect289State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect289State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.True(restored.ColdSourceMeshW);
        Assert.Equal((ushort)3456, Word(restored.ReadStatic(resources, MeshAddresses[0] + 14, 2), 0));
        Assert.Equal((ushort)4080, Word(restored.ReadStatic(resources, MeshAddresses[1] + 14 + 163 * 8, 2), 0));
        Assert.Equal((ushort)119, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadMesh(MeshAddresses[0]).Bytes.AsSpan().Slice(14, 2)));
        Assert.Equal((ushort)0, Word(restored.ReadStatic(resources, MeshAddresses[0] + 22, 2), 0));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
    }

    [Fact]
    public void Mutable_domains_reject_gaps_wrong_sources_and_out_of_range_vertices()
    {
        NativeBattleEffect289State state = NativeBattleEffect289State.FromUnknown();
        Assert.Throws<InvalidDataException>(() => state.WriteMeshW(CameraAddress, 0, 0));
        Assert.Throws<InvalidDataException>(() => state.WriteMeshW(MeshAddresses[0], -1, 0));
        Assert.Throws<InvalidDataException>(() => state.WriteMeshW(MeshAddresses[0], MeshVertexCount, 0));
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(12, [0]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { MeshWStorage = state.MeshWStorage.Write(MeshWStorageLength, [0]) }).Validate());
    }

    private static NativeBattleEffect289Resources Resources()
    {
        ImmutableArray<NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) =>
        {
            byte[] bytes = new byte[MeshLengths[index]];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 8 + MeshVertexCount * 8);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), MeshVertexCount);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 119);
            return new NativeBattleStaticMeshResource(0, address, bytes.ToImmutableArray());
        }).ToImmutableArray();
        return new(new(MainProgrammeAddress, [0]), new(ChildProgrammeAddress, [0]), meshes, [], new("test", []), 0, new byte[8].ToImmutableArray(), new byte[LookupLength].ToImmutableArray());
    }

    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
}
