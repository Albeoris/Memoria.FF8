using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect320StateTests
{
    [Fact]
    public void Cold_and_unknown_states_keep_only_exact_sparse_owners()
    {
        NativeBattleEffect320State cold = NativeBattleEffect320State.FromColdProcess();
        cold.Validate();
        Assert.False(cold.MutableAnchorSourceBound);
        Assert.Equal(22, OwnedRanges.Sum(range => range.Length));
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], cold.Read(address, length));
        }
        NativeBattleEffect320State unknown = NativeBattleEffect320State.FromUnknown();
        unknown.Validate();
        Assert.Null(unknown.MutableAnchorSourceBound);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Throws<NotSupportedException>(() => unknown.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => cold.Read(MutableAnchorAddress + 2, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(InitializerGlobal - 1, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(ArenaGlobal + 4, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => cold.Read(RootBank, 4));
        Assert.Throws<InvalidDataException>(() => cold.Read(ParticleDescriptor + CommonQueueLength, 4));
        Assert.Throws<InvalidDataException>(() => cold.Read(MutableAnchorAddress, checked((int)(InitializerGlobal - MutableAnchorAddress))));
    }

    [Fact]
    public void Sparse_WORD_and_DWORD_writes_preserve_unknown_neighbors_and_bound_source_through_JSON()
    {
        NativeBattleEffect320State state = NativeBattleEffect320State.FromUnknown().WriteWord(MutableAnchorAddress, unchecked((ushort)-100)).WriteDword(CursorGlobal, 0x12345678).WriteDword(ArenaGlobal, 0x87654321) with { MutableAnchorSourceBound = true };
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect320State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.True(state.MutableAnchorSourceBound);
        Assert.Equal(unchecked((ushort)-100), state.ReadWord(MutableAnchorAddress));
        Assert.Equal(0x12345678u, state.ReadDword(CursorGlobal));
        Assert.Equal(0x87654321u, state.ReadDword(ArenaGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(InitializerGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ParametersGlobal));
        Assert.Throws<InvalidDataException>(() => state.WriteWord(MutableAnchorAddress + 1, 100));
    }

    [Fact]
    public void Root_bank_and_each_COMMON_queue_transfer_exclusively_without_clearing_retained_bytes()
    {
        NativeBattleEffect320State state = NativeBattleEffect320State.FromUnknown();
        NativeBattleEffectArena root = state.ReadBank(RootBank).Write(14, BitConverter.GetBytes((ushort)0xBEEF));
        state = state.DetachBank(RootBank);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(RootBank));
        state = state.AttachBank(RootBank, root);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(RootBank, root));
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            NativeBattleEffectArena queue = state.ReadCommonQueue(shape.Descriptor).Write(0, BitConverter.GetBytes(0xCAFEBABEu));
            state = state.DetachCommonQueue(shape.Descriptor);
            Assert.Throws<InvalidDataException>(() => state.ReadDword(shape.Descriptor));
            state = state.AttachCommonQueue(shape.Descriptor, queue);
            Assert.Throws<InvalidDataException>(() => state.AttachCommonQueue(shape.Descriptor, queue));
        }
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect320State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal(BitConverter.GetBytes((ushort)0xBEEF), state.ReadBank(RootBank).Read(14, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(RootBank).Read(16, 4));
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            Assert.Equal(0xCAFEBABEu, state.ReadDword(shape.Descriptor));
            Assert.Throws<NotSupportedException>(() => state.ReadDword(shape.Descriptor + 4));
        }
    }

    [Fact]
    public void Invalid_sparse_storage_and_pool_extents_are_rejected()
    {
        NativeBattleEffect320State state = NativeBattleEffect320State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => (state with { Storage = NativeBattleEffectArena.Clear().Write(StorageLength, [1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => state.DetachBank(RootBank).AttachBank(RootBank, NativeBattleEffectArena.Clear().Write(20, [1])));
        Assert.Throws<InvalidDataException>(() => state.DetachCommonQueue(MainDescriptor).AttachCommonQueue(MainDescriptor, NativeBattleEffectArena.Clear().Write(8, [1])));
        Assert.Throws<InvalidDataException>(() => (state with { CommonQueueStorage = ImmutableDictionary<uint, NativeBattleEffectArena?>.Empty }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { FixedBanks = ImmutableDictionary<uint, NativeBattleEffectArena?>.Empty }).Validate());
    }

    [Fact]
    public void JSON_requires_explicit_mutable_source_binding_history()
    {
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect320State.FromColdProcess()));
        using JsonDocument parsed = JsonDocument.Parse(json);
        Dictionary<string, JsonElement> fields = parsed.RootElement.EnumerateObject().Where(property => property.Name != nameof(NativeBattleEffect320State.MutableAnchorSourceBound)).ToDictionary(property => property.Name, property => property.Value.Clone());
        Assert.Throws<JsonException>(() => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect320State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fields)))));
        NativeBattleEffect320State unknown = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect320State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect320State.FromUnknown())))!);
        Assert.Null(unknown.MutableAnchorSourceBound);
    }
}
