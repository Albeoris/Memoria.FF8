using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect315StateTests
{
    [Fact]
    public void Cold_state_has_exact_scalar_ranges_and_distinct_addressable_particle_and_task_banks()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[4], state.Read(TargetGlobal, 4));
        Assert.Equal(new byte[24], state.Read(ParametersGlobal, 24));
        Assert.Equal(new byte[RootBankLength], state.Read(RootBank, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], state.Read(ChildBank, ChildBankLength));
        Assert.Equal(new byte[ParticleLength], state.Read(ParticleAddress, ParticleLength));
        Assert.Equal(RootDescriptor, RootBank + RootBankLength);
        Assert.Equal(ChildDescriptor, ParticleAddress + ParticleLength);
        Assert.Equal(ParametersGlobal, ChildBank + ChildBankLength);
        foreach (uint address in new[] { TargetGlobal + 4, RootDescriptor, ChildDescriptor, ParametersGlobal + 24, ArenaAddress })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(ParticleAddress + ParticleLength - 1, 2));
        Assert.Throws<InvalidDataException>(() => state.Read(TargetGlobal, -1));
    }

    [Fact]
    public void Canonical_particle_rows_retain_other_fields_and_unknown_bytes_across_JSON()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromUnknown();
        uint row = ParticleAddress + 199 * ParticleStride;
        state = state.WriteDword(row, 0x20).WriteWord(row + 4, 7).WriteWord(row + 22, 0xAABB);
        NativeBattleEffect315State restored = JsonSerializer.Deserialize<NativeBattleEffect315State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(0x20u, restored.ReadDword(row));
        Assert.Equal((ushort)7, restored.ReadWord(row + 4));
        Assert.Equal((ushort)0xAABB, restored.ReadWord(row + 22));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(row + 6));
        NativeBattleEffect315State cleared = restored.WriteDword(row, 0);
        Assert.Equal(0u, cleared.ReadDword(row));
        Assert.Equal((ushort)7, cleared.ReadWord(row + 4));
        Assert.Equal((ushort)0xAABB, cleared.ReadWord(row + 22));
        Assert.Throws<NotSupportedException>(() => cleared.ReadWord(row + 6));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Active_pool_transfer_removes_inactive_bank_access_without_touching_particles_or_globals()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess().WriteDword(TargetGlobal, 4).WriteDword(ParticleAddress, 1) with { RootBankStorage = null, ChildBankStorage = null };
        NativeBattleEffect315State restored = JsonSerializer.Deserialize<NativeBattleEffect315State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Null(restored.ChildBankStorage);
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(RootBank));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ChildBank));
        Assert.Equal(4u, restored.ReadDword(TargetGlobal));
        Assert.Equal(1u, restored.ReadDword(ParticleAddress));
    }

    [Fact]
    public void Unknown_import_never_invents_cold_bytes_and_malformed_owners_fail_validation()
    {
        NativeBattleEffect315State unknown = NativeBattleEffect315State.FromUnknown();
        unknown.Validate();
        foreach (uint address in new[] { TargetGlobal, ParametersGlobal, RootBank, ChildBank, ParticleAddress })
        {
            Assert.Throws<NotSupportedException>(() => unknown.ReadDword(address));
        }
        Assert.Throws<InvalidDataException>(() => (unknown with { Globals = unknown.Globals.Write(4, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (unknown with { ParticleStorage = unknown.ParticleStorage.Write(ParticleLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (unknown with { RootBankStorage = unknown.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (unknown with { ChildBankStorage = unknown.ChildBankStorage!.Write(ChildBankLength, new byte[1]) }).Validate());
    }
}
