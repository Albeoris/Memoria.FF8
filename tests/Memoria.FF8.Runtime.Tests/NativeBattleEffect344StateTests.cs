using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect344StateTests
{
    [Fact]
    public void Cold_origin_owns_only_exact_globals_and_two_inactive_physical_banks()
    {
        NativeBattleEffect344State state = NativeBattleEffect344State.FromColdProcess();
        state.Validate();
        Assert.Equal(2, state.BankStorage.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[16], state.ReadBank(0).Read(0, 16));
        Assert.Equal(new byte[3600], state.ReadBank(1).Read(0, 3600));
        Assert.Throws<InvalidDataException>(() => state.Read(BaseAddress + 4, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(ChildBank, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(CompletionGlobal + 1, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x24E0BC8, 4));
    }

    [Fact]
    public void Unknown_import_requires_actual_byte_producers_and_preserves_adjacent_unknowns()
    {
        NativeBattleEffect344State state = NativeBattleEffect344State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => state.Read(CompletionGlobal, 1));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        state = state.Write(CompletionGlobal, [1]).WriteDword(CasterGlobal, 4);
        Assert.Equal((byte)1, state.Read(CompletionGlobal, 1)[0]);
        Assert.Equal(4u, state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ParametersGlobal));
        NativeBattleEffectArena partial = state.ReadView(CursorGlobal, 4).Write(0, BitConverter.GetBytes((ushort)0xBEEF));
        state = state.WriteView(CursorGlobal, partial, 4);
        Assert.Equal((ushort)0xBEEF, state.ReadWord(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CursorGlobal));
        state.Validate();
    }

    [Fact]
    public void Bank_transfer_keeps_retained_payload_knownness_and_forbids_duplicate_ownership()
    {
        NativeBattleEffect344State state = NativeBattleEffect344State.FromUnknown();
        NativeBattleEffectArena child = state.ReadBank(1).Write(12, BitConverter.GetBytes((ushort)35)).Write(3598, BitConverter.GetBytes((ushort)0xCAFE));
        state = state.DetachBank(1);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
        Assert.Throws<InvalidDataException>(() => state.DetachBank(1));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(1, NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(child)).Write(3600, [1])));
        state = state.AttachBank(1, child);
        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(state.ReadBank(1).Read(3598, 2)));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(1).Read(14, 2));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(1, child));
        Assert.Throws<InvalidDataException>(() => state.DetachBank(2));
    }

    [Fact]
    public void Json_retains_completion_byte_current_scalar_and_exclusive_bank_owner()
    {
        NativeBattleEffect344State state = NativeBattleEffect344State.FromUnknown().Write(CompletionGlobal, [1]).WriteDword(CursorGlobal, 0x24E8BC8).DetachBank(0);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        NativeBattleEffect344State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect344State>(json)!);
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal((byte)1, restored.Read(CompletionGlobal, 1)[0]);
        Assert.Equal(0x24E8BC8u, restored.ReadDword(CursorGlobal));
        Assert.Null(restored.BankStorage[0]);
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(TargetGlobal));
    }
}
