using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79StateTests
{
    [Fact]
    public void Cold_owner_is_sparse_and_excludes_shared_buffers_and_fixed_banks()
    {
        NativeBattleEffect79State state = NativeBattleEffect79State.FromColdProcess();
        state.Validate();
        Assert.True(state.ColdSourceOrigin);
        Assert.Empty(state.NodeBanks);
        Assert.Null(state.ProgramData);
        Assert.Equal(10, state.FixedBanks.Count);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.All(state.Read(address, length), value => Assert.Equal((byte)0, value));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x25DF341, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(RootBank, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(ViewAddress, 32));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(TransformedAddress + TransformedLength, 1));
    }

    [Fact]
    public void Unknown_rows_preserve_the_native_sixteen_byte_partial_store_boundary()
    {
        NativeBattleEffect79State state = NativeBattleEffect79State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => state.ReadWord(TargetAddress));
        NativeBattleEffectArena row = state.ReadView(TransformedAddress, 16).Write(0, new byte[8]);
        state = state.WriteView(TransformedAddress, row, 16);
        Assert.Equal(new byte[8], state.Read(TransformedAddress, 8));
        Assert.Throws<NotSupportedException>(() => state.Read(TransformedAddress + 8, 8));
        state = state.WriteDword(TransformedAddress + 8, 0x12345678).WriteDword(TransformedAddress + 12, 512);
        Assert.Equal(0x12345678u, state.ReadDword(TransformedAddress + 8));
        Assert.Equal(512u, state.ReadDword(TransformedAddress + 12));
        state.Validate();
    }

    [Fact]
    public void Fixed_bank_index_and_address_views_transfer_one_physical_owner()
    {
        NativeBattleEffect79State state = NativeBattleEffect79State.FromUnknown();
        uint address = Pools[5].Bank;
        NativeBattleEffectArena bank = state.ReadBank(address).Write(12, [0xA6]);
        state = state.DetachBank(5);
        Assert.Null(state.FixedBanks[address]);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(address));
        Assert.Throws<InvalidDataException>(() => state.DetachBank(address));
        state = state.AttachBank(address, bank);
        Assert.Equal((byte)0xA6, state.ReadBank(5).Read(12, 1)[0]);
        Assert.Throws<NotSupportedException>(() => state.ReadBank(5).Read(13, 1));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(5, bank));
        state.Validate();
    }

    [Fact]
    public void Adjacent_scalar_and_control_writes_share_one_storage_owner_without_owning_gaps()
    {
        NativeBattleEffect79State state = NativeBattleEffect79State.FromColdProcess().WriteDword(0x25EB3BC, ControlAddress).WriteWord(ControlAddress, 7);
        Assert.Equal(ControlAddress, state.ReadDword(0x25EB3BC));
        Assert.Equal((ushort)7, state.ReadWord(ControlAddress));
        Assert.Equal(6, state.Read(0x25EB3BC, 6).Length);
        state.Validate();
        Assert.Throws<InvalidDataException>(() => state.Read(0x25DF340, 3));
        Assert.Throws<InvalidDataException>(() => state.Read(MorphAddress + MorphLength, 4));
    }

    [Fact]
    public void Json_preserves_current_script_node_data_morph_knownness_and_detached_banks()
    {
        NativeBattleEffect79State state = NativeBattleEffect79State.FromUnknown().DetachBank(0);
        state = state with { ProgramData = new byte[ScriptLength].ToImmutableArray(), NodeBanks = state.NodeBanks.Add(0x176A958, new byte[6916].ToImmutableArray()) };
        state = state.WriteDword(ScriptAddress + 12, 0x19F1018).WriteDword(0x176A958, 1).WriteWord(MorphAddress, 0xA55A);
        string json = JsonSerializer.Serialize(state);
        NativeBattleEffect79State restored = JsonSerializer.Deserialize<NativeBattleEffect79State>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(0x19F1018u, restored.ReadDword(ScriptAddress + 12));
        Assert.Equal(1u, restored.ReadDword(0x176A958));
        Assert.Equal((ushort)0xA55A, restored.ReadWord(MorphAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MorphAddress + 2));
        Assert.Null(restored.FixedBanks[RootBank]);
    }
}
