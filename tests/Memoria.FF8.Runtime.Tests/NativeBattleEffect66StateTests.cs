using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66StateTests
{
    [Fact]
    public void Cold_partial_matrix_and_pointer_origins_preserve_physical_holes()
    {
        NativeBattleEffect66State state = NativeBattleEffect66State.FromColdProcess();
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(uint.MaxValue, state.ReadDword(InitializedBase));
        Assert.Equal(uint.MaxValue, state.ReadDword(InitializedBase + 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x24C9184, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x24C91AC, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 1));
    }

    [Fact]
    public void Unknown_matrix_lanes_and_pointer_imports_remain_unknown_after_partial_writes()
    {
        NativeBattleEffect66State state = NativeBattleEffect66State.FromUnknown();
        state = state.WriteWord(PartialMatrixAddress, 4096).WriteWord(PartialMatrixAddress + 8, 2048).WriteWord(PartialMatrixAddress + 16, 1024).WriteDword(PartialMatrixAddress + 20, 17);
        Assert.Equal((ushort)4096, state.ReadWord(PartialMatrixAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(PartialMatrixAddress + 2));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(PartialMatrixAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(InitializedBase));
        state.Validate();
    }

    [Fact]
    public void Fixed_banks_transfer_exclusively_and_retain_callback_payload()
    {
        NativeBattleEffect66State state = NativeBattleEffect66State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(1).Write(12, BitConverter.GetBytes((ushort)123));
        state = state.DetachBank(1);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(ChildBank));
        state = state.AttachBank(ChildBank, bank);
        Assert.Equal((ushort)123, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(state.ReadBank(1).Read(12, 2)));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(1).Read(14, 2));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(1, bank));
        state.Validate();
    }

    [Fact]
    public void Json_continuation_keeps_warm_matrix_padding_pointer_and_record_knownness()
    {
        NativeBattleEffect66State state = NativeBattleEffect66State.FromUnknown().WriteWord(PartialMatrixAddress + 18, 0xCAFE).WriteDword(InitializedBase + 4, 0x20DFAB8).WriteDword(FirstPointAddress, 1);
        NativeBattleEffect66State restored = JsonSerializer.Deserialize<NativeBattleEffect66State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal((ushort)0xCAFE, restored.ReadWord(PartialMatrixAddress + 18));
        Assert.Equal(0x20DFAB8u, restored.ReadDword(InitializedBase + 4));
        Assert.Equal(1u, restored.ReadDword(FirstPointAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(FirstPointAddress + 4));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(InitializedBase));
    }
}
