using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAcid170Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid170StateTests
{
    [Fact]
    public void Cold_loader_and_unknown_import_distinguish_scalar_particle_and_task_storage()
    {
        NativeBattleAcid170State cold = NativeBattleAcid170State.FromColdProcess();
        NativeBattleAcid170State unknown = NativeBattleAcid170State.FromUnknown();
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
    public void Entry_flag_clear_preserves_the_particle_bank_payload_and_unknown_suffixes()
    {
        uint last = ParticleBase + (ParticleCount - 1) * ParticleStride;
        NativeBattleAcid170State state = NativeBattleAcid170State.FromUnknown().WriteDword(ParticleBase, 2).WriteDword(last, 4).WriteDword(ParticleBase + 8, 0x12345678).WriteWord(last + 22, 0x9876);
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
        NativeBattleAcid170State state = NativeBattleAcid170State.FromUnknown().WriteWord(ParticleBase + 14, 0xABCD).WriteView(ParticleBase + 8, xyz, 6).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + PacketStep);
        NativeBattleAcid170State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAcid170State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, restored.ReadBytes(ParticleBase + 8, 6));
        Assert.Equal((ushort)0xABCD, restored.ReadWord(ParticleBase + 14));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ParticleBase + 16));
        Assert.Equal(ArenaAddress, restored.ReadDword(WorkingGlobal));
        Assert.Equal(ArenaAddress + PacketStep, restored.ReadDword(CursorGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Sparse_carrier_excludes_task_banks_descriptors_unused_gaps_and_COMMON_packet_bytes()
    {
        NativeBattleAcid170State state = NativeBattleAcid170State.FromColdProcess();
        foreach (uint address in new[] { RootBank, RootDescriptor, ChildBank, ChildDescriptor, FirstTargetGlobal + 4, ParticleBase + ParticleBankBytes, ParametersGlobal + 4, CasterGlobal + 4, InitializerGlobal + 4, RawSpriteRegistersBase + RawSpriteRegistersLength, ArenaAddress })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Equal(ChildDescriptor, ParticleBase + ParticleBankBytes);
        Assert.Equal(Bone38PointGlobal, ChildBank + ChildBankLength);
        Assert.Equal(WorkingGlobal + 4, CursorGlobal);
    }

    [Fact]
    public void Selected_vertex_XYZ_and_curve_views_preserve_W_and_shared_raw_register_fronts()
    {
        NativeBattleAcid170State state = NativeBattleAcid170State.FromUnknown().WriteWord(SelectedVerticesBase + 6, 0xABCD).WriteWord(CurveBase + CurveLength - 2, 0x9876).WriteDword(RawSpriteRegistersBase + RawSpriteRegistersLength - 4, 0x12345678);
        NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 2, 3, 4, 5, 6 });
        state = state.WriteView(SelectedVerticesBase, xyz, 6);
        state.Validate();
        Assert.Equal((ushort)0xABCD, state.ReadWord(SelectedVerticesBase + 6));
        Assert.Equal((ushort)0x9876, state.ReadWord(CurveBase + CurveLength - 2));
        Assert.Equal(0x12345678u, state.ReadDword(RawSpriteRegistersBase + RawSpriteRegistersLength - 4));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(SelectedVerticesBase + 8));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CurveBase));
    }

    [Fact]
    public void Active_task_transfer_revokes_inactive_owners_and_retained_task_bytes_remain_bounded()
    {
        NativeBattleAcid170State state = NativeBattleAcid170State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ChildBank - BaseAddress)), new byte[1]) }).Validate());
    }
}
