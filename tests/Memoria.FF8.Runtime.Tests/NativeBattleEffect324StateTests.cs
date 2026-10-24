using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect324StateTests
{
    [Fact]
    public void Cold_process_factory_owns_exact_ranges_and_two_exclusive_native_banks()
    {
        NativeBattleEffect324State state = Assert.IsType<NativeBattleEffect324State>(NativeBattleEffectMemory.FromColdProcess().Effect324);
        state.Validate();
        Assert.Equal(10, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[16], state.ReadBank(0).Read(0, 16));
        Assert.Equal(new byte[3600], state.ReadBank(1).Read(0, 3600));
        Assert.Throws<InvalidDataException>(() => state.Read(0x22010A4, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2201428, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x22027A8, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2203C54, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2201AB8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2202E28, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Width_specific_current_scalars_and_actual_packet_tag_knownness_survive_JSON()
    {
        NativeBattleEffect324State state = NativeBattleEffect324State.FromUnknown().WriteWord(0x2203C4C, 3).WriteDword(0x221BC60, 0x220BC60);
        state = state.Write(0x220BC60, new byte[] { 1, 2, 3 }).Write(0x2213C60, new byte[] { 4, 5, 6 });
        state = JsonSerializer.Deserialize<NativeBattleEffect324State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal((ushort)3, state.ReadWord(0x2203C4C));
        Assert.Equal(0x220BC60u, state.ReadDword(0x221BC60));
        Assert.Equal(new byte[] { 1, 2, 3 }, state.Read(0x220BC60, 3));
        Assert.Equal(new byte[] { 4, 5, 6 }, state.Read(0x2213C60, 3));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x2203C4C));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x220BC60));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x2213C60));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x2203C48));
    }

    [Fact]
    public void Both_fixed_banks_transfer_exclusively_with_retained_payload_unknownness()
    {
        NativeBattleEffect324State state = NativeBattleEffect324State.FromUnknown();
        for (int index = 0; index < 2; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(14, BitConverter.GetBytes((ushort)0xBEEF));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(index, bank);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect324State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(BitConverter.GetBytes((ushort)0xBEEF), state.ReadBank(0).Read(14, 2));
        Assert.Equal(BitConverter.GetBytes((ushort)0xBEEF), state.ReadBank(1).Read(14, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(1).Read(16, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(2));
    }

    [Fact]
    public void Packed_current_bone_and_world_copies_preserve_source_padding_without_extending_capacity()
    {
        NativeBattleEffect324State state = NativeBattleEffect324State.FromColdProcess();
        NativeBattleEffectArena matrix = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)4096)).MarkUnknown(18, 2).Write(20, BitConverter.GetBytes(1234));
        state = state.WriteView(0x2201080, matrix, 32).WriteView(0x22010A8 + 27 * 32, matrix, 32).WriteView(0x2202428 + 27 * 32, matrix, 32);
        state = JsonSerializer.Deserialize<NativeBattleEffect324State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        foreach (uint address in new uint[] { 0x2201080, 0x22010A8 + 27 * 32, 0x2202428 + 27 * 32 })
        {
            Assert.Equal(1234u, state.ReadDword(address + 20));
            Assert.Throws<NotSupportedException>(() => state.Read(address + 18, 2));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x22010A8 + 28 * 32, 32));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2202428 + 28 * 32, 32));
    }
}
