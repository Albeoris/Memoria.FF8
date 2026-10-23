using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAdditional182Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAdditional182StateTests
{
    [Fact]
    public void Loader_zero_and_unknown_import_distinguish_globals_particles_history_and_task_owners()
    {
        NativeBattleAdditional182State cold = NativeBattleAdditional182State.FromColdProcess();
        NativeBattleAdditional182State unknown = NativeBattleAdditional182State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkingGlobal));
        Assert.Equal(new byte[HistoryBytes], cold.Read(HistoryAddress, HistoryBytes));
        Assert.Equal(new byte[ParticleStride], cold.Read(ParticleABase + (ParticleCountA - 1) * ParticleStride, ParticleStride));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(HistoryAddress + HistoryBytes - 2));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(ParticleBBase));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(0, 4));
    }

    [Fact]
    public void Native_120_and_100_particle_flag_clears_preserve_warm_payload_and_unknown_padding()
    {
        uint lastA = ParticleABase + (ParticleCountA - 1) * ParticleStride;
        NativeBattleAdditional182State state = NativeBattleAdditional182State.FromUnknown().WriteDword(lastA + 8, 0x12345678).WriteWord(ParticleBBase + 22, 0x9876);
        for (uint index = 0; index < ParticleCountA; index++)
        {
            state = state.WriteDword(ParticleABase + index * ParticleStride, 0);
        }
        for (uint index = 0; index < ParticleCountB; index++)
        {
            state = state.WriteDword(ParticleBBase + index * ParticleStride, 0);
        }
        Assert.Equal(0u, state.ReadDword(lastA));
        Assert.Equal(0u, state.ReadDword(ParticleBBase + (ParticleCountB - 1) * ParticleStride));
        Assert.Equal(0x12345678u, state.ReadDword(lastA + 8));
        Assert.Equal((ushort)0x9876, state.ReadWord(ParticleBBase + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(lastA + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ParticleBBase + 8));
    }

    [Fact]
    public void Five_history_flag_clears_preserve_current_matrix_bytes_and_unknownness_through_JSON()
    {
        NativeBattleAdditional182State state = NativeBattleAdditional182State.FromUnknown();
        byte[] matrix = Enumerable.Range(0, 32).Select(index => (byte)(index + 1)).ToArray();
        uint last = HistoryAddress + (HistoryCount - 1) * HistoryStride;
        state = state.WriteView(last + 4, NativeBattleEffectArena.Clear().Write(0, matrix), matrix.Length);
        for (uint row = 0; row < HistoryCount; row++)
        {
            state = state.WriteDword(HistoryAddress + row * HistoryStride, 0);
        }
        state = state.WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + PacketStep);
        NativeBattleAdditional182State restored = JsonSerializer.Deserialize<NativeBattleAdditional182State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(matrix, restored.ReadView(last + 4, 32).Read(0, 32));
        Assert.Equal(0u, restored.ReadDword(last));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(HistoryAddress + 4));
        Assert.Equal(ArenaAddress, restored.ReadDword(WorkingGlobal));
        Assert.Equal(ArenaAddress + PacketStep, restored.ReadDword(CursorGlobal));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Carrier_excludes_descriptors_task_banks_COMMON_and_unused_gaps()
    {
        NativeBattleAdditional182State state = NativeBattleAdditional182State.FromColdProcess();
        foreach (uint address in new[] { FirstTargetGlobal + 4, RootBank, RootDescriptor, ChildBank, ChildDescriptor, ParametersGlobal - 4, InitializerGlobal + 4, CursorGlobal + 4, ArenaAddress })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Equal(ParticleBBase, ParticleABase + ParticleBankABytes);
        Assert.Equal(ChildDescriptor, ParticleBBase + ParticleBankBBytes);
        Assert.Equal(WorkingGlobal, HistoryAddress + HistoryBytes);
        Assert.Equal(ParametersGlobal, ChildBank + ChildBankLength);
    }

    [Fact]
    public void Active_bank_transfer_revokes_inactive_owners_and_retained_storage_is_bounded()
    {
        NativeBattleAdditional182State state = NativeBattleAdditional182State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ChildBank - BaseAddress)), new byte[1]) }).Validate());
    }
}
