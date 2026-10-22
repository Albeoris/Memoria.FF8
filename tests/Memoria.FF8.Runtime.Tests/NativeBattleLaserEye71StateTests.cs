using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLaserEye71StateTests
{
    [Fact]
    public void Cold_loader_globals_and_source_flag_are_known_while_imported_absence_stays_unknown()
    {
        NativeBattleLaserEye71State cold = NativeBattleLaserEye71State.FromColdProcess();
        cold.Validate();
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Equal(0u, cold.ReadSourceFlag());
        Assert.Null(cold.ExecutableBytes);
        NativeBattleLaserEye71State unknown = NativeBattleLaserEye71State.FromUnknown();
        unknown.Validate();
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadSourceFlag());
    }

    [Theory]
    [InlineData(0x25FA430u)]
    [InlineData(0x25FA4F8u)]
    [InlineData(0x25FA518u)]
    [InlineData(0x25FAAE8u)]
    [InlineData(0x25FC180u)]
    [InlineData(0x25FCC20u)]
    public void Globals_cannot_duplicate_task_pool_payload_or_descriptor_bytes(uint address)
    {
        NativeBattleLaserEye71State state = NativeBattleLaserEye71State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
        Assert.Throws<InvalidDataException>(() => state.WriteResourcePointer(address, null));
    }

    [Fact]
    public void Partial_saved_binding_metadata_and_nonzero_packet_base_survive_JSON()
    {
        NativeBattleResourcePointer pointer = new(new NativeBattleModelCpuReference(0, 32), null, 64);
        NativeBattleLaserEye71State state = NativeBattleLaserEye71State.FromUnknown().WriteResourcePointer(SavedBindingAddress, pointer).WriteDword(CursorGlobal, ArenaAddress + 0x800 + DrawBankLength).WriteDword(PacketEndGlobal, ArenaAddress + 0x800 + 2 * DrawBankLength);
        state.Validate();
        string json = JsonSerializer.Serialize(state);
        NativeBattleLaserEye71State restored = JsonSerializer.Deserialize<NativeBattleLaserEye71State>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(pointer, restored.ReadResourcePointer(SavedBindingAddress));
        Assert.Equal((uint)DrawBankLength, restored.ReadDword(PacketEndGlobal) - restored.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadView(SavedBindingAddress + 4, 28).Read(0, 28));
        Assert.Throws<NotSupportedException>(() => restored.ReadSourceFlag());
    }

    [Fact]
    public void Materialized_source_is_one_owner_and_rejects_incomplete_or_contradictory_cold_state()
    {
        byte[] bytes = new byte[SourceLength];
        bytes[0] = 1;
        NativeBattleLaserEye71State warm = NativeBattleLaserEye71State.FromUnknown() with { ExecutableBytes = bytes.ToImmutableArray() };
        warm.Validate();
        Assert.Equal(1u, warm.ReadSourceFlag());
        Assert.Throws<InvalidDataException>(() => (warm with { ColdSourcePending = true }).Validate());
        Assert.Throws<InvalidDataException>(() => (warm with { ExecutableBytes = bytes.AsSpan(0, SourceLength - 1).ToArray().ToImmutableArray() }).Validate());
    }
}
