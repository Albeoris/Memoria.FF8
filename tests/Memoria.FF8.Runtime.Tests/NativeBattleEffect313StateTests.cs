using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect313StateTests
{
    [Fact]
    public void Process_factory_initializes_exact_nine_ranges_and_two_exclusive_banks()
    {
        NativeBattleEffect313State state = Assert.IsType<NativeBattleEffect313State>(NativeBattleEffectMemory.FromColdProcess().Effect313);
        state.Validate();
        Assert.Equal(9, OwnedRanges.Length);
        Assert.Equal(3, SavedActorCount);
        Assert.Equal(80, RecordCount);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[32], state.ReadBank(0).Read(0, 32));
        Assert.Equal(new byte[3600], state.ReadBank(1).Read(0, 3600));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ChildDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(RootBank, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ChildBank, 4));
    }

    [Fact]
    public void Saved_party_rows_preserve_unknown_holes_and_do_not_alias_globals()
    {
        NativeBattleEffect313State state = NativeBattleEffect313State.FromUnknown();
        for (int actor = 0; actor < 3; actor++)
        {
            uint row = SavedActorsAddress + (uint)(actor * SavedActorStride);
            state = state.WriteWord(row, (ushort)(0x1000 + actor)).WriteDword(row + 4, (uint)(100 + actor));
            Assert.Throws<InvalidDataException>(() => state.Read(row + 2, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadDword(row + 8));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(SavedActorsAddress + 3 * SavedActorStride, 2));
        Assert.Throws<InvalidDataException>(() => state.Read(InitializerGlobal + 4, 4));
        state.Validate();
    }

    [Fact]
    public void Current_scalars_and_last_record_knownness_survive_JSON()
    {
        uint last = RecordsAddress + (RecordCount - 1) * RecordStride;
        NativeBattleEffect313State state = NativeBattleEffect313State.FromUnknown().WriteDword(last, 1).WriteWord(last + 6, 512).WriteDword(CursorGlobal, 123);
        state = JsonSerializer.Deserialize<NativeBattleEffect313State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(1u, state.ReadDword(last));
        Assert.Equal((ushort)512, state.ReadWord(last + 6));
        Assert.Equal(123u, state.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(last + 14));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ArenaGlobal));
    }

    [Fact]
    public void Bank_transfers_keep_payload_knownness_exclusive_across_JSON()
    {
        NativeBattleEffect313State state = NativeBattleEffect313State.FromUnknown();
        NativeBattleEffectArena root = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xABCD));
        NativeBattleEffectArena child = state.ReadBank(1).Write(3596, BitConverter.GetBytes(456u));
        state = state.DetachBank(0).DetachBank(1);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = JsonSerializer.Deserialize<NativeBattleEffect313State>(JsonSerializer.Serialize(state))!;
        state = state.AttachBank(0, root).AttachBank(1, child);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(1, child));
        Assert.Equal((ushort)0xABCD, BitConverter.ToUInt16(state.ReadBank(0).Read(14, 2)));
        Assert.Equal(456u, BitConverter.ToUInt32(state.ReadBank(1).Read(3596, 4)));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(1).Read(0, 4));
        state.Validate();
    }
}
