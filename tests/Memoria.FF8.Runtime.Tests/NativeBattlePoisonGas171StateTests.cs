using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattlePoisonGas171Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePoisonGas171StateTests
{
    [Fact]
    public void Cold_loader_and_unknown_import_distinguish_scalar_particle_and_task_storage()
    {
        NativeBattlePoisonGas171State cold = NativeBattlePoisonGas171State.FromColdProcess();
        NativeBattlePoisonGas171State unknown = NativeBattlePoisonGas171State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkingGlobal));
        Assert.Equal(new byte[ParticleBankBytes], cold.Read(ParticleABase, ParticleBankBytes));
        Assert.Equal(new byte[ParticleBankBytes], cold.Read(ParticleBBase, ParticleBankBytes));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(ParticleABase + ParticleBankBytes - 2));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(0, 4));
    }

    [Fact]
    public void Entry_flag_clear_preserves_both_physical_banks_payload_and_unknown_suffixes()
    {
        uint last = ParticleABase + (ParticleCount - 1) * ParticleStride;
        NativeBattlePoisonGas171State state = NativeBattlePoisonGas171State.FromUnknown().WriteDword(ParticleABase, 2).WriteDword(last, 4).WriteDword(ParticleABase + 8, 0x12345678).WriteWord(last + 22, 0x9876).WriteWord(ParticleBBase + 22, 0x4321);
        for (uint index = 0; index < ParticleCount; index++)
        {
            state = state.WriteDword(ParticleABase + index * ParticleStride, 0).WriteDword(ParticleBBase + index * ParticleStride, 0);
        }
        Assert.Equal(0u, state.ReadDword(last));
        Assert.Equal(0u, state.ReadDword(ParticleBBase));
        Assert.Equal((ushort)0x4321, state.ReadWord(ParticleBBase + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ParticleBBase + 4));
        Assert.Equal(0x12345678u, state.ReadDword(ParticleABase + 8));
        Assert.Equal((ushort)0x9876, state.ReadWord(last + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ParticleABase + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(last + 8));
    }

    [Fact]
    public void Partial_particle_views_retain_W_velocity_masks_and_current_COMMON_cursor_through_JSON()
    {
        NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 2, 3, 4, 5, 6 });
        NativeBattlePoisonGas171State state = NativeBattlePoisonGas171State.FromUnknown().WriteWord(ParticleABase + 14, 0xABCD).WriteView(ParticleABase + 8, xyz, 6).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + PacketStep);
        NativeBattlePoisonGas171State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePoisonGas171State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, restored.ReadBytes(ParticleABase + 8, 6));
        Assert.Equal((ushort)0xABCD, restored.ReadWord(ParticleABase + 14));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ParticleABase + 16));
        Assert.Equal(ArenaAddress, restored.ReadDword(WorkingGlobal));
        Assert.Equal(ArenaAddress + PacketStep, restored.ReadDword(CursorGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Sparse_carrier_excludes_task_banks_descriptors_unused_gaps_and_COMMON_packet_bytes()
    {
        NativeBattlePoisonGas171State state = NativeBattlePoisonGas171State.FromColdProcess();
        foreach (uint address in new[] { RootBank, RootDescriptor, ChildBank, ChildDescriptor, FirstTargetGlobal + 4, ParticleBBase + ParticleBankBytes, CursorGlobal + 4, ArenaAddress })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Equal(ParticleBBase, ParticleABase + ParticleBankBytes);
        Assert.Equal(ChildDescriptor, ParticleBBase + ParticleBankBytes);
        Assert.Equal(ParametersGlobal, ChildBank + ChildBankLength);
        Assert.Equal(InitializerGlobal + 4, WorkingGlobal);
        Assert.Equal(WorkingGlobal + 4, CursorGlobal);
    }

    [Fact]
    public void Active_task_transfer_revokes_inactive_owners_and_retained_task_bytes_remain_bounded()
    {
        NativeBattlePoisonGas171State state = NativeBattlePoisonGas171State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ChildBank - BaseAddress)), new byte[1]) }).Validate());
    }
}
