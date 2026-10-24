using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect213StateTests
{
    [Fact]
    public void Process_factory_owns_only_exact_scalars_and_root_bank()
    {
        NativeBattleEffect213State state = Assert.IsType<NativeBattleEffect213State>(NativeBattleEffectMemory.FromColdProcess().Effect213);
        state.Validate();
        Assert.True(state.MutableStreamPending);
        Assert.Equal(3, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(TranslationAddress - 1, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(TranslationAddress + 12, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(MutableStreamAddress, 1));
    }

    [Fact]
    public void Unknown_scalars_and_exclusive_bank_ownership_survive_JSON()
    {
        NativeBattleEffect213State state = NativeBattleEffect213State.FromUnknown().WriteDword(CursorGlobal, 123);
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xCAFE));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(RootBank, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state = JsonSerializer.Deserialize<NativeBattleEffect213State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Null(state.MutableStreamPending);
        Assert.Equal(123u, state.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(0).Read(14, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(16, 2));
    }

    [Fact]
    public void Cold_binding_is_once_and_warm_current_stream_survives_JSON()
    {
        NativeBattleEffect213Resources resources = SyntheticResources();
        var bound = NativeBattleEffect213State.FromColdProcess().BindSources(NativeBattleFileStaging.Unknown(), resources);
        Assert.False(bound.State.MutableStreamPending);
        Assert.Equal(resources.OriginalMutableVertices.ToArray(), bound.Staging.Read(MutableStreamAddress, MutableStreamLength));
        NativeBattleFileStaging warm = bound.Staging.Write(MutableStreamAddress + 6, BitConverter.GetBytes((ushort)0xBEEF));
        NativeBattleEffect213State state = JsonSerializer.Deserialize<NativeBattleEffect213State>(JsonSerializer.Serialize(bound.State))!;
        warm = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(warm))!;
        var rebound = state.BindSources(warm, resources);
        Assert.Equal(BitConverter.GetBytes((ushort)0xBEEF), rebound.Staging.Read(MutableStreamAddress + 6, 2));
        Assert.Throws<NotSupportedException>(() => state.BindSources(NativeBattleFileStaging.Unknown(), resources));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MutableStreamAddress - 8, 16));
        Assert.Equal(rebound.Staging.Read(MutableStreamAddress, 8), resources.ReadStatic(rebound.Staging, MutableStreamAddress, 8));
        Assert.Equal(rebound.Staging.Read(MutableStreamAddress, MutableStreamLength), resources.ReadCurrentMesh(rebound.Staging, 0x117ADD8).Bytes.AsSpan(8, MutableStreamLength).ToArray());
    }

    [Fact]
    public void Binding_preserves_complete_existing_owner_and_rejects_partial_or_unknown_origin()
    {
        NativeBattleEffect213Resources resources = SyntheticResources();
        byte[] current = Enumerable.Repeat((byte)0xA5, MutableStreamLength).ToArray();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(MutableStreamAddress, current);
        var bound = NativeBattleEffect213State.FromColdProcess().BindSources(staging, resources);
        Assert.Equal(current, bound.Staging.Read(MutableStreamAddress, MutableStreamLength));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect213State.FromColdProcess().BindSources(NativeBattleFileStaging.Unknown().Write(MutableStreamAddress, [1]), resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect213State.FromUnknown().BindSources(staging, resources));
        NativeBattleEffect213State legacy = JsonSerializer.Deserialize<NativeBattleEffect213State>(JsonSerializer.Serialize(bound.State).Replace("\"MutableStreamPending\":false", "\"MutableStreamPending\":null"))!;
        Assert.Throws<NotSupportedException>(() => legacy.BindSources(staging, resources));
    }

    private static NativeBattleEffect213Resources SyntheticResources()
    {
        byte[] bytes = new byte[8 + MutableStreamLength];
        for (int index = 8; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(index * 17));
        }
        NativeBattleStaticMeshResource mesh = new(0, 0x117ADD8, bytes.ToImmutableArray());
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(mesh.Address, mesh.Bytes));
    }
}
