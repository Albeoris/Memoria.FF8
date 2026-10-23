using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect83StateTests
{
    [Fact]
    public void Cold_process_retains_exact_sparse_owners_and_excludes_canonical_shared_memory()
    {
        NativeBattleEffect83State state = NativeBattleEffect83State.FromColdProcess();
        state.Validate();
        Assert.True(state.ColdSourceOrigin);
        Assert.Null(state.NodeData);
        Assert.Empty(state.OwnedModels);
        Assert.Equal(8, state.BankStorage.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.All(state.Read(address, length), value => Assert.Equal((byte)0, value));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x25C9F7A, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(RootBank, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ViewAddress, ViewLength));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ProjectedAddress + ProjectedLength, 1));
    }

    [Fact]
    public void Unknown_import_keeps_bytes_unknown_until_their_actual_width_writer()
    {
        NativeBattleEffect83State state = NativeBattleEffect83State.FromUnknown();
        Assert.False(state.ColdSourceOrigin);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(TargetAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(0, 1));
        NativeBattleEffectArena view = state.ReadView(TransformedAddress, 16);
        state = state.WriteView(TransformedAddress, view.Write(0, new byte[8]), 16);
        Assert.Equal(new byte[8], state.Read(TransformedAddress, 8));
        Assert.Throws<NotSupportedException>(() => state.Read(TransformedAddress + 8, 8));
        Assert.Throws<NotSupportedException>(() => state.Read(NodeSourceAddress, 4));
        Assert.Throws<InvalidDataException>(() => state.ReplaceNodeBank(new byte[NodeSourceLength].ToImmutableArray()));
    }

    [Fact]
    public void Physical_bank_transfers_are_exclusive_and_retain_payload_known_masks()
    {
        NativeBattleEffect83State state = NativeBattleEffect83State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(2).Write(12, [0xA7]);
        state = state.DetachBank(2);
        Assert.Null(state.BankStorage[2]);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(2));
        Assert.Throws<InvalidDataException>(() => state.DetachBank(2));
        state = state.AttachBank(2, bank);
        Assert.Equal((byte)0xA7, state.ReadBank(2).Read(12, 1)[0]);
        Assert.Throws<NotSupportedException>(() => state.ReadBank(2).Read(13, 1));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(2, bank));
        state.Validate();
    }

    [Fact]
    public void Json_preserves_current_owner_writes_sidecars_and_detached_banks()
    {
        NativeBattleEffect83State state = NativeBattleEffect83State.FromColdProcess().WriteWord(TargetAddress + 0x46, 110).WriteDword(DescriptorAddress + 4, TransformedAddress).DetachBank(7);
        state = state with { NodeData = new byte[NodeSourceLength].ToImmutableArray(), OwnedModels = state.OwnedModels.Add(0x25CF8D8, new(0x25CF8D8, ModelAddress, null)) };
        state = state.WriteWord(NodeSourceAddress + 4, 0xB278);
        string json = JsonSerializer.Serialize(state);
        NativeBattleEffect83State restored = JsonSerializer.Deserialize<NativeBattleEffect83State>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal((ushort)110, restored.ReadWord(TargetAddress + 0x46));
        Assert.Equal(TransformedAddress, restored.ReadDword(DescriptorAddress + 4));
        Assert.Equal((ushort)0xB278, restored.ReadWord(NodeSourceAddress + 4));
        Assert.Null(restored.BankStorage[7]);
        Assert.Equal(ModelAddress, restored.OwnedModels[0x25CF8D8].ResourceAddress);
    }
}
