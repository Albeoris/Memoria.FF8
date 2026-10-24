using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect227StateTests
{
    [Fact]
    public void Process_factory_initializes_only_exact_sparse_owners_and_root_bank()
    {
        NativeBattleEffect227State state = Assert.IsType<NativeBattleEffect227State>(NativeBattleEffectMemory.FromColdProcess().Effect227);
        state.Validate();
        Assert.Equal(2, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[16], state.ReadBank(0).Read(0, 16));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2508718, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x250876C, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Unknown_anchor_W_and_endpoint_W_remain_unknown_after_width_specific_JSON_writes()
    {
        NativeBattleEffect227State state = NativeBattleEffect227State.FromUnknown().Write(0x2508758, new byte[6]).Write(0x2508760, new byte[6]).WriteDword(0x2508704, 123);
        state = JsonSerializer.Deserialize<NativeBattleEffect227State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(new byte[6], state.Read(0x2508758, 6));
        Assert.Equal(new byte[6], state.Read(0x2508760, 6));
        Assert.Equal(123u, state.ReadDword(0x2508704));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(0x250875E));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(0x2508766));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x2508700));
    }

    [Fact]
    public void Physical_root_transfer_preserves_retained_WORD_and_unknown_suffix_through_JSON()
    {
        NativeBattleEffect227State state = NativeBattleEffect227State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(0).Write(12, BitConverter.GetBytes((ushort)0xBEEF));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state = JsonSerializer.Deserialize<NativeBattleEffect227State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(BitConverter.GetBytes((ushort)0xBEEF), state.ReadBank(0).Read(12, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(14, 2));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
    }
}
