using System.Text.Json;
using System.Text.Json.Nodes;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect31StateTests
{
    [Fact]
    public void Sparse_storage_excludes_pools_descriptors_and_unobserved_padding()
    {
        NativeBattleEffect31State state = NativeBattleEffect31State.FromColdProcess();
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        foreach (NativeBattleCureFamilyPool pool in Pools)
        {
            Assert.Throws<InvalidDataException>(() => state.Read(pool.Descriptor, 1));
            Assert.Throws<InvalidDataException>(() => state.Read(pool.Bank, 1));
        }
        foreach (uint gap in new uint[] { 0x26CDD96, 0x26D10A2, 0x26D2DB0, 0x26D2DBA, 0x26D33CA, 0x26D33E4 })
        {
            Assert.Throws<InvalidDataException>(() => state.Read(gap, 1));
        }
    }

    [Fact]
    public void Word_stores_and_morph_writes_preserve_unknown_neighbors_through_json()
    {
        NativeBattleEffect31State state = NativeBattleEffect31State.FromUnknown().WriteWord(0x26D2DA8, 0).WriteDword(MorphScratch, 0xABCD1234);
        state = JsonSerializer.Deserialize<NativeBattleEffect31State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal((ushort)0, state.ReadWord(0x26D2DA8));
        Assert.Equal(0xABCD1234u, state.ReadDword(MorphScratch));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(0x26D2DAA));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(MorphScratch + 4));
        Assert.False(state.ColdSourceOrigin);
    }

    [Fact]
    public void Seven_banks_and_queues_transfer_exclusively_with_their_retained_knownness()
    {
        NativeBattleEffect31State state = NativeBattleEffect31State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool pool = Pools[index];
            NativeBattleEffectArena bank = state.ReadBank(index).Write(12, [0xA5]);
            NativeBattleEffectArena queue = state.ReadQueue(pool.Descriptor).Write(0, BitConverter.GetBytes(pool.Bank));
            state = state.DetachBank(index).DetachQueue(pool.Descriptor);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(pool.Bank));
            Assert.Throws<InvalidDataException>(() => state.ReadQueue(pool.Descriptor));
            state = state.AttachBank(index, bank).AttachQueue(pool.Descriptor, queue);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
            Assert.Throws<InvalidDataException>(() => state.AttachQueue(pool.Descriptor, queue));
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect31State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal((byte)0xA5, state.ReadBank(index).Read(12, 1)[0]);
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(13, 1));
            Assert.Equal(Pools[index].Bank, BitConverter.ToUInt32(state.ReadQueue(Pools[index].Descriptor).Read(0, 4)));
            Assert.Throws<NotSupportedException>(() => state.ReadQueue(Pools[index].Descriptor).Read(4, 4));
        }
    }

    [Fact]
    public void Older_json_cannot_infer_current_memory_or_source_origin()
    {
        foreach (string property in new[] { "Storage", "FixedBanks", "FixedQueues", "ColdSourceOrigin", "StaticSourcesBound" })
        {
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(NativeBattleEffect31State.FromColdProcess()))!.AsObject();
            Assert.True(json.Remove(property));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleEffect31State>(json.ToJsonString()));
        }
    }
}
