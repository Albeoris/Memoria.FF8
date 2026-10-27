using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBladeShot12StateTests
{
    [Fact]
    public void Cold_state_needs_no_resources_but_mutable_PE_is_unavailable_until_explicit_binding()
    {
        NativeBattleBladeShot12State cold = NativeBattleBladeShot12State.FromColdProcess();
        cold.Validate();
        Assert.Equal(false, cold.VertexSourcesLoaded);
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Equal(new byte[8], cold.Read(SourcePointGlobal, 8));
        Assert.Equal(new byte[4], cold.ReadPacket(PacketBase + PacketBankBytes, 4));
        Assert.Equal(new byte[100], cold.RootBankStorage!.Read(0, 100));
        Assert.Equal(new byte[512], cold.ChildABankStorage!.Read(0, 512));
        Assert.Equal(new byte[3328], cold.ChildBBankStorage!.Read(0, 3328));
        Assert.Throws<NotSupportedException>(() => cold.ReadWord(MutableVerticesA));
        Assert.Throws<NotSupportedException>(() => cold.WriteWord(MutableVerticesB, 1));
        Assert.False(cold.MutableVertices.ReadSnapshot(6, 2).Known.All(value => value));
    }

    [Fact]
    public void First_binding_seeds_both_complete_PE_fronts_including_W_and_changes_no_other_owner()
    {
        NativeBattleBladeShot12State cold = NativeBattleBladeShot12State.FromColdProcess();
        NativeBattleBladeShot12Resources resources = Seeds(0x1234);
        NativeBattleBladeShot12State loaded = cold.EnsureVertexSources(resources);
        loaded.Validate();
        Assert.Equal(true, loaded.VertexSourcesLoaded);
        Assert.Equal(resources.MeshA.Bytes.AsSpan().Slice(8, MutableVerticesLength).ToArray(), loaded.Read(MutableVerticesA, MutableVerticesLength));
        Assert.Equal(resources.MeshB.Bytes.AsSpan().Slice(8, MutableVerticesLength).ToArray(), loaded.Read(MutableVerticesB, MutableVerticesLength));
        Assert.Equal((ushort)0x1234, loaded.ReadWord(MutableVerticesA + 6));
        Assert.Same(cold.Globals, loaded.Globals);
        Assert.Same(cold.Packets, loaded.Packets);
        Assert.Same(cold.RootBankStorage, loaded.RootBankStorage);
        Assert.Same(cold.ChildABankStorage, loaded.ChildABankStorage);
        Assert.Same(cold.ChildBBankStorage, loaded.ChildBBankStorage);
    }

    [Fact]
    public void Warm_binding_retains_modified_XYZ_W_and_unknown_masks_through_JSON_without_reseeding()
    {
        NativeBattleBladeShot12State warm = NativeBattleBladeShot12State.FromColdProcess().EnsureVertexSources(Seeds(7)).WriteWord(MutableVerticesA, 0xABCD).WriteWord(MutableVerticesA + 6, 0x9876).WriteView(MutableVerticesB + 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 8).WriteDword(PacketBase + 4, 0x12345678);
        Assert.Same(warm, warm.EnsureVertexSources(Seeds(0xFFFF)));
        NativeBattleBladeShot12State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBladeShot12State>(NativeBattleTestSnapshots.Json(warm))!;
        restored.Validate();
        Assert.Same(restored, restored.EnsureVertexSources(Seeds(0)));
        Assert.Equal((ushort)0xABCD, restored.ReadWord(MutableVerticesA));
        Assert.Equal((ushort)0x9876, restored.ReadWord(MutableVerticesA + 6));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MutableVerticesB + 14));
        Assert.Equal(0x12345678u, restored.ReadDword(PacketBase + 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(warm), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Imported_unknown_or_missing_initialization_marker_fails_without_guessing_initial_PE()
    {
        NativeBattleBladeShot12State unknown = NativeBattleBladeShot12State.FromUnknown();
        unknown.Validate();
        Assert.Null(unknown.VertexSourcesLoaded);
        Assert.Throws<NotSupportedException>(() => unknown.EnsureVertexSources(Seeds(0)));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadPacket(PacketBase, 4));
        JsonObject json = JsonNode.Parse(NativeBattleTestSnapshots.Json(unknown))!.AsObject();
        Assert.True(json.Remove(nameof(NativeBattleBladeShot12State.VertexSourcesLoaded)));
        Assert.Throws<JsonException>(() => NativeBattleTestSnapshots.RestoreJson<NativeBattleBladeShot12State>(json.ToJsonString()));
    }

    [Fact]
    public void Sparse_ranges_exclude_three_task_banks_descriptors_PE_gaps_and_COMMON_aliases()
    {
        NativeBattleBladeShot12State loaded = NativeBattleBladeShot12State.FromColdProcess().EnsureVertexSources(Seeds(0));
        foreach (uint address in new[] { RootBank, ChildABank, ChildBBank, RootDescriptor, ChildADescriptor, ChildBDescriptor, CursorGlobal + 4, MutableVerticesA + MutableVerticesLength, MeshAAddress, 0x20DFAB8u })
        {
            Assert.Throws<InvalidDataException>(() => loaded.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => loaded.ReadPacket(CursorGlobal, 4));
        Assert.Throws<InvalidDataException>(() => (loaded with { Globals = loaded.Globals.Write(checked((int)(PacketBase - BaseAddress)), new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (loaded with { MutableVertices = loaded.MutableVertices.Write(MutableVerticesLength, new byte[1]) }).Validate());
    }

    [Fact]
    public void Active_three_pool_transfer_revokes_retained_owners_and_bounds_each_actual_stride()
    {
        NativeBattleBladeShot12State state = NativeBattleBladeShot12State.FromColdProcess();
        (state with { RootBankStorage = null, ChildABankStorage = null, ChildBBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildABankStorage = state.ChildABankStorage!.MarkUnknown(ChildABankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBBankStorage = state.ChildBBankStorage!.Write(ChildBBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Packets = state.Packets.Write(PacketBytes, new byte[1]) }).Validate());
    }

    private static NativeBattleBladeShot12Resources Seeds(ushort w)
    {
        return new(MeshA: Mesh(MeshAAddress), MeshB: Mesh(MeshBAddress), MeshC: null!, MeshD: null!, MeshE: null!, SpriteA: null!, SpriteB: null!, SpriteC: null!, SpriteD: null!, SpriteE: null!, SourceA0: null!, SourceA1: null!, SourceB0: null!, SourceB1: null!, Camera: null!, OpeningSound: 0, EndingSound: 0);

        NativeBattleStaticMeshResource Mesh(uint address)
        {
            byte[] bytes = new byte[0x5D0];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, MorphSourceLength);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), MutableVertexCount);
            for (int vertex = 0; vertex < MutableVertexCount; vertex++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + vertex * 8), unchecked((ushort)(vertex * 13)));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14 + vertex * 8), w);
            }
            return new(0, address, bytes.ToImmutableArray());
        }
    }
}
