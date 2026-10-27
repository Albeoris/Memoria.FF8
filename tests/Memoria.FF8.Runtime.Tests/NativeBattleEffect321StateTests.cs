using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect321StateTests
{
    [Fact]
    public void Cold_factory_owns_exact_sparse_rows_and_two_exclusive_native_banks()
    {
        NativeBattleEffect321State state = Assert.IsType<NativeBattleEffect321State>(NativeBattleEffectMemory.FromColdProcess().Effect321);
        state.Validate();
        Assert.Equal(20, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[32], state.ReadBank(0).Read(0, 32));
        Assert.Equal(new byte[3600], state.ReadBank(1).Read(0, 3600));
        foreach (uint gap in new uint[] { 0x221BC6C, 0x221BC90, 0x221D422, 0x221D434, 0x2239530, 0x20DFAB8 })
        {
            Assert.Throws<InvalidDataException>(() => state.Read(gap, 1));
        }
    }

    [Fact]
    public void Unknown_saved_fields_and_partial_packet_tags_survive_JSON_without_refill()
    {
        NativeBattleEffect321State state = NativeBattleEffect321State.FromUnknown().WriteWord(0x221D420, 0x1020).WriteDword(0x2239528, 0x222B528);
        state = state.Write(0x221D528, new byte[] { 1, 2, 3 }).Write(0x222B528, new byte[] { 4, 5, 6 });
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect321State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal((ushort)0x1020, state.ReadWord(0x221D420));
        Assert.Equal(0x222B528u, state.ReadDword(0x2239528));
        Assert.Equal(new byte[] { 1, 2, 3 }, state.Read(0x221D528, 3));
        Assert.Equal(new byte[] { 4, 5, 6 }, state.Read(0x222B528, 3));
        Assert.Throws<InvalidDataException>(() => state.ReadDword(0x221D420));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x221D424));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x221D528));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x222B528));
    }

    [Fact]
    public void Both_fixed_pools_transfer_current_payload_and_unknownness_exclusively()
    {
        NativeBattleEffect321State state = NativeBattleEffect321State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            int last = Pools[index].Stride * Pools[index].Capacity - 2;
            NativeBattleEffectArena bank = state.ReadBank(index).Write(last, BitConverter.GetBytes((ushort)0xBEEF));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(index, bank);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect321State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal((byte)0xEF, state.ReadBank(0).Read(30, 1)[0]);
        Assert.Equal((byte)0xEF, state.ReadBank(1).Read(3598, 1)[0]);
        Assert.Throws<NotSupportedException>(() => state.ReadBank(1).Read(0, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(2));
    }

    [Fact]
    public void All_seven_saved_actor_rows_retain_packed_rotation_position_without_owning_gaps()
    {
        NativeBattleEffect321State state = NativeBattleEffect321State.FromUnknown();
        for (int actor = 0; actor < 7; actor++)
        {
            uint address = 0x221D420u + (uint)(actor * 36);
            NativeBattleEffectArena fields = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(actor + 100)).MarkUnknown(6, 2).Write(12, BitConverter.GetBytes(actor + 200));
            state = state.WriteWord(address, (ushort)actor).WriteView(address + 4, fields, 16);
        }
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect321State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        for (int actor = 0; actor < 7; actor++)
        {
            uint address = 0x221D420u + (uint)(actor * 36);
            Assert.Equal((uint)(actor + 100), state.ReadDword(address + 4));
            Assert.Equal((uint)(actor + 200), state.ReadDword(address + 16));
            Assert.Throws<NotSupportedException>(() => state.Read(address + 10, 2));
            Assert.Throws<InvalidDataException>(() => state.Read(address + 2, 2));
            Assert.Throws<InvalidDataException>(() => state.Read(address + 20, 1));
        }
    }
}
