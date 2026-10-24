using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65StateTests
{
    [Fact]
    public void Cold_owners_are_sparse_and_exclude_readonly_descriptor_aliases()
    {
        NativeBattleEffect65State state = NativeBattleEffect65State.FromColdProcess();
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges.Concat(LiteralRanges))
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[8], state.ReadChildQueue().Read(0, 8));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2544F74, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x133AC40, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x133ACAA, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ArenaAddress, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(ScaleAddress, 4));
    }

    [Fact]
    public void Unknown_import_does_not_reconstruct_literal_counters_or_retained_padding()
    {
        NativeBattleEffect65State state = NativeBattleEffect65State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => state.ReadWord(LiteralBase));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(CenterAddress + 6));
        state = state.WriteWord(LiteralBase, 0xFFFF).WriteWord(0x133ACAC, 0x8000);
        Assert.Equal((ushort)0xFFFF, state.ReadWord(LiteralBase));
        Assert.Equal((ushort)0x8000, state.ReadWord(0x133ACAC));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(LiteralBase + 2));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(0x133ACAE));
        state.Validate();
    }

    [Fact]
    public void Root_and_child_descriptor_ownership_transfer_exclusively_without_clearing_payload()
    {
        NativeBattleEffect65State state = NativeBattleEffect65State.FromUnknown();
        NativeBattleEffectArena root = state.ReadBank(0).Write(12, BitConverter.GetBytes((ushort)7));
        NativeBattleEffectArena queue = state.ReadChildQueue().Write(0, BitConverter.GetBytes(0x20DFAB8u));
        state = state.DetachBank(0).DetachChildQueue();
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        Assert.Throws<InvalidDataException>(() => state.ReadDword(ChildDescriptor));
        state = state.AttachBank(0, root).AttachChildQueue(queue);
        Assert.Equal((ushort)7, BitConverter.ToUInt16(state.ReadBank(0).Read(12, 2)));
        Assert.Equal(0x20DFAB8u, state.ReadDword(ChildDescriptor));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ChildDescriptor + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(14, 2));
        Assert.Throws<InvalidDataException>(() => state.AttachChildQueue(queue));
    }

    [Fact]
    public void Json_preserves_warm_literal_values_queue_transfer_and_private_seed()
    {
        NativeBattleEffect65State state = NativeBattleEffect65State.FromUnknown().WriteWord(LiteralBase + 4, 0xCAFE).WriteDword(RandomGlobal, 0x7FFF).WriteDword(SavedFlagsAddress + 24, 0x8123).DetachChildQueue();
        string json = JsonSerializer.Serialize(state);
        NativeBattleEffect65State restored = JsonSerializer.Deserialize<NativeBattleEffect65State>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Null(restored.ChildQueueStorage);
        Assert.Equal((ushort)0xCAFE, restored.ReadWord(LiteralBase + 4));
        Assert.Equal(0x7FFFu, restored.ReadDword(RandomGlobal));
        Assert.Equal(0x8123u, restored.ReadDword(SavedFlagsAddress + 24));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(LiteralBase + 6));
    }
}
