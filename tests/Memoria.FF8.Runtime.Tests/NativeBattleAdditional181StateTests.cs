using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAdditional181StateTests
{
    [Fact]
    public void Cold_loader_and_unknown_import_distinguish_scalar_particle_and_task_storage()
    {
        NativeBattleAdditional181State cold = NativeBattleAdditional181State.FromColdProcess();
        NativeBattleAdditional181State unknown = NativeBattleAdditional181State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkingGlobal));
        Assert.Equal(new byte[ParticleBankBytes], cold.Read(ParticleBase, ParticleBankBytes));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(ParticleBase + ParticleBankBytes - 2));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(0, 4));
    }

    [Fact]
    public void Entry_flag_clear_preserves_the_single_shared_bank_payload_and_unknown_suffixes()
    {
        uint last = ParticleBase + (ParticleCount - 1) * ParticleStride;
        NativeBattleAdditional181State state = NativeBattleAdditional181State.FromUnknown().WriteDword(ParticleBase, 2).WriteDword(last, 4).WriteDword(ParticleBase + 8, 0x12345678).WriteWord(last + 22, 0x9876);
        for (uint index = 0; index < ParticleCount; index++)
        {
            state = state.WriteDword(ParticleBase + index * ParticleStride, 0);
        }
        Assert.Equal(0u, state.ReadDword(last));
        Assert.Equal(0x12345678u, state.ReadDword(ParticleBase + 8));
        Assert.Equal((ushort)0x9876, state.ReadWord(last + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ParticleBase + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(last + 8));
    }

    [Fact]
    public void Partial_particle_views_retain_W_velocity_masks_and_current_COMMON_cursor_through_JSON()
    {
        NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 2, 3, 4, 5, 6 });
        NativeBattleAdditional181State state = NativeBattleAdditional181State.FromUnknown().WriteWord(ParticleBase + 14, 0xABCD).WriteView(ParticleBase + 8, xyz, 6).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + PacketStep);
        NativeBattleAdditional181State restored = JsonSerializer.Deserialize<NativeBattleAdditional181State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, restored.ReadBytes(ParticleBase + 8, 6));
        Assert.Equal((ushort)0xABCD, restored.ReadWord(ParticleBase + 14));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ParticleBase + 16));
        Assert.Equal(ArenaAddress, restored.ReadDword(WorkingGlobal));
        Assert.Equal(ArenaAddress + PacketStep, restored.ReadDword(CursorGlobal));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Sparse_carrier_excludes_task_banks_descriptors_unused_gaps_and_COMMON_packet_bytes()
    {
        NativeBattleAdditional181State state = NativeBattleAdditional181State.FromColdProcess();
        foreach (uint address in new[] { RootBank, RootDescriptor, ChildBank, ChildDescriptor, FirstTargetGlobal + 4, ParticleBase + ParticleBankBytes, CursorGlobal + 4, ArenaAddress })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Equal(ChildDescriptor, ParticleBase + ParticleBankBytes);
        Assert.Equal(ParametersGlobal, ChildBank + ChildBankLength);
        Assert.Equal(InitializerGlobal + 4, WorkingGlobal);
        Assert.Equal(WorkingGlobal + 4, CursorGlobal);
    }

    [Fact]
    public void Active_task_transfer_revokes_inactive_owners_and_retained_task_bytes_remain_bounded()
    {
        NativeBattleAdditional181State state = NativeBattleAdditional181State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ChildBank - BaseAddress)), new byte[1]) }).Validate());
    }
}
